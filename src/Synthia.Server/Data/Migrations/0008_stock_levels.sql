-- Saldo por produto mantido a cada evento, em vez de somar o ledger inteiro a cada consulta.
-- Mesma regra de antes: saldo = última contagem + movimentos posteriores a ela.
-- O ledger (stock_movements) continua sendo a verdade; esta tabela é reconstruída com as projeções.
CREATE TABLE stock_levels (
    product_id       uuid           PRIMARY KEY,
    count_quantity   numeric(14, 3) NOT NULL DEFAULT 0,
    count_at         timestamptz    NULL,
    delta            numeric(14, 3) NOT NULL DEFAULT 0,
    last_movement_at timestamptz    NULL
);

-- Devolução/cancelamento procura o item vendido; contagem procura a última contagem do produto.
CREATE INDEX ix_stock_movements_item ON stock_movements (item_id) WHERE item_id IS NOT NULL;
CREATE INDEX ix_stock_movements_count ON stock_movements (product_id, occurred_at DESC, id DESC) WHERE kind = 'count';

INSERT INTO stock_levels (product_id, count_quantity, count_at, delta, last_movement_at)
SELECT m.product_id, coalesce(lc.quantity, 0), lc.occurred_at,
       coalesce(sum(m.quantity) FILTER (
           WHERE m.kind <> 'count' AND (lc.occurred_at IS NULL OR m.occurred_at > lc.occurred_at)), 0),
       max(m.occurred_at)
FROM stock_movements m
LEFT JOIN (
    SELECT DISTINCT ON (product_id) product_id, quantity, occurred_at
    FROM stock_movements WHERE kind = 'count'
    ORDER BY product_id, occurred_at DESC, id DESC
) lc ON lc.product_id = m.product_id
GROUP BY m.product_id, lc.quantity, lc.occurred_at;

DROP VIEW stock_balances;
CREATE VIEW stock_balances AS
SELECT product_id, count_quantity + delta AS quantity, last_movement_at FROM stock_levels;
