-- Projeções derivadas de events. Podem ser apagadas e reconstruídas relendo o log.

-- Estoque é ledger de movimentos, nunca um número editado.
-- kind: sale (negativo), item_cancelled / sale_cancelled (estorno positivo), count (contagem absoluta).
CREATE TABLE stock_movements (
    id          bigserial   PRIMARY KEY,
    event_id    uuid        NOT NULL REFERENCES events (event_id),
    product_id  uuid        NOT NULL,
    item_id     uuid        NULL,
    kind        text        NOT NULL,
    quantity    numeric(14, 3) NOT NULL,
    occurred_at timestamptz NOT NULL
);

CREATE INDEX ix_stock_movements_product ON stock_movements (product_id, occurred_at);

-- Saldo = última contagem + movimentos posteriores ao timestamp dela.
CREATE VIEW stock_balances AS
WITH last_count AS (
    SELECT DISTINCT ON (product_id) product_id, quantity, occurred_at
    FROM stock_movements
    WHERE kind = 'count'
    ORDER BY product_id, occurred_at DESC, id DESC
)
SELECT m.product_id,
       coalesce(lc.quantity, 0)
         + coalesce(sum(m.quantity) FILTER (
               WHERE m.kind <> 'count' AND (lc.occurred_at IS NULL OR m.occurred_at > lc.occurred_at)), 0)
         AS quantity
FROM stock_movements m
LEFT JOIN last_count lc ON lc.product_id = m.product_id
GROUP BY m.product_id, lc.quantity, lc.occurred_at;

CREATE TABLE shift_totals (
    shift_id          uuid        PRIMARY KEY,
    terminal_id       text        NOT NULL,
    operator          text        NULL,
    opened_at         timestamptz NULL,
    opening_cash_cents bigint     NOT NULL DEFAULT 0,
    sales_count       int         NOT NULL DEFAULT 0,
    sales_total_cents bigint      NOT NULL DEFAULT 0,
    cash_sales_cents  bigint      NOT NULL DEFAULT 0,
    withdrawals_cents bigint      NOT NULL DEFAULT 0,
    declared_cash_cents bigint    NULL,
    closed_at         timestamptz NULL
);

-- Fila de NFC-e que pede atenção: contingência a transmitir e rejeições a corrigir.
CREATE TABLE nfce_queue (
    sale_id     uuid        PRIMARY KEY,
    terminal_id text        NOT NULL,
    nfce_key    text        NULL,
    status      text        NOT NULL,
    reason      text        NULL,
    updated_at  timestamptz NOT NULL
);
