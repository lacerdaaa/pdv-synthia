-- Dados da loja, editados na retaguarda: identidade, rodapé do cupom e marca (logo e cores).
-- Uma linha por servidor. store_id identifica esta loja quando a nuvem atender várias.
CREATE TABLE store_profile (
    singleton          boolean PRIMARY KEY DEFAULT true CHECK (singleton),
    store_id           uuid NOT NULL DEFAULT gen_random_uuid(),
    trade_name         text NOT NULL DEFAULT 'Minha Loja',
    legal_name         text NOT NULL DEFAULT '',
    cnpj               text NOT NULL DEFAULT '',
    state_registration text NOT NULL DEFAULT '',
    address            text NOT NULL DEFAULT '',
    receipt_footer     text NOT NULL DEFAULT '',
    primary_color      text NOT NULL DEFAULT '#2B4C7E',
    accent_color       text NOT NULL DEFAULT '#E07A1F',
    logo               bytea,
    logo_content_type  text,
    version            bigint NOT NULL DEFAULT 1,
    updated_at         timestamptz NOT NULL DEFAULT now()
);

INSERT INTO store_profile DEFAULT VALUES;
