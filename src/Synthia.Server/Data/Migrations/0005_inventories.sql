-- Inventários em andamento. Rascunho não é fato: fica aqui até ser confirmado, quando vira
-- StockCounted no log de eventos (com o horário em que cada item foi contado).
CREATE TABLE inventories (
    id           uuid        PRIMARY KEY,
    name         text        NOT NULL,
    full_count   boolean     NOT NULL DEFAULT false,
    status       text        NOT NULL DEFAULT 'open',   -- open | confirmed | discarded
    created_by   text        NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    closed_by    text        NULL,
    closed_at    timestamptz NULL
);

CREATE TABLE inventory_lines (
    inventory_id uuid           NOT NULL REFERENCES inventories (id) ON DELETE CASCADE,
    product_id   uuid           NOT NULL REFERENCES products (id),
    quantity     numeric(14, 3) NOT NULL,
    counted_at   timestamptz    NOT NULL,
    counted_by   text           NOT NULL,
    PRIMARY KEY (inventory_id, product_id)
);
