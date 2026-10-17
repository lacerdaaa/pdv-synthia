using Microsoft.Extensions.FileProviders;
using Npgsql;
using Synthia.Contracts.Catalog;
using Synthia.Contracts.Sync;
using Synthia.Server.Backoffice;
using Synthia.Server.Catalog;
using Synthia.Server.Data;
using Synthia.Server.Sync;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(
    builder.Configuration.GetConnectionString("Synthia")
    ?? throw new InvalidOperationException("ConnectionStrings:Synthia não configurada.")));
builder.Services.AddSingleton<Migrator>();
builder.Services.AddSingleton<Projector>();
builder.Services.AddSingleton<EventStore>();
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<BackofficeEvents>();
builder.Services.AddSingleton<StockService>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "PDV Synthia · Servidor local";
    doc.Info.Description = "Sync caixa ↔ servidor e back-office. Documentação interativa em /docs.";
    return Task.CompletedTask;
}));

var app = builder.Build();

await app.Services.GetRequiredService<Migrator>().MigrateAsync();

// Documentação interativa (docs/ do repositório, copiada para a saída do build) + OpenAPI.
var docsPath = Path.Combine(AppContext.BaseDirectory, "docs");
if (Directory.Exists(docsPath))
{
    var docs = new PhysicalFileProvider(docsPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = docs, RequestPath = "/docs" });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = docs, RequestPath = "/docs" });
    app.MapGet("/", () => Results.Redirect("/docs/")).ExcludeFromDescription();
}
app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags("Sistema").WithName("Health")
    .WithSummary("Verifica se o servidor está no ar");

// Caixa → servidor
var sync = app.MapGroup("").WithTags("Sync");

sync.MapPost(SyncRoutes.Events, async (PushEventsRequest request, EventStore store, CancellationToken ct) =>
{
    if (request.Events.Count > 1000)
        return Results.BadRequest("Lote acima de 1000 eventos.");

    var accepted = await store.AppendAsync(request.Events, ct);
    return Results.Ok(new PushEventsResponse(accepted));
})
    .WithName("PushEvents")
    .WithSummary("Caixa envia um lote de eventos da outbox")
    .WithDescription("Grava com ON CONFLICT (event_id) DO NOTHING e alimenta as projeções na mesma transação. " +
                     "Devolve todos os event_id aceitos, inclusive os que já existiam: reenviar é sempre seguro.")
    .Produces<PushEventsResponse>()
    .Produces<string>(StatusCodes.Status400BadRequest);

// Servidor → caixas
sync.MapGet(SyncRoutes.Catalog, (long since, CatalogService catalog, CancellationToken ct) =>
    catalog.GetDeltaAsync(since, ct))
    .WithName("GetCatalogDelta")
    .WithSummary("Delta do catálogo depois da versão do caixa")
    .WithDescription($"Páginas de {CatalogService.DeltaPageSize} alterações. Pede snapshot (snapshotRequired) se o caixa é novo, " +
                     $"está à frente do servidor ou ficou mais de {CatalogService.MaxDeltaDistance} versões para trás.");

sync.MapGet(SyncRoutes.CatalogSnapshot, (CatalogService catalog, CancellationToken ct) =>
    catalog.GetSnapshotAsync(ct))
    .WithName("GetCatalogSnapshot")
    .WithSummary("Catálogo completo e preços agendados ainda válidos");

// Back-office (mínimo, até existir a interface web)
var backoffice = app.MapGroup("/backoffice").WithTags("Back-office");

backoffice.MapGet("/products", (CatalogService catalog, CancellationToken ct) => catalog.ListProductsAsync(ct))
    .WithName("ListProducts")
    .WithSummary("Lista o cadastro atual de produtos");

backoffice.MapPut("/products/{id:guid}", async (Guid id, ProductData product, CatalogService catalog, CancellationToken ct) =>
{
    if (id != product.Id) return Results.BadRequest("Id da rota difere do corpo.");
    var version = await catalog.UpsertProductAsync(product, ct);
    return Results.Ok(new CatalogVersionResponse(version));
})
    .WithName("UpsertProduct")
    .WithSummary("Cria ou altera um produto")
    .WithDescription("Gera uma linha em catalog_changes com nova versão. Exclusão é lógica: active = false.")
    .Produces<CatalogVersionResponse>()
    .Produces<string>(StatusCodes.Status400BadRequest);

backoffice.MapPost("/scheduled-prices", async (ScheduledPriceData price, CatalogService catalog, CancellationToken ct) =>
{
    var version = await catalog.SchedulePriceAsync(price, ct);
    return Results.Ok(new CatalogVersionResponse(version));
})
    .WithName("SchedulePrice")
    .WithSummary("Agenda um preço")
    .WithDescription("Os caixas recebem antes e aplicam sozinhos em effectiveAt, mesmo offline.")
    .Produces<CatalogVersionResponse>();

backoffice.MapPost("/projections/rebuild", async (EventStore store, CancellationToken ct) =>
{
    await store.RebuildProjectionsAsync(ct);
    return Results.NoContent();
})
    .WithName("RebuildProjections")
    .WithSummary("Apaga as projeções e relê todo o log de eventos")
    .Produces(StatusCodes.Status204NoContent);

app.Run();

public sealed record CatalogVersionResponse(long Version);

public partial class Program;
