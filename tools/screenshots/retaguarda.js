// Capturas da retaguarda usadas na documentação (docs/img/retaguarda-*.png).
// Monta um dia de loja de verdade pelo próprio sistema: cadastro da loja, importação de produtos, entrada da loja
// antiga, vendas dos 4 caixas pelo sync (com devolução, cancelamento, contingência e rejeição de NFC-e), turnos,
// inventário, nota XML, ajuste e usuários. Rode por tools/screenshots.sh, que sobe um servidor descartável.
//
//   node tools/screenshots/retaguarda.js http://localhost:5298 docs/img
const { chromium } = require('playwright');
const crypto = require('crypto');

// UUIDv7 crescente, como os caixas geram (Uuid7): o servidor processa cada lote na ordem dos ids.
let lastMs = 0, counter = 0;
function uuid7() {
  const ms = Date.now();
  counter = ms === lastMs ? counter + 1 : 0;
  lastMs = ms;
  const b = crypto.randomBytes(16);
  b.writeUIntBE(ms, 0, 6);
  b[6] = 0x70 | ((counter >> 8) & 0x0f);
  b[7] = counter & 0xff;
  b[8] = 0x80 | (b[8] & 0x3f);
  const h = b.toString('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}
const fs = require('fs');
const os = require('os');
const path = require('path');

const [base, outDir] = process.argv.slice(2);
const root = path.resolve(__dirname, '..', '..');
const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'synthia-prints-'));
const TERMINAL_KEYS = { CX01: 'docs-cx01', CX02: 'docs-cx02', CX03: 'docs-cx03', CX04: 'docs-cx04' };

const PRODUCTS_CSV = [
  'codigo_barras;plu;descricao;unidade;preco;ncm',
  '7891000100103;;Arroz Tipo 1 5kg;UN;28,99;10063021',
  '7891000200209;;Feijão Carioca 1kg;UN;8,99;07133399',
  '7891000400401;;Café Torrado 500g;UN;18,99;09012100',
  '7891000300305;;Leite Integral 1L;UN;5,49;04012010',
  '7891000500507;;Azeite Extravirgem 500ml;UN;34,90;15091000',
  '7891000600603;;Refrigerante Cola 2L;UN;9,99;22021000',
  ';1001;Banana Prata;KG;5,99;08039000',
  ';1002;Pão Francês;KG;16,90;19059090',
  '7891000700709;;Açúcar Refinado 1kg;UN;4,79;',
  '7891000100103;;Arroz duplicado;UN;27,90;10063021',
  ';;Sem código;UN;3,00;',
].join('\n');

const TRANSFER_CSV = [
  'codigo;quantidade;custo',
  '7891000400401;360;', '1002;180;', '7891000100103;420;', '7891000200209;480;', '1001;190;', '7891000600603;420;',
  '7891000300305;480;', '7891000500507;300;', '7891000700709;360;', '9999999;5;',
].join('\n');

function write(name, content) {
  const file = path.join(tmp, name);
  fs.writeFileSync(file, content);
  return file;
}

// Hoje no fuso da loja, para as vendas caírem no "Hoje" da retaguarda.
const today = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date());
const at = (h, m) => `${today}T${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:00-03:00`;

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
  page.on('pageerror', e => console.error('erro na página:', e.message));

  const go = async p => { await page.goto(base + p); await page.waitForTimeout(1200); };
  const shot = async (name, full = false) => {
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(outDir, `${name}.png`), fullPage: full });
    console.log('  ' + name);
  };
  const field = label => page.locator('label', { hasText: label }).locator('input, select').first();

  // Login.
  await go('/retaguarda/login');
  await page.fill('input[autocomplete=username]', 'admin');
  await page.fill('input[type=password]', 'synthia');
  await page.click('button[type=submit]');
  await page.waitForURL('**/retaguarda');

  // Loja: o Xandão, cliente zero. Primeiro, para todas as telas saírem com a logo dele.
  await go('/retaguarda/loja');
  await field('Nome fantasia').fill('Xandão Supermercados');
  await field('Razão social').fill('Xandão Comércio de Alimentos Ltda');
  await field('CNPJ').fill('11.222.333/0001-81');
  await field('Inscrição estadual').fill('123.456.789.110');
  await field('Endereço').fill('Av. Brasil, 1000 - Centro - São Paulo/SP');
  await field('Rodapé').fill('Vem pro Xandão! Obrigado pela preferência.');
  await page.locator('input[type=color]').first().fill('#c8272e');
  await page.locator('input[type=file]').setInputFiles(path.join(root, 'docs/img/logo-xandao.png'));
  await page.waitForTimeout(1500);
  await page.click('button.primary:has-text("Salvar")');
  await page.waitForTimeout(2500);
  await go('/retaguarda/loja');
  await shot('retaguarda-13-loja');

  // Produtos: planilha com dois erros e um aviso.
  await go('/retaguarda/produtos/importar');
  await page.locator('input[type=file]').setInputFiles(write('produtos.csv', PRODUCTS_CSV));
  await page.waitForSelector('text=Importar 9 produto');
  await shot('retaguarda-2-importar');
  await page.click('button.primary:has-text("Importar")');
  await page.waitForTimeout(1500);

  // Entrada: transferência da loja antiga por planilha (uma linha com código desconhecido).
  await go('/retaguarda/entradas/nova');
  await field('Origem').selectOption('transfer');
  await field('Documento').fill('Carga 1 - loja antiga');
  await page.locator('input[type=file]').setInputFiles(write('transferencia.csv', TRANSFER_CSV));
  await page.waitForTimeout(1200);
  await shot('retaguarda-3-entrada');
  await page.click('button.primary:has-text("Lançar entrada")');
  await page.waitForTimeout(1500);

  // Um dia de vendas nos 4 caixas, pelo mesmo sync que os caixas usam.
  const products = await (await page.request.get(base + '/backoffice/products')).json();
  const byName = n => products.find(p => p.name === n);
  const sold = await simulateDay(page, products);
  const received = { 'Arroz Tipo 1 5kg': 420, 'Feijão Carioca 1kg': 480, 'Banana Prata': 190 };
  const expected = name => received[name] - (sold.get(byName(name).id) ?? 0);

  await go('/retaguarda/estoque');
  await shot('retaguarda-4-estoque');
  await go('/retaguarda/estoque/' + byName('Arroz Tipo 1 5kg').id);
  await shot('retaguarda-5-historico');

  // Inventário de um setor, na revisão das diferenças.
  await go('/retaguarda/inventarios');
  await page.fill('input[placeholder^="Nome"]', 'Mercearia corredor 3');
  await page.click('button:has-text("Começar contagem")');
  await page.waitForURL('**/retaguarda/inventarios/*');
  await page.waitForTimeout(1200);
  const fmt = q => String(Math.round(q * 1000) / 1000).replace('.', ',');
  for (const [code, qty] of [['7891000100103', fmt(expected('Arroz Tipo 1 5kg') - 2)],
                             ['7891000200209', fmt(expected('Feijão Carioca 1kg') + 1)],
                             ['1001', fmt(expected('Banana Prata') - 0.7)]]) {
    await page.fill('input[placeholder="Código de barras ou PLU"]', code);
    await page.fill('input[aria-label="Quantidade"]', qty);
    await page.click('button:has-text("Somar")');
    await page.waitForTimeout(500);
  }
  await page.click('button:has-text("Revisar diferenças")');
  await page.waitForTimeout(1000);
  await shot('retaguarda-6-inventario');

  // Nota XML do fornecedor: dois itens pelo código de barras, um produto novo cadastrado pela nota, sacola ignorada.
  await go('/retaguarda/notas');
  await page.locator('input[type=file]').setInputFiles(path.join(root, 'tests/Synthia.Server.Tests/Fixtures/nfe-exemplo.xml'));
  await page.waitForTimeout(1500);
  await page.locator('button:has-text("cadastrar produto novo")').first().click();
  await page.fill('input[placeholder="Preço de venda"]', '24,90');
  await page.click('button:has-text("Cadastrar e vincular")');
  await page.waitForTimeout(1000);
  const ignore = page.locator('table tbody tr').last().locator('input[type=checkbox]');
  if (!(await ignore.isChecked())) await ignore.check();
  await page.waitForTimeout(500);
  await shot('retaguarda-7-nota-xml');

  // Perdas: quebra no recebimento.
  await go('/retaguarda/ajustes');
  await field('Motivo').selectOption('damage');
  await field('Observação').fill('caixa amassada no recebimento');
  for (const [code, qty] of [['7891000300305', '3'], ['7891000500507', '1']]) {
    await page.fill('input[placeholder="Código de barras ou PLU"]', code);
    await page.fill('input[aria-label="Quantidade"]', qty);
    await page.click('button:has-text("Adicionar")');
    await page.waitForTimeout(400);
  }
  await shot('retaguarda-8-ajustes');

  // Usuários: gerente e estoquista.
  await go('/retaguarda/usuarios');
  for (const [login, name, role] of [['carla', 'Carla Mendes', 'gerente'], ['joao', 'João Pereira', 'estoquista']]) {
    await field('Usuário').fill(login);
    await field('Nome').fill(name);
    await field('Papel').selectOption(role);
    await field('Senha inicial').fill('trocar123');
    await page.click('button:has-text("Criar usuário")');
    await page.waitForTimeout(800);
  }
  await shot('retaguarda-9-usuarios');

  await go('/retaguarda/nfce');
  await shot('retaguarda-10-nfce');
  await go('/retaguarda/relatorios');
  await shot('retaguarda-11-vendas', true);
  await go('/retaguarda/fechamentos');
  await shot('retaguarda-12-fechamentos');
  await go('/retaguarda');
  await shot('retaguarda-1-painel', true);

  await browser.close();
  fs.rmSync(tmp, { recursive: true, force: true });
})().catch(e => { console.error(e); process.exit(1); });

/// Vendas de 8h às 20h com mais movimento no almoço e no fim da tarde; turnos com sangria e fechamento.
async function simulateDay(page, products) {
  const unitPrice = p => p.priceCents;
  const send = async (terminal, events) => {
    const res = await page.request.post(base + '/sync/events', {
      headers: { 'X-Terminal-Id': terminal, 'X-Terminal-Key': TERMINAL_KEYS[terminal] },
      data: { events },
    });
    if (!res.ok()) throw new Error(`sync ${terminal}: ${res.status()} ${await res.text()}`);
  };
  const ev = (terminal, type, streamId, seq, when, payload) =>
    ({ eventId: uuid7(), streamId, streamSeq: seq, type, terminalId: terminal, occurredAt: when, catalogVersion: 1, payload });

  let seed = 7;
  const rand = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  const pick = xs => xs[Math.floor(rand() * xs.length)];
  const operators = { CX01: 'Maria', CX02: 'Ana', CX03: 'Rafael', CX04: 'Bruna' };
  const sold = new Map();
  const weights = [0, 0, 0, 0, 0, 0, 0, 0, 4, 6, 8, 10, 14, 12, 7, 6, 7, 10, 14, 12, 6, 0, 0, 0];

  for (const terminal of Object.keys(operators)) {
    const shiftId = crypto.randomUUID();
    let seq = 1, cash = 0;
    await send(terminal, [ev(terminal, 'ShiftOpened', shiftId, seq++, at(7, 50), { shiftId, operator: operators[terminal], openingCashCents: 10000 })]);

    let n = 0;
    for (let hour = 8; hour <= 20; hour++) {
      for (let k = 0; k < Math.round(weights[hour] * (0.8 + rand() * 0.4)); k++) {
        const saleId = crypto.randomUUID();
        const items = Array.from({ length: 1 + Math.floor(rand() * 5) }, () => {
          const p = pick(products);
          const quantity = p.unit === 'KG' ? Math.round((0.3 + rand() * 1.5) * 1000) / 1000 : 1 + Math.floor(rand() * 3);
          return { itemId: crypto.randomUUID(), productId: p.id, description: p.name, quantity, unitPriceCents: unitPrice(p),
                   totalCents: Math.round(quantity * unitPrice(p)) };
        });
        const total = items.reduce((s, i) => s + i.totalCents, 0);
        for (const i of items) sold.set(i.productId, (sold.get(i.productId) ?? 0) + i.quantity);
        const method = pick(['cash', 'cash', 'debit', 'debit', 'credit', 'pix']);
        const paid = method === 'cash' ? Math.ceil(total / 1000) * 1000 : total;
        if (method === 'cash') cash += total;
        n++;
        const nfceStatus = terminal === 'CX03' && hour === 12 && k < 3 ? 'pending' : 'authorized';
        const when = at(hour, Math.floor(rand() * 59));
        const events = [ev(terminal, 'SaleCompleted', saleId, 1, when, {
          saleId, shiftId, items, payments: [{ method, amountCents: paid, tefNsu: method === 'cash' ? null : String(100000 + n) }],
          totalCents: total, changeCents: paid - total, nfceKey: `3526101122233300018165${terminal.slice(2)}0000${String(n).padStart(5, '0')}1`, nfceStatus,
        })];
        if (terminal === 'CX02' && n === 9) {
          events.push(ev(terminal, 'SaleItemCancelled', saleId, 2, when, { saleId, itemId: items[0].itemId, reason: 'cliente desistiu', refundMethod: method }));
          if (method === 'cash') cash -= items[0].totalCents;
        }
        if (terminal === 'CX04' && n === 15 && method === 'cash') cash -= total;
        if (terminal === 'CX04' && n === 15) events.push(ev(terminal, 'SaleCancelled', saleId, 2, when, { saleId, reason: 'venda lançada errada' }));
        if (terminal === 'CX01' && n === 20) events.push(ev(terminal, 'NfceRejected', saleId, 2, when, { saleId, nfceKey: events[0].payload.nfceKey, code: '778', reason: 'NCM inexistente' }));
        await send(terminal, events);
      }
      if (hour === 13 && cash > 30000) {
        await send(terminal, [ev(terminal, 'CashWithdrawal', shiftId, seq++, at(13, 30), { shiftId, amountCents: 20000, reason: 'cofre' })]);
        cash -= 20000;
      }
    }

    // CX01 e CX02 fecham o turno (um com falta de R$ 0,10); CX03 e CX04 seguem abertos.
    if (terminal === 'CX01' || terminal === 'CX02') {
      const declared = 10000 + cash - (terminal === 'CX01' ? 10 : 0);
      await send(terminal, [ev(terminal, 'ShiftClosed', shiftId, seq++, at(20, 55), { shiftId, declaredCashCents: declared })]);
    }
  }
  return sold;
}
