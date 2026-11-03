-- Vendas em colunas para os relatórios. Ler o JSON das vendas direto do log custava ~23 s por ano de loja;
-- estas tabelas são preenchidas no sync e reconstruídas com as outras projeções.
CREATE TABLE sales (
    sale_id      uuid        PRIMARY KEY,
    terminal_id  text        NOT NULL,
    occurred_at  timestamptz NOT NULL,
    total_cents  bigint      NOT NULL,
    change_cents bigint      NOT NULL,
    cancelled    boolean     NOT NULL DEFAULT false
);
CREATE INDEX ix_sales_occurred ON sales (occurred_at);

-- refund_method: por onde o valor do item devolvido voltou ao cliente.
CREATE TABLE sale_items (
    sale_id       uuid           NOT NULL,
    item_id       uuid           NOT NULL,
    product_id    uuid           NOT NULL,
    description   text           NOT NULL,
    quantity      numeric(14, 3) NOT NULL,
    total_cents   bigint         NOT NULL,
    refunded      boolean        NOT NULL DEFAULT false,
    refund_method text           NULL,
    PRIMARY KEY (sale_id, item_id)
);
CREATE INDEX ix_sale_items_refunded ON sale_items (sale_id) WHERE refunded;

CREATE TABLE sale_payments (
    sale_id      uuid   NOT NULL,
    seq          int    NOT NULL,
    method       text   NOT NULL,
    amount_cents bigint NOT NULL,
    PRIMARY KEY (sale_id, seq)
);

-- Carga inicial a partir do log.
INSERT INTO sales (sale_id, terminal_id, occurred_at, total_cents, change_cents)
SELECT (payload->>'saleId')::uuid, terminal_id, occurred_at, (payload->>'totalCents')::bigint, coalesce((payload->>'changeCents')::bigint, 0)
FROM events WHERE type = 'SaleCompleted'
ON CONFLICT DO NOTHING;

INSERT INTO sale_items (sale_id, item_id, product_id, description, quantity, total_cents)
SELECT (e.payload->>'saleId')::uuid, (i->>'itemId')::uuid, (i->>'productId')::uuid, i->>'description',
       (i->>'quantity')::numeric, (i->>'totalCents')::bigint
FROM events e CROSS JOIN LATERAL jsonb_array_elements(e.payload->'items') i
WHERE e.type = 'SaleCompleted'
ON CONFLICT DO NOTHING;

INSERT INTO sale_payments (sale_id, seq, method, amount_cents)
SELECT (e.payload->>'saleId')::uuid, p.n::int, p.v->>'method', (p.v->>'amountCents')::bigint
FROM events e CROSS JOIN LATERAL jsonb_array_elements(e.payload->'payments') WITH ORDINALITY AS p(v, n)
WHERE e.type = 'SaleCompleted'
ON CONFLICT DO NOTHING;

UPDATE sales s SET cancelled = true
FROM events e WHERE e.type = 'SaleCancelled' AND (e.payload->>'saleId')::uuid = s.sale_id;

UPDATE sale_items i
SET refunded = true,
    refund_method = coalesce(e.payload->>'refundMethod',
                             (SELECT method FROM sale_payments p WHERE p.sale_id = i.sale_id ORDER BY seq LIMIT 1))
FROM events e
WHERE e.type = 'SaleItemCancelled' AND (e.payload->>'saleId')::uuid = i.sale_id AND (e.payload->>'itemId')::uuid = i.item_id;
