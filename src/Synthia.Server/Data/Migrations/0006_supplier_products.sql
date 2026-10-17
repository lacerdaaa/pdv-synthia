-- Vínculo entre o código do produto no fornecedor e o nosso produto, aprendido na importação do XML
-- da NF-e. factor = quantas unidades nossas vêm em 1 unidade comercial da nota (ex.: CX com 12 = 12).
CREATE TABLE supplier_products (
    supplier_cnpj text           NOT NULL,
    supplier_code text           NOT NULL,
    product_id    uuid           NOT NULL REFERENCES products (id),
    factor        numeric(14, 4) NOT NULL CHECK (factor > 0),
    updated_at    timestamptz    NOT NULL DEFAULT now(),
    PRIMARY KEY (supplier_cnpj, supplier_code)
);
