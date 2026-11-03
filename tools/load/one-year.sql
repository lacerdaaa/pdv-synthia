-- Um ano de loja para teste de volume: 8 mil produtos, 4 caixas, ~1.200 cupons/dia, ~10 itens por cupom
-- (~450 mil eventos, 4,4 milhões de movimentos). Grava direto no log e no ledger, como o sync gravaria.
--
-- Uso: suba o servidor uma vez num banco vazio (para migrar), pare e rode
--   psql -d synthia_carga -v ON_ERROR_STOP=1 -f tools/load/one-year.sql
-- e depois chame POST /backoffice/projections/rebuild para preencher saldos e vendas a partir do log.
\timing on
SET synchronous_commit = off;

INSERT INTO products (id, name, barcode, plu, unit, price_cents, ncm, active, version)
SELECT gen_random_uuid(), 'Produto ' || lpad(g::text, 5, '0'), (7890000000000 + g)::text, NULL, 'UN',
       199 + (g * 37) % 4800, '00000000', true, g
FROM generate_series(1, 8000) g;

CREATE TEMP TABLE prod AS SELECT row_number() OVER (ORDER BY id) AS n, id, name, price_cents FROM products;
CREATE INDEX ON prod (n);

-- Vendas: dia, caixa e horário entre 8h e 21h (BRT = UTC-3).
CREATE TEMP TABLE sale AS
SELECT gen_random_uuid() AS sale_id,
       ('CX0' || (1 + s % 4)) AS terminal_id,
       timestamptz '2025-10-18 11:00+00' + d * interval '1 day' + (random() * 13 * 3600) * interval '1 second' AS at,
       md5(d::text || (s % 4)::text)::uuid AS shift_id,
       1 + (random() * 18)::int AS n_items
FROM generate_series(0, 364) d, generate_series(1, 1200) s;

CREATE TEMP TABLE item AS
SELECT s.sale_id, gen_random_uuid() AS item_id, p.id AS product_id, p.name, p.price_cents,
       (1 + (random() * 2)::int)::numeric AS qty
FROM (SELECT s.sale_id, 1 + (random() * 7999)::int AS pn FROM sale s, generate_series(1, s.n_items) i) s
JOIN prod p ON p.n = s.pn;

INSERT INTO events (event_id, stream_id, stream_seq, type, terminal_id, occurred_at, catalog_version, payload, received_at)
SELECT s.sale_id, s.sale_id, 1, 'SaleCompleted', s.terminal_id, s.at, 1,
       jsonb_build_object(
         'saleId', s.sale_id, 'shiftId', s.shift_id,
         'items', i.items,
         'payments', jsonb_build_array(jsonb_build_object('method', (ARRAY['cash','debit','credit','pix'])[1 + (abs(hashtext(s.sale_id::text)) % 4)], 'amountCents', i.total, 'tefNsu', null)),
         'totalCents', i.total, 'changeCents', 0, 'nfceKey', null, 'nfceStatus', 'authorized'),
       s.at
FROM sale s
JOIN (SELECT sale_id,
             jsonb_agg(jsonb_build_object('itemId', item_id, 'productId', product_id, 'description', name,
                       'quantity', qty, 'unitPriceCents', price_cents, 'totalCents', (qty * price_cents)::bigint)) AS items,
             sum((qty * price_cents)::bigint) AS total
      FROM item GROUP BY sale_id) i USING (sale_id);

INSERT INTO stock_movements (event_id, product_id, item_id, kind, quantity, occurred_at)
SELECT i.sale_id, i.product_id, i.item_id, 'sale', -i.qty, s.at FROM item i JOIN sale s USING (sale_id);

-- Contagem inicial de tudo e um inventário de setor (500 produtos) por mês.
CREATE TEMP TABLE cnt AS
SELECT gen_random_uuid() AS event_id, p.id AS product_id, 500::numeric AS qty, timestamptz '2025-10-18 09:00+00' AS at FROM prod p
UNION ALL
SELECT gen_random_uuid(), p.id, (random() * 300)::int, timestamptz '2025-10-18 09:00+00' + m * interval '30 days'
FROM generate_series(1, 11) m, prod p WHERE p.n % 16 = m;

INSERT INTO events (event_id, stream_id, stream_seq, type, terminal_id, occurred_at, catalog_version, payload, received_at)
SELECT event_id, event_id, 1, 'StockCounted', 'RETAGUARDA', at, 1,
       jsonb_build_object('productId', product_id, 'countedQuantity', qty), at FROM cnt;
INSERT INTO stock_movements (event_id, product_id, item_id, kind, quantity, occurred_at)
SELECT event_id, product_id, NULL, 'count', qty, at FROM cnt;

ANALYZE;
SELECT (SELECT count(*) FROM events) AS events, (SELECT count(*) FROM stock_movements) AS movements,
       pg_size_pretty(pg_database_size(current_database())) AS size;
