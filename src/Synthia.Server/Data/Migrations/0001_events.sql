-- Log de eventos imutáveis vindos dos caixas. event_id é a chave de idempotência:
-- INSERT ... ON CONFLICT DO NOTHING torna o reenvio inofensivo.
CREATE TABLE events (
    event_id        uuid        PRIMARY KEY,
    stream_id       uuid        NOT NULL,
    stream_seq      int         NOT NULL,
    type            text        NOT NULL,
    terminal_id     text        NOT NULL,
    occurred_at     timestamptz NOT NULL,
    catalog_version bigint      NOT NULL,
    payload         jsonb       NOT NULL,
    received_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_events_stream ON events (stream_id, stream_seq);
CREATE INDEX ix_events_type_occurred ON events (type, occurred_at);
