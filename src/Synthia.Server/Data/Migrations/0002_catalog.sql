-- Back-office é o dono do catálogo. Toda alteração grava uma linha em catalog_changes;
-- version é o contador global que os caixas usam para puxar só o delta.
CREATE TABLE products (
    id          uuid    PRIMARY KEY,
    name        text    NOT NULL,
    barcode     text    NULL,
    plu         int     NULL,
    unit        text    NOT NULL,
    price_cents bigint  NOT NULL,
    ncm         text    NOT NULL,
    active      boolean NOT NULL DEFAULT true,
    version     bigint  NOT NULL
);

CREATE UNIQUE INDEX ux_products_barcode ON products (barcode) WHERE barcode IS NOT NULL AND active;
CREATE UNIQUE INDEX ux_products_plu ON products (plu) WHERE plu IS NOT NULL AND active;

CREATE TABLE scheduled_prices (
    id           uuid        PRIMARY KEY,
    product_id   uuid        NOT NULL REFERENCES products (id),
    price_cents  bigint      NOT NULL,
    effective_at timestamptz NOT NULL,
    version      bigint      NOT NULL
);

CREATE TABLE catalog_changes (
    version    bigserial   PRIMARY KEY,
    entity     text        NOT NULL,
    entity_id  uuid        NOT NULL,
    data       jsonb       NOT NULL,
    changed_at timestamptz NOT NULL DEFAULT now()
);
