using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Synthia.Contracts;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Events;
using Synthia.Simulator;
using Synthia.Terminal.Core.Sales;

// Simula um dia de loja contra um servidor real (já rodando) e dois caixas reais em SQLite.
// Uso: dotnet run --project tools/Synthia.Simulator -- <url-do-servidor> <connection-string-postgres> <saida.js>
var serverUrl = new Uri(args.ElementAtOrDefault(0) ?? "http://localhost:5290");
var pgConnection = args.ElementAtOrDefault(1) ?? "Host=localhost;Username=synthia;Password=synthia;Database=synthia_sim";
var outputPath = args.ElementAtOrDefault(2) ?? "docs/simulacao-data.js";

var workDir = Path.Combine(Path.GetTempPath(), $"synthia-sim-{Guid.NewGuid():N}");
Directory.CreateDirectory(workDir);

var recorder = new Recorder();
var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
var cx01 = new SimTerminal("CX01", workDir, serverUrl, recorder, clock);
var cx02 = new SimTerminal("CX02", workDir, serverUrl, recorder, clock);
var backofficeLink = new RecordingLink("Back-office", recorder);
var backoffice = new HttpClient(backofficeLink) { BaseAddress = serverUrl };
if (Environment.GetEnvironmentVariable("SYNTHIA_API_KEY") is { Length: > 0 } apiKey)
    backoffice.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
await using var pg = NpgsqlDataSource.Create(pgConnection);
var metrics = new Metrics(pg, [cx01, cx02]);

var steps = new List<Step>();
var saleIds = new Dictionary<string, SaleCompleted>();
var products = new Dictionary<string, ProductData>();

async Task Run(string phase, string situation, string[] process, Func<Task<IEnumerable<string>>> action)
{
    clock.Advance(TimeSpan.FromMinutes(3));
    var before = await metrics.CaptureAsync();
    var notes = (await action()).ToList();
    var exchanges = recorder.Drain();
    var after = await metrics.CaptureAsync();

    var routes = exchanges.Count == 0
        ? ["local · SQLite do caixa (sem rota HTTP)"]
        : exchanges.Select(e => $"{e.Method} {e.Path.Split('?')[0]}").Distinct().ToList();
    if (exchanges.Count > 0 && process.Any(p => p.StartsWith("Caixa:")))
        routes.Insert(0, "local · SQLite do caixa");

    steps.Add(new Step(steps.Count + 1, phase, situation, routes, process, exchanges, Metrics.Diff(before, after), notes));
    Console.WriteLine($"[{steps.Count:00}] {situation}");
    foreach (var n in notes) Console.WriteLine($"     {n}");
}

async Task<string> PutProduct(ProductData p)
{
    products[p.Name] = p;
    var res = await backoffice.PutAsJsonAsync($"/backoffice/products/{p.Id}", p, SynthiaJson.Options);
    res.EnsureSuccessStatusCode();
    return $"{p.Name}: {Metrics.Money(p.PriceCents)}{(p.Active ? "" : " · inativado (active = false)")}";
}

async Task<string> Sell(SimTerminal t, string label, (string Code, decimal Qty)[] items, params SalePayment[] payments)
{
    var cart = new Cart();
    foreach (var (code, qty) in items)
    {
        var item = int.TryParse(code, out var plu) && code.Length <= 5 ? t.Catalog.FindByPlu(plu) : t.Catalog.FindByBarcode(code);
        if (item is null) return $"{t.Id}: código {code} não encontrado no catálogo local";
        cart.Add(item, qty);
    }

    var sale = await t.Sales.CompleteAsync(cart, payments);
    saleIds[label] = sale;
    var lines = string.Join("; ", sale.Items.Select(i => $"{i.Quantity.ToString("0.###", Metrics.PtBr)} × {i.Description} a {Metrics.Money(i.UnitPriceCents)} = {Metrics.Money(i.TotalCents)}"));
    return $"{t.Id}: venda {sale.SaleId.ToString()[^6..]} · {lines} · total {Metrics.Money(sale.TotalCents)} · troco {Metrics.Money(sale.ChangeCents)} · NFC-e {sale.NfceStatus}";
}

string Record<T>(SimTerminal t, string type, Guid stream, T payload)
{
    using var conn = t.Db.Open();
    using var tx = conn.BeginTransaction();
    var e = t.Outbox.Append(conn, tx, type, stream, payload);
    tx.Commit();
    return $"{t.Id}: evento {type} gravado na outbox (event_id {e.EventId.ToString()[..13]}…, seq {e.StreamSeq})";
}

ProductData P(string name, string? barcode, int? plu, string unit, long cents, string ncm) =>
    new(Guid.CreateVersion7(), name, barcode, plu, unit, cents, ncm, true);

// ───────────────────────── Fase 1 · Abertura da loja ─────────────────────────

await Run("Abertura", "Gerente cadastra o mix inicial no back-office",
[
    "Back-office: PUT /backoffice/products/{id} para cada produto.",
    "Servidor: CatalogService.UpsertProductAsync trava pg_advisory_xact_lock (versões nunca saem fora de ordem).",
    "Servidor: INSERT em catalog_changes RETURNING version + upsert em products, na mesma transação.",
], async () => new[]
{
    await PutProduct(P("Arroz 5kg", "7891000100103", null, "UN", 2899, "10063021")),
    await PutProduct(P("Feijão 1kg", "7891000200209", null, "UN", 899, "07133399")),
    await PutProduct(P("Banana prata", null, 1001, "KG", 599, "08039000")),
    await PutProduct(P("Leite 1L", "7891000300305", null, "UN", 549, "04012010")),
    await PutProduct(P("Café 500g", "7891000400401", null, "UN", 1899, "09012100")),
    await PutProduct(P("Azeite 500ml", "7891000500507", null, "UN", 3490, "15091000")),
});

await Run("Abertura", "Gerente agenda promoção do café (R$ 15,99) para daqui a 2 horas",
[
    "Back-office: POST /backoffice/scheduled-prices com effective_at.",
    "Servidor: grava scheduled_prices + catalog_changes (entity = scheduled_price).",
    "Os caixas vão receber o agendamento antes e aplicar sozinhos no horário, mesmo offline.",
], async () =>
{
    var price = new ScheduledPriceData(Guid.CreateVersion7(), products["Café 500g"].Id, 1599, clock.GetUtcNow().AddHours(2));
    (await backoffice.PostAsJsonAsync("/backoffice/scheduled-prices", price, SynthiaJson.Options)).EnsureSuccessStatusCode();
    return [$"Café 500g: R$ 15,99 a partir de {price.EffectiveAt:HH:mm} UTC"];
});

await Run("Abertura", "CX01 é ligado pela primeira vez (SQLite vazio)",
[
    "Caixa: CatalogPuller.PullAsync lê last_catalog_version = 0.",
    "Caixa: GET /sync/catalog?since=0 → servidor responde snapshotRequired = true (caixa novo).",
    "Caixa: GET /sync/catalog/snapshot → LocalCatalog.ApplySnapshot substitui products e scheduled_prices numa transação.",
], async () => [await cx01.PullRoundAsync()]);

await Run("Abertura", "CX02 é ligado",
[
    "Mesmo fluxo do CX01: delta pede snapshot, caixa baixa catálogo completo.",
], async () => [await cx02.PullRoundAsync()]);

await Run("Abertura", "Conferência de estoque antes de abrir (contagem física)",
[
    "Caixa: grava um StockCounted por produto na outbox (a tela de inventário ainda não existe; o evento é o mesmo).",
    "Caixa: OutboxPusher envia o lote → POST /sync/events.",
    "Servidor: EventStore insere com ON CONFLICT DO NOTHING; Projector grava movimento kind = count.",
    "Saldo = última contagem + movimentos posteriores a ela (view stock_balances).",
], async () =>
{
    var notes = new List<string>();
    foreach (var (name, qty) in new[] { ("Arroz 5kg", 50m), ("Feijão 1kg", 40m), ("Banana prata", 30m), ("Leite 1L", 24m), ("Café 500g", 20m), ("Azeite 500ml", 1m) })
        notes.Add(Record(cx01, EventTypes.StockCounted, products[name].Id, new StockCounted(products[name].Id, qty)) + $" · {name} = {qty}");
    notes.Add(await cx01.PushRoundAsync());
    return notes;
});

await Run("Abertura", "Maria abre o turno no CX01 com R$ 100,00 de fundo de troco",
[
    "Caixa: ShiftService.Open grava ShiftOpened na outbox e open_shift_id em meta, na mesma transação SQLite.",
    "Caixa: IOutboxSignal.Notify acorda o worker, que envia logo (POST /sync/events).",
    "Servidor: Projector cria a linha do turno em shift_totals.",
], async () =>
{
    cx01.Shifts.Open("Maria", 10000);
    return ["CX01: turno aberto", await cx01.PushRoundAsync()];
});

await Run("Abertura", "João abre o turno no CX02 com R$ 100,00",
[
    "Mesmo fluxo: ShiftOpened → outbox → POST /sync/events → shift_totals.",
], async () =>
{
    cx02.Shifts.Open("João", 10000);
    return ["CX02: turno aberto", await cx02.PushRoundAsync()];
});

// ───────────────────────── Fase 2 · Vendas normais ─────────────────────────

await Run("Vendas", "CX01: cliente leva 2 arroz e 1 feijão, paga R$ 70,00 em dinheiro",
[
    "Caixa: leitor bipa o código → LocalCatalog.FindByBarcode (preço vigente calculado no SQLite).",
    "Caixa: Cart congela preço por item; SaleService valida pagamento e troco (só de dinheiro).",
    "Caixa: INfceEmitter emite a NFC-e (simulada aqui; será a ACBr) → chave e status vão no evento.",
    "Caixa: SaleCompleted gravado na outbox (stream = venda, seq 1); worker envia.",
    "Servidor: Projector baixa estoque (movimentos kind = sale) e soma no turno.",
], async () => new[]
{
    await Sell(cx01, "cx01-arroz-feijao", [("7891000100103", 2), ("7891000200209", 1)], new SalePayment(PaymentMethods.Cash, 7000)),
    await cx01.PushRoundAsync(),
});

await Run("Vendas", "CX02: cliente pesa 1,235 kg de banana e paga no PIX",
[
    "Caixa: balança informa o peso; LocalCatalog.FindByPlu(1001).",
    "Caixa: Cart arredonda 1,235 × R$ 5,99 = R$ 7,39765 para R$ 7,40.",
    "Caixa: SaleCompleted → outbox → POST /sync/events.",
], async () => new[]
{
    await Sell(cx02, "cx02-banana", [("1001", 1.235m)], new SalePayment(PaymentMethods.Pix, 740)),
    await cx02.PushRoundAsync(),
});

await Run("Vendas", "Gerente reajusta o feijão para R$ 9,49 no back-office",
[
    "Back-office: PUT /backoffice/products/{id} → nova versão em catalog_changes.",
    "Caixas: na rodada de 30 s, GET /sync/catalog?since=<versão local> recebe só o delta.",
    "Caixas: LocalCatalog.ApplyDelta faz upsert idempotente e avança last_catalog_version.",
], async () => new[]
{
    await PutProduct(products["Feijão 1kg"] with { PriceCents = 949 }),
    await cx01.PullRoundAsync(),
    await cx02.PullRoundAsync(),
});

await Run("Vendas", "CX01: cliente compra 3 feijões no débito",
[
    "Caixa: preço já é o novo (R$ 9,49), lido do SQLite local.",
    "Caixa: pagamento com NSU do TEF gravado no evento.",
    "Servidor: o evento carrega catalog_version, então dá para auditar com qual tabela de preço a venda foi feita.",
], async () => new[]
{
    await Sell(cx01, "cx01-feijao-debito", [("7891000200209", 3)], new SalePayment(PaymentMethods.Debit, 2847, TefNsu: "004512")),
    await cx01.PushRoundAsync(),
});

// ───────────────────────── Fase 3 · Servidor local cai ─────────────────────────

await Run("Servidor fora", "O mini PC do servidor trava. CX01 vende 1 arroz em dinheiro",
[
    "Caixa: a venda é 100% local; nada nela depende do servidor.",
    "Caixa: SaleCompleted vai para a outbox normalmente.",
    "Caixa: OutboxPusher tenta POST /sync/events e falha; o evento fica com sent_at NULL e o backoff dobra (2 s → 4 s → … → 60 s).",
], async () =>
{
    cx01.Link.State = LinkState.Offline;
    cx02.Link.State = LinkState.Offline;
    backofficeLink.State = LinkState.Offline;
    return new[]
    {
        await Sell(cx01, "cx01-offline", [("7891000100103", 1)], new SalePayment(PaymentMethods.Cash, 2899)),
        await cx01.PushRoundAsync(),
    };
});

await Run("Servidor fora", "CX02 vende 2 cafés no crédito e tenta atualizar o catálogo",
[
    "Caixa: venda local; push falha e o evento acumula.",
    "Caixa: CatalogPuller falha; o catálogo congela na última versão (o caixa segue vendendo com ela).",
    "Tela do caixa: indicador de eventos pendentes de envio sobe (o gerente vê se passar de algumas centenas).",
], async () => new[]
{
    await Sell(cx02, "cx02-cafe-offline", [("7891000400401", 2)], new SalePayment(PaymentMethods.Credit, 3798, TefNsu: "009931")),
    await cx02.PushRoundAsync(),
    await cx02.PullRoundAsync(),
});

await Run("Servidor fora", "Servidor volta, mas a resposta para o CX01 se perde na rede",
[
    "Caixa: POST /sync/events chega ao servidor, que grava o evento e projeta o estoque.",
    "Rede: a resposta 200 não chega ao caixa (timeout).",
    "Caixa: sem ack, não marca sent_at; o evento continua pendente para reenvio.",
], async () =>
{
    cx02.Link.State = LinkState.Online;
    backofficeLink.State = LinkState.Online;
    cx01.Link.State = LinkState.DropNextResponse;
    return new[] { await cx01.PushRoundAsync() };
});

await Run("Servidor fora", "Workers tentam de novo: CX01 reenvia, CX02 drena o que acumulou",
[
    "Caixa: reenvia o mesmo event_id (entrega pelo menos uma vez).",
    "Servidor: INSERT … ON CONFLICT (event_id) DO NOTHING → 0 linhas, projeção não roda de novo, mas o id volta em accepted.",
    "Caixa: marca sent_at. Nada duplicou no estoque nem no turno.",
], async () => new[]
{
    await cx01.PushRoundAsync(),
    await cx02.PushRoundAsync(),
    await cx02.PullRoundAsync(),
});

// ───────────────────────── Fase 4 · Internet cai ─────────────────────────

await Run("Internet fora", "Fibra e 4G caem juntas. CX02 vende 1 leite em dinheiro",
[
    "Caixa: ACBr não alcança a SEFAZ → emite em contingência offline (tpEmis 9), imprime DANFE.",
    "Caixa: SaleCompleted com nfce_status = pending. O servidor local continua acessível pela rede interna.",
    "Servidor: Projector coloca a venda em nfce_queue (pendente de transmissão).",
], async () =>
{
    cx02.Nfce.InternetUp = false;
    return new[]
    {
        await Sell(cx02, "cx02-leite-contingencia", [("7891000300305", 1)], new SalePayment(PaymentMethods.Cash, 549)),
        await cx02.PushRoundAsync(),
    };
});

await Run("Internet fora", "Internet volta; a NFC-e em contingência é transmitida e autorizada",
[
    "Caixa: worker de contingência (a implementar) transmite o XML e recebe protocolo.",
    "Caixa: grava NfceAuthorized no stream da venda (seq 2) e envia.",
    "Servidor: Projector remove a venda de nfce_queue.",
], async () =>
{
    cx02.Nfce.InternetUp = true;
    var sale = saleIds["cx02-leite-contingencia"];
    return new[]
    {
        Record(cx02, EventTypes.NfceAuthorized, sale.SaleId, new NfceAuthorized(sale.SaleId, sale.NfceKey!, "135260004417788")),
        await cx02.PushRoundAsync(),
    };
});

await Run("Internet fora", "Uma NFC-e do CX01 volta rejeitada pela SEFAZ (NCM inválido)",
[
    "Caixa: retorno da SEFAZ vira evento NfceRejected (seq 2 no stream da venda).",
    "Servidor: Projector põe a venda em nfce_queue com status rejected e o motivo, para o back-office corrigir o cadastro e reenviar.",
], async () =>
{
    var sale = saleIds["cx01-feijao-debito"];
    return new[]
    {
        Record(cx01, EventTypes.NfceRejected, sale.SaleId, new NfceRejected(sale.SaleId, sale.NfceKey!, "778", "Informado NCM inexistente")),
        await cx01.PushRoundAsync(),
    };
});

// ───────────────────────── Fase 5 · Conflitos viram regras ─────────────────────────

await Run("Conflitos", "Gerente tira o leite de linha enquanto o CX02 está sem rede",
[
    "Back-office: PUT com active = false (exclusão é lógica, nunca DELETE).",
    "CX01: puxa o delta e o leite some da busca por código de barras.",
    "CX02: está fora da rede, não recebe; segue com o leite ativo no catálogo local.",
], async () =>
{
    cx02.Link.State = LinkState.Offline;
    return new[]
    {
        await PutProduct(products["Leite 1L"] with { Active = false }),
        await cx01.PullRoundAsync(),
        cx01.Catalog.FindByBarcode("7891000300305") is null ? "CX01: leite não é mais encontrado pelo leitor" : "CX01: leite ainda aparece",
        await cx02.PullRoundAsync(),
    };
});

await Run("Conflitos", "CX02, ainda offline, vende 2 leites; depois volta à rede",
[
    "Caixa: vende com o catálogo que tem; o evento carrega o preço praticado e a catalog_version antiga.",
    "Servidor: aceita o evento normalmente. Regra: exclusão lógica mantém o produto referenciável e o estoque baixa.",
    "Relatórios podem listar vendas feitas com versão de catálogo defasada.",
], async () =>
{
    var sale = await Sell(cx02, "cx02-leite-inativo", [("7891000300305", 2)], new SalePayment(PaymentMethods.Cash, 1098));
    cx02.Link.State = LinkState.Online;
    return new[] { sale, await cx02.PushRoundAsync(), await cx02.PullRoundAsync() };
});

await Run("Conflitos", "Os dois caixas vendem a última unidade de azeite ao mesmo tempo",
[
    "Caixas: nenhum consulta o servidor para vender; ambos vendem.",
    "Servidor: estoque fica −1. Venda nunca é bloqueada; o saldo negativo é o alerta de divergência para conferência.",
], async () => new[]
{
    await Sell(cx01, "cx01-azeite", [("7891000500507", 1)], new SalePayment(PaymentMethods.Debit, 3490, TefNsu: "004513")),
    await Sell(cx02, "cx02-azeite", [("7891000500507", 1)], new SalePayment(PaymentMethods.Cash, 3500)),
    await cx01.PushRoundAsync(),
    await cx02.PushRoundAsync(),
});

await Run("Conflitos", "Cliente do CX01 desiste do feijão da primeira venda",
[
    "Caixa: SaleService.CancelItem valida que o item é da venda e que a devolução sai por uma forma usada nela; grava SaleItemCancelled (seq 2) com refundMethod = cash. A venda original não muda.",
    "Servidor: Projector procura o SaleCompleted do stream, estorna a quantidade (kind = item_cancelled), subtrai o valor do turno e, por ser em dinheiro, do dinheiro esperado na gaveta.",
], async () =>
{
    var sale = saleIds["cx01-arroz-feijao"];
    var feijao = sale.Items.Single(i => i.Description.StartsWith("Feijão"));
    cx01.Sales.CancelItem(sale.SaleId, feijao.ItemId, "cliente desistiu", PaymentMethods.Cash);
    return new[] { $"CX01: item {feijao.Description} cancelado ({Metrics.Money(feijao.TotalCents)})", await cx01.PushRoundAsync() };
});

await Run("Conflitos", "CX02 cancela a venda inteira da banana (PIX estornado)",
[
    "Caixa: SaleService.CancelSale grava SaleCancelled no stream da venda.",
    "Servidor: estorna todos os itens ainda não estornados, desconta a venda do turno e tira da fila de NFC-e.",
], async () =>
{
    var sale = saleIds["cx02-banana"];
    cx02.Sales.CancelSale(sale.SaleId, "pagamento PIX estornado");
    return new[] { "CX02: venda da banana cancelada", await cx02.PushRoundAsync() };
});

// ───────────────────────── Fase 6 · Preço agendado ─────────────────────────

await Run("Preço agendado", "Passam 2 horas; CX01 vende 1 café sem falar com o servidor",
[
    "Caixa: LocalCatalog calcula o preço vigente no SQLite: agendamento com effective_at ≤ agora e versão posterior à última edição do produto.",
    "Caixa: todos os caixas mudam juntos, mesmo que algum esteja offline no horário.",
], async () =>
{
    clock.Advance(TimeSpan.FromHours(2));
    return new[]
    {
        await Sell(cx01, "cx01-cafe-promo", [("7891000400401", 1)], new SalePayment(PaymentMethods.Cash, 2000)),
        await cx01.PushRoundAsync(),
    };
});

// ───────────────────────── Fase 7 · Fechamento ─────────────────────────

await Run("Fechamento", "Sangria de R$ 50,00 no CX01",
[
    "Caixa: ShiftService.Withdraw grava CashWithdrawal no stream do turno.",
    "Servidor: soma em withdrawals_cents; o dinheiro esperado na gaveta cai.",
], async () =>
{
    cx01.Shifts.Withdraw(5000, "sangria para o cofre");
    return new[] { "CX01: sangria registrada", await cx01.PushRoundAsync() };
});

await Run("Fechamento", "Maria e João fecham os turnos contando a gaveta",
[
    "Caixa: ShiftService.Close grava ShiftClosed com o dinheiro declarado e limpa open_shift_id.",
    "Servidor: shift_totals guarda declarado × esperado (fundo + vendas em dinheiro − troco − sangrias) para conferência.",
], async () =>
{
    // Valores contados na gaveta física. No CX01 o cliente do feijão recebeu R$ 8,99 de volta em dinheiro.
    cx01.Shifts.Close(15296);
    cx02.Shifts.Close(15137);
    return new[] { "CX01: Maria conta R$ 152,96 (o feijão cancelado foi devolvido em dinheiro)", "CX02: João conta R$ 151,37", await cx01.PushRoundAsync(), await cx02.PushRoundAsync() };
});

await Run("Fechamento", "Auditoria: apagar e reconstruir todas as projeções a partir do log",
[
    "Back-office: POST /backoffice/projections/rebuild.",
    "Servidor: TRUNCATE stock_movements, shift_totals, nfce_queue e relê events em ordem de event_id.",
    "Esperado: nenhum número muda. É o que garante restaurar o servidor por pg_dump + replay das outboxes.",
], async () =>
{
    (await backoffice.PostAsync("/backoffice/projections/rebuild", null)).EnsureSuccessStatusCode();
    return ["Projeções reconstruídas"];
});

// ───────────────────────── Saída ─────────────────────────

var final = await metrics.CaptureAsync();
var trace = new
{
    generatedAt = DateTimeOffset.UtcNow,
    terminals = new[] { "CX01", "CX02" },
    steps,
    final = final.Select(kv => new { key = kv.Key, value = kv.Value }),
};
var json = JsonSerializer.Serialize(trace, new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
});
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
await File.WriteAllTextAsync(outputPath, $"// Gerado por tools/Synthia.Simulator. Não editar à mão.\nwindow.SIMULACAO = {json};\n");
Console.WriteLine($"\n{steps.Count} passos gravados em {outputPath}");

Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
Directory.Delete(workDir, recursive: true);
