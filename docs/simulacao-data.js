// Gerado por tools/Synthia.Simulator. Não editar à mão.
window.SIMULACAO = {
  "generatedAt": "2026-10-17T17:22:48.8416718+00:00",
  "terminals": [
    "CX01",
    "CX02"
  ],
  "steps": [
    {
      "number": 1,
      "phase": "Abertura",
      "situation": "Gerente cadastra o mix inicial no back-office",
      "routes": [
        "PUT /backoffice/products/01a14ae3-3862-7bf8-90e3-3c7e48a22941",
        "PUT /backoffice/products/01a14ae3-3914-7707-9f92-0113b6c6d365",
        "PUT /backoffice/products/01a14ae3-391d-7233-bbd2-ef97f2839521",
        "PUT /backoffice/products/01a14ae3-3924-7ef2-be96-1bc597278736",
        "PUT /backoffice/products/01a14ae3-392c-7e08-9dc1-02ea25aa7a02",
        "PUT /backoffice/products/01a14ae3-3932-7c48-8d04-20ac4cf1b27a"
      ],
      "process": [
        "Back-office: PUT /backoffice/products/{id} para cada produto.",
        "Servidor: CatalogService.UpsertProductAsync trava pg_advisory_xact_lock (versões nunca saem fora de ordem).",
        "Servidor: INSERT em catalog_changes RETURNING version + upsert em products, na mesma transação."
      ],
      "exchanges": [
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-3862-7bf8-90e3-3c7e48a22941",
          "request": "{\n  \"id\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n  \"name\": \"Arroz 5kg\",\n  \"barcode\": \"7891000100103\",\n  \"plu\": null,\n  \"unit\": \"UN\",\n  \"priceCents\": 2899,\n  \"ncm\": \"10063021\",\n  \"active\": true\n}",
          "status": 200,
          "response": "{\n  \"version\": 1\n}",
          "note": null
        },
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-3914-7707-9f92-0113b6c6d365",
          "request": "{\n  \"id\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n  \"name\": \"Feij\\u00E3o 1kg\",\n  \"barcode\": \"7891000200209\",\n  \"plu\": null,\n  \"unit\": \"UN\",\n  \"priceCents\": 899,\n  \"ncm\": \"07133399\",\n  \"active\": true\n}",
          "status": 200,
          "response": "{\n  \"version\": 2\n}",
          "note": null
        },
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-391d-7233-bbd2-ef97f2839521",
          "request": "{\n  \"id\": \"01a14ae3-391d-7233-bbd2-ef97f2839521\",\n  \"name\": \"Banana prata\",\n  \"barcode\": null,\n  \"plu\": 1001,\n  \"unit\": \"KG\",\n  \"priceCents\": 599,\n  \"ncm\": \"08039000\",\n  \"active\": true\n}",
          "status": 200,
          "response": "{\n  \"version\": 3\n}",
          "note": null
        },
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-3924-7ef2-be96-1bc597278736",
          "request": "{\n  \"id\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n  \"name\": \"Leite 1L\",\n  \"barcode\": \"7891000300305\",\n  \"plu\": null,\n  \"unit\": \"UN\",\n  \"priceCents\": 549,\n  \"ncm\": \"04012010\",\n  \"active\": true\n}",
          "status": 200,
          "response": "{\n  \"version\": 4\n}",
          "note": null
        },
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-392c-7e08-9dc1-02ea25aa7a02",
          "request": "{\n  \"id\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n  \"name\": \"Caf\\u00E9 500g\",\n  \"barcode\": \"7891000400401\",\n  \"plu\": null,\n  \"unit\": \"UN\",\n  \"priceCents\": 1899,\n  \"ncm\": \"09012100\",\n  \"active\": true\n}",
          "status": 200,
          "response": "{\n  \"version\": 5\n}",
          "note": null
        },
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-3932-7c48-8d04-20ac4cf1b27a",
          "request": "{\n  \"id\": \"01a14ae3-3932-7c48-8d04-20ac4cf1b27a\",\n  \"name\": \"Azeite 500ml\",\n  \"barcode\": \"7891000500507\",\n  \"plu\": null,\n  \"unit\": \"UN\",\n  \"priceCents\": 3490,\n  \"ncm\": \"15091000\",\n  \"active\": true\n}",
          "status": 200,
          "response": "{\n  \"version\": 6\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "Servidor · cadastro · Arroz 5kg",
          "before": null,
          "after": "R$ 28,99"
        },
        {
          "key": "Servidor · cadastro · Azeite 500ml",
          "before": null,
          "after": "R$ 34,90"
        },
        {
          "key": "Servidor · cadastro · Banana prata",
          "before": null,
          "after": "R$ 5,99"
        },
        {
          "key": "Servidor · cadastro · Café 500g",
          "before": null,
          "after": "R$ 18,99"
        },
        {
          "key": "Servidor · cadastro · Feijão 1kg",
          "before": null,
          "after": "R$ 8,99"
        },
        {
          "key": "Servidor · cadastro · Leite 1L",
          "before": null,
          "after": "R$ 5,49"
        },
        {
          "key": "Servidor · versão do catálogo",
          "before": "0",
          "after": "6"
        }
      ],
      "notes": [
        "Arroz 5kg: R$ 28,99",
        "Feijão 1kg: R$ 8,99",
        "Banana prata: R$ 5,99",
        "Leite 1L: R$ 5,49",
        "Café 500g: R$ 18,99",
        "Azeite 500ml: R$ 34,90"
      ]
    },
    {
      "number": 2,
      "phase": "Abertura",
      "situation": "Gerente agenda promoção do café (R$ 15,99) para daqui a 2 horas",
      "routes": [
        "POST /backoffice/scheduled-prices"
      ],
      "process": [
        "Back-office: POST /backoffice/scheduled-prices com effective_at.",
        "Servidor: grava scheduled_prices + catalog_changes (entity = scheduled_price).",
        "Os caixas vão receber o agendamento antes e aplicar sozinhos no horário, mesmo offline."
      ],
      "exchanges": [
        {
          "actor": "Back-office",
          "method": "POST",
          "path": "/backoffice/scheduled-prices",
          "request": "{\n  \"id\": \"01a14ae3-3950-7d27-b0dc-dcdc587c1e4a\",\n  \"productId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n  \"priceCents\": 1599,\n  \"effectiveAt\": \"2026-10-17T19:28:46.897633\\u002B00:00\"\n}",
          "status": 200,
          "response": "{\n  \"version\": 7\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "Servidor · versão do catálogo",
          "before": "6",
          "after": "7"
        }
      ],
      "notes": [
        "Café 500g: R$ 15,99 a partir de 19:28 UTC"
      ]
    },
    {
      "number": 3,
      "phase": "Abertura",
      "situation": "CX01 é ligado pela primeira vez (SQLite vazio)",
      "routes": [
        "local · SQLite do caixa",
        "GET /sync/catalog",
        "GET /sync/catalog/snapshot"
      ],
      "process": [
        "Caixa: CatalogPuller.PullAsync lê last_catalog_version = 0.",
        "Caixa: GET /sync/catalog?since=0 → servidor responde snapshotRequired = true (caixa novo).",
        "Caixa: GET /sync/catalog/snapshot → LocalCatalog.ApplySnapshot substitui products e scheduled_prices numa transação."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "GET",
          "path": "/sync/catalog?since=0",
          "request": null,
          "status": 200,
          "response": "{\n  \"since\": 0,\n  \"latestVersion\": 7,\n  \"snapshotRequired\": true,\n  \"changes\": []\n}",
          "note": null
        },
        {
          "actor": "CX01",
          "method": "GET",
          "path": "/sync/catalog/snapshot",
          "request": null,
          "status": 200,
          "response": "{\n  \"version\": 7,\n  \"products\": [\n    {\n      \"id\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n      \"name\": \"Arroz 5kg\",\n      \"barcode\": \"7891000100103\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 2899,\n      \"ncm\": \"10063021\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-3932-7c48-8d04-20ac4cf1b27a\",\n      \"name\": \"Azeite 500ml\",\n      \"barcode\": \"7891000500507\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 3490,\n      \"ncm\": \"15091000\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-391d-7233-bbd2-ef97f2839521\",\n      \"name\": \"Banana prata\",\n      \"barcode\": null,\n      \"plu\": 1001,\n      \"unit\": \"KG\",\n      \"priceCents\": 599,\n      \"ncm\": \"08039000\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n      \"name\": \"Caf\\u00E9 500g\",\n      \"barcode\": \"7891000400401\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 1899,\n      \"ncm\": \"09012100\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n      \"name\": \"Feij\\u00E3o 1kg\",\n      \"barcode\": \"7891000200209\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 899,\n      \"ncm\": \"07133399\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n      \"name\": \"Leite 1L\",\n      \"barcode\": \"7891000300305\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 549,\n      \"ncm\": \"04012010\",\n      \"active\": true\n    }\n  ],\n  \"scheduledPrices\": [\n    {\n      \"id\": \"01a14ae3-3950-7d27-b0dc-dcdc587c1e4a\",\n      \"productId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n      \"priceCents\": 1599,\n      \"effectiveAt\": \"2026-10-17T19:28:46.897633\\u002B00:00\"\n    }\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · versão do catálogo",
          "before": "0",
          "after": "7"
        }
      ],
      "notes": [
        "CX01: catálogo local na versão 7"
      ]
    },
    {
      "number": 4,
      "phase": "Abertura",
      "situation": "CX02 é ligado",
      "routes": [
        "GET /sync/catalog",
        "GET /sync/catalog/snapshot"
      ],
      "process": [
        "Mesmo fluxo do CX01: delta pede snapshot, caixa baixa catálogo completo."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "GET",
          "path": "/sync/catalog?since=0",
          "request": null,
          "status": 200,
          "response": "{\n  \"since\": 0,\n  \"latestVersion\": 7,\n  \"snapshotRequired\": true,\n  \"changes\": []\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "GET",
          "path": "/sync/catalog/snapshot",
          "request": null,
          "status": 200,
          "response": "{\n  \"version\": 7,\n  \"products\": [\n    {\n      \"id\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n      \"name\": \"Arroz 5kg\",\n      \"barcode\": \"7891000100103\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 2899,\n      \"ncm\": \"10063021\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-3932-7c48-8d04-20ac4cf1b27a\",\n      \"name\": \"Azeite 500ml\",\n      \"barcode\": \"7891000500507\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 3490,\n      \"ncm\": \"15091000\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-391d-7233-bbd2-ef97f2839521\",\n      \"name\": \"Banana prata\",\n      \"barcode\": null,\n      \"plu\": 1001,\n      \"unit\": \"KG\",\n      \"priceCents\": 599,\n      \"ncm\": \"08039000\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n      \"name\": \"Caf\\u00E9 500g\",\n      \"barcode\": \"7891000400401\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 1899,\n      \"ncm\": \"09012100\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n      \"name\": \"Feij\\u00E3o 1kg\",\n      \"barcode\": \"7891000200209\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 899,\n      \"ncm\": \"07133399\",\n      \"active\": true\n    },\n    {\n      \"id\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n      \"name\": \"Leite 1L\",\n      \"barcode\": \"7891000300305\",\n      \"plu\": null,\n      \"unit\": \"UN\",\n      \"priceCents\": 549,\n      \"ncm\": \"04012010\",\n      \"active\": true\n    }\n  ],\n  \"scheduledPrices\": [\n    {\n      \"id\": \"01a14ae3-3950-7d27-b0dc-dcdc587c1e4a\",\n      \"productId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n      \"priceCents\": 1599,\n      \"effectiveAt\": \"2026-10-17T19:28:46.897633\\u002B00:00\"\n    }\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX02 · versão do catálogo",
          "before": "0",
          "after": "7"
        }
      ],
      "notes": [
        "CX02: catálogo local na versão 7"
      ]
    },
    {
      "number": 5,
      "phase": "Abertura",
      "situation": "Conferência de estoque antes de abrir (contagem física)",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: grava um StockCounted por produto na outbox (a tela de inventário ainda não existe; o evento é o mesmo).",
        "Caixa: OutboxPusher envia o lote → POST /sync/events.",
        "Servidor: EventStore insere com ON CONFLICT DO NOTHING; Projector grava movimento kind = count.",
        "Saldo = última contagem + movimentos posteriores a ela (view stock_balances)."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14af0-f191-798e-96ed-d758681ecf78\",\n      \"streamId\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n      \"streamSeq\": 1,\n      \"type\": \"StockCounted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:37:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"productId\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n        \"countedQuantity\": 50\n      }\n    },\n    {\n      \"eventId\": \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e4\",\n      \"streamId\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n      \"streamSeq\": 1,\n      \"type\": \"StockCounted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:37:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"productId\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n        \"countedQuantity\": 40\n      }\n    },\n    {\n      \"eventId\": \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e5\",\n      \"streamId\": \"01a14ae3-391d-7233-bbd2-ef97f2839521\",\n      \"streamSeq\": 1,\n      \"type\": \"StockCounted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:37:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"productId\": \"01a14ae3-391d-7233-bbd2-ef97f2839521\",\n        \"countedQuantity\": 30\n      }\n    },\n    {\n      \"eventId\": \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e6\",\n      \"streamId\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n      \"streamSeq\": 1,\n      \"type\": \"StockCounted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:37:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"productId\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n        \"countedQuantity\": 24\n      }\n    },\n    {\n      \"eventId\": \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e7\",\n      \"streamId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n      \"streamSeq\": 1,\n      \"type\": \"StockCounted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:37:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"productId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n        \"countedQuantity\": 20\n      }\n    },\n    {\n      \"eventId\": \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e8\",\n      \"streamId\": \"01a14ae3-3932-7c48-8d04-20ac4cf1b27a\",\n      \"streamSeq\": 1,\n      \"type\": \"StockCounted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:37:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"productId\": \"01a14ae3-3932-7c48-8d04-20ac4cf1b27a\",\n        \"countedQuantity\": 1\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14af0-f191-798e-96ed-d758681ecf78\",\n    \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e4\",\n    \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e5\",\n    \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e6\",\n    \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e7\",\n    \"01a14af0-f191-7f1b-a8d5-7ee88a7b21e8\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "0",
          "after": "6"
        },
        {
          "key": "Servidor · estoque · Arroz 5kg",
          "before": null,
          "after": "50"
        },
        {
          "key": "Servidor · estoque · Azeite 500ml",
          "before": null,
          "after": "1"
        },
        {
          "key": "Servidor · estoque · Banana prata",
          "before": null,
          "after": "30"
        },
        {
          "key": "Servidor · estoque · Café 500g",
          "before": null,
          "after": "20"
        },
        {
          "key": "Servidor · estoque · Feijão 1kg",
          "before": null,
          "after": "40"
        },
        {
          "key": "Servidor · estoque · Leite 1L",
          "before": null,
          "after": "24"
        },
        {
          "key": "Servidor · events gravados",
          "before": "0",
          "after": "6"
        }
      ],
      "notes": [
        "CX01: evento StockCounted gravado na outbox (event_id 01a14af0-f191…, seq 1) · Arroz 5kg = 50",
        "CX01: evento StockCounted gravado na outbox (event_id 01a14af0-f191…, seq 1) · Feijão 1kg = 40",
        "CX01: evento StockCounted gravado na outbox (event_id 01a14af0-f191…, seq 1) · Banana prata = 30",
        "CX01: evento StockCounted gravado na outbox (event_id 01a14af0-f191…, seq 1) · Leite 1L = 24",
        "CX01: evento StockCounted gravado na outbox (event_id 01a14af0-f191…, seq 1) · Café 500g = 20",
        "CX01: evento StockCounted gravado na outbox (event_id 01a14af0-f191…, seq 1) · Azeite 500ml = 1",
        "CX01: worker enviou 6 evento(s)"
      ]
    },
    {
      "number": 6,
      "phase": "Abertura",
      "situation": "Maria abre o turno no CX01 com R$ 100,00 de fundo de troco",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: ShiftService.Open grava ShiftOpened na outbox e open_shift_id em meta, na mesma transação SQLite.",
        "Caixa: IOutboxSignal.Notify acorda o worker, que envia logo (POST /sync/events).",
        "Servidor: Projector cria a linha do turno em shift_totals."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14af3-b0b1-718f-a817-5708c0e3a238\",\n      \"streamId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n      \"streamSeq\": 1,\n      \"type\": \"ShiftOpened\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:40:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"operator\": \"Maria\",\n        \"openingCashCents\": 10000\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14af3-b0b1-718f-a817-5708c0e3a238\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "6",
          "after": "7"
        },
        {
          "key": "CX01 · turno aberto",
          "before": "não",
          "after": "sim"
        },
        {
          "key": "Servidor · events gravados",
          "before": "6",
          "after": "7"
        },
        {
          "key": "Servidor · turno CX01 · dinheiro esperado na gaveta",
          "before": null,
          "after": "R$ 100,00"
        },
        {
          "key": "Servidor · turno CX01 · sangrias",
          "before": null,
          "after": "R$ 0,00"
        },
        {
          "key": "Servidor · turno CX01 · status",
          "before": null,
          "after": "aberto"
        },
        {
          "key": "Servidor · turno CX01 · vendas",
          "before": null,
          "after": "0 · R$ 0,00"
        }
      ],
      "notes": [
        "CX01: turno aberto",
        "CX01: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 7,
      "phase": "Abertura",
      "situation": "João abre o turno no CX02 com R$ 100,00",
      "routes": [
        "POST /sync/events"
      ],
      "process": [
        "Mesmo fluxo: ShiftOpened → outbox → POST /sync/events → shift_totals."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14af6-6fd1-71fc-a5db-381b7ca7e42b\",\n      \"streamId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n      \"streamSeq\": 1,\n      \"type\": \"ShiftOpened\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T17:43:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"operator\": \"Jo\\u00E3o\",\n        \"openingCashCents\": 10000\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14af6-6fd1-71fc-a5db-381b7ca7e42b\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "0",
          "after": "1"
        },
        {
          "key": "CX02 · turno aberto",
          "before": "não",
          "after": "sim"
        },
        {
          "key": "Servidor · events gravados",
          "before": "7",
          "after": "8"
        },
        {
          "key": "Servidor · turno CX02 · dinheiro esperado na gaveta",
          "before": null,
          "after": "R$ 100,00"
        },
        {
          "key": "Servidor · turno CX02 · sangrias",
          "before": null,
          "after": "R$ 0,00"
        },
        {
          "key": "Servidor · turno CX02 · status",
          "before": null,
          "after": "aberto"
        },
        {
          "key": "Servidor · turno CX02 · vendas",
          "before": null,
          "after": "0 · R$ 0,00"
        }
      ],
      "notes": [
        "CX02: turno aberto",
        "CX02: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 8,
      "phase": "Vendas",
      "situation": "CX01: cliente leva 2 arroz e 1 feijão, paga R$ 70,00 em dinheiro",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: leitor bipa o código → LocalCatalog.FindByBarcode (preço vigente calculado no SQLite).",
        "Caixa: Cart congela preço por item; SaleService valida pagamento e troco (só de dinheiro).",
        "Caixa: INfceEmitter emite a NFC-e (simulada aqui; será a ACBr) → chave e status vão no evento.",
        "Caixa: SaleCompleted gravado na outbox (stream = venda, seq 1); worker envia.",
        "Servidor: Projector baixa estoque (movimentos kind = sale) e soma no turno."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14af9-2ef1-78e3-9e59-da80b19f5bda\",\n      \"streamId\": \"01a14ae3-3af5-789a-a919-69b8da0ee676\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:46:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3af5-789a-a919-69b8da0ee676\",\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3af1-768c-8e36-4a2468ebd4af\",\n            \"productId\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n            \"description\": \"Arroz 5kg\",\n            \"quantity\": 2,\n            \"unitPriceCents\": 2899,\n            \"totalCents\": 5798\n          },\n          {\n            \"itemId\": \"01a14ae3-3af2-7a01-a3bd-cefb54f4a32d\",\n            \"productId\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n            \"description\": \"Feij\\u00E3o 1kg\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 899,\n            \"totalCents\": 899\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 7000,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 6697,\n        \"changeCents\": 303,\n        \"nfceKey\": \"3526100000000000000065001000000001100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14af9-2ef1-78e3-9e59-da80b19f5bda\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "7",
          "after": "8"
        },
        {
          "key": "Servidor · estoque · Arroz 5kg",
          "before": "50",
          "after": "48"
        },
        {
          "key": "Servidor · estoque · Feijão 1kg",
          "before": "40",
          "after": "39"
        },
        {
          "key": "Servidor · events gravados",
          "before": "8",
          "after": "9"
        },
        {
          "key": "Servidor · turno CX01 · dinheiro esperado na gaveta",
          "before": "R$ 100,00",
          "after": "R$ 166,97"
        },
        {
          "key": "Servidor · turno CX01 · vendas",
          "before": "0 · R$ 0,00",
          "after": "1 · R$ 66,97"
        }
      ],
      "notes": [
        "CX01: venda 0ee676 · 2 × Arroz 5kg a R$ 28,99 = R$ 57,98; 1 × Feijão 1kg a R$ 8,99 = R$ 8,99 · total R$ 66,97 · troco R$ 3,03 · NFC-e authorized",
        "CX01: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 9,
      "phase": "Vendas",
      "situation": "CX02: cliente pesa 1,235 kg de banana e paga no PIX",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: balança informa o peso; LocalCatalog.FindByPlu(1001).",
        "Caixa: Cart arredonda 1,235 × R$ 5,99 = R$ 7,39765 para R$ 7,40.",
        "Caixa: SaleCompleted → outbox → POST /sync/events."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14afb-ee11-7db8-a5fb-6f06c0e5003c\",\n      \"streamId\": \"01a14ae3-3b25-7668-ab8e-b8ba0a777db6\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T17:49:46.897633\\u002B00:00\",\n      \"catalogVersion\": 7,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b25-7668-ab8e-b8ba0a777db6\",\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3b25-7297-a203-796da3e4b644\",\n            \"productId\": \"01a14ae3-391d-7233-bbd2-ef97f2839521\",\n            \"description\": \"Banana prata\",\n            \"quantity\": 1.235,\n            \"unitPriceCents\": 599,\n            \"totalCents\": 740\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"pix\",\n            \"amountCents\": 740,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 740,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000001100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14afb-ee11-7db8-a5fb-6f06c0e5003c\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "1",
          "after": "2"
        },
        {
          "key": "Servidor · estoque · Banana prata",
          "before": "30",
          "after": "28.765"
        },
        {
          "key": "Servidor · events gravados",
          "before": "9",
          "after": "10"
        },
        {
          "key": "Servidor · turno CX02 · vendas",
          "before": "0 · R$ 0,00",
          "after": "1 · R$ 7,40"
        }
      ],
      "notes": [
        "CX02: venda 777db6 · 1,235 × Banana prata a R$ 5,99 = R$ 7,40 · total R$ 7,40 · troco R$ 0,00 · NFC-e authorized",
        "CX02: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 10,
      "phase": "Vendas",
      "situation": "Gerente reajusta o feijão para R$ 9,49 no back-office",
      "routes": [
        "PUT /backoffice/products/01a14ae3-3914-7707-9f92-0113b6c6d365",
        "GET /sync/catalog"
      ],
      "process": [
        "Back-office: PUT /backoffice/products/{id} → nova versão em catalog_changes.",
        "Caixas: na rodada de 30 s, GET /sync/catalog?since=<versão local> recebe só o delta.",
        "Caixas: LocalCatalog.ApplyDelta faz upsert idempotente e avança last_catalog_version."
      ],
      "exchanges": [
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-3914-7707-9f92-0113b6c6d365",
          "request": "{\n  \"id\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n  \"name\": \"Feij\\u00E3o 1kg\",\n  \"barcode\": \"7891000200209\",\n  \"plu\": null,\n  \"unit\": \"UN\",\n  \"priceCents\": 949,\n  \"ncm\": \"07133399\",\n  \"active\": true\n}",
          "status": 200,
          "response": "{\n  \"version\": 8\n}",
          "note": null
        },
        {
          "actor": "CX01",
          "method": "GET",
          "path": "/sync/catalog?since=7",
          "request": null,
          "status": 200,
          "response": "{\n  \"since\": 7,\n  \"latestVersion\": 8,\n  \"snapshotRequired\": false,\n  \"changes\": [\n    {\n      \"version\": 8,\n      \"entity\": \"product\",\n      \"entityId\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n      \"data\": {\n        \"id\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n        \"ncm\": \"07133399\",\n        \"plu\": null,\n        \"name\": \"Feij\\u00E3o 1kg\",\n        \"unit\": \"UN\",\n        \"active\": true,\n        \"barcode\": \"7891000200209\",\n        \"priceCents\": 949\n      }\n    }\n  ]\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "GET",
          "path": "/sync/catalog?since=7",
          "request": null,
          "status": 200,
          "response": "{\n  \"since\": 7,\n  \"latestVersion\": 8,\n  \"snapshotRequired\": false,\n  \"changes\": [\n    {\n      \"version\": 8,\n      \"entity\": \"product\",\n      \"entityId\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n      \"data\": {\n        \"id\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n        \"ncm\": \"07133399\",\n        \"plu\": null,\n        \"name\": \"Feij\\u00E3o 1kg\",\n        \"unit\": \"UN\",\n        \"active\": true,\n        \"barcode\": \"7891000200209\",\n        \"priceCents\": 949\n      }\n    }\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · versão do catálogo",
          "before": "7",
          "after": "8"
        },
        {
          "key": "CX02 · versão do catálogo",
          "before": "7",
          "after": "8"
        },
        {
          "key": "Servidor · cadastro · Feijão 1kg",
          "before": "R$ 8,99",
          "after": "R$ 9,49"
        },
        {
          "key": "Servidor · versão do catálogo",
          "before": "7",
          "after": "8"
        }
      ],
      "notes": [
        "Feijão 1kg: R$ 9,49",
        "CX01: catálogo local na versão 8",
        "CX02: catálogo local na versão 8"
      ]
    },
    {
      "number": 11,
      "phase": "Vendas",
      "situation": "CX01: cliente compra 3 feijões no débito",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: preço já é o novo (R$ 9,49), lido do SQLite local.",
        "Caixa: pagamento com NSU do TEF gravado no evento.",
        "Servidor: o evento carrega catalog_version, então dá para auditar com qual tabela de preço a venda foi feita."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b01-6c51-7a7e-97e7-607e457579f5\",\n      \"streamId\": \"01a14ae3-3b61-7d3b-b374-dcb031eee4ab\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:55:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b61-7d3b-b374-dcb031eee4ab\",\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3b61-76f0-872d-a85862d96eef\",\n            \"productId\": \"01a14ae3-3914-7707-9f92-0113b6c6d365\",\n            \"description\": \"Feij\\u00E3o 1kg\",\n            \"quantity\": 3,\n            \"unitPriceCents\": 949,\n            \"totalCents\": 2847\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"debit\",\n            \"amountCents\": 2847,\n            \"tefNsu\": \"004512\"\n          }\n        ],\n        \"totalCents\": 2847,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000002100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b01-6c51-7a7e-97e7-607e457579f5\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "8",
          "after": "9"
        },
        {
          "key": "Servidor · estoque · Feijão 1kg",
          "before": "39",
          "after": "36"
        },
        {
          "key": "Servidor · events gravados",
          "before": "10",
          "after": "11"
        },
        {
          "key": "Servidor · turno CX01 · vendas",
          "before": "1 · R$ 66,97",
          "after": "2 · R$ 95,44"
        }
      ],
      "notes": [
        "CX01: venda eee4ab · 3 × Feijão 1kg a R$ 9,49 = R$ 28,47 · total R$ 28,47 · troco R$ 0,00 · NFC-e authorized",
        "CX01: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 12,
      "phase": "Servidor fora",
      "situation": "O mini PC do servidor trava. CX01 vende 1 arroz em dinheiro",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: a venda é 100% local; nada nela depende do servidor.",
        "Caixa: SaleCompleted vai para a outbox normalmente.",
        "Caixa: OutboxPusher tenta POST /sync/events e falha; o evento fica com sent_at NULL e o backoff dobra (2 s → 4 s → … → 60 s)."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b04-2b71-7f0c-b961-0d8b7a8f1e9e\",\n      \"streamId\": \"01a14ae3-3b73-78b1-a331-04c8115f33bd\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:58:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b73-78b1-a331-04c8115f33bd\",\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3b73-765b-b45a-177bfb08aa15\",\n            \"productId\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n            \"description\": \"Arroz 5kg\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 2899,\n            \"totalCents\": 2899\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 2899,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 2899,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000003100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": null,
          "response": null,
          "note": "sem conexão com o servidor"
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "9",
          "after": "10"
        },
        {
          "key": "CX01 · outbox pendente",
          "before": "0",
          "after": "1"
        }
      ],
      "notes": [
        "CX01: venda 5f33bd · 1 × Arroz 5kg a R$ 28,99 = R$ 28,99 · total R$ 28,99 · troco R$ 0,00 · NFC-e authorized",
        "CX01: envio falhou (sem conexão com o servidor (simulado)); eventos continuam na outbox, próxima tentativa com backoff"
      ]
    },
    {
      "number": 13,
      "phase": "Servidor fora",
      "situation": "CX02 vende 2 cafés no crédito e tenta atualizar o catálogo",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events",
        "GET /sync/catalog"
      ],
      "process": [
        "Caixa: venda local; push falha e o evento acumula.",
        "Caixa: CatalogPuller falha; o catálogo congela na última versão (o caixa segue vendendo com ela).",
        "Tela do caixa: indicador de eventos pendentes de envio sobe (o gerente vê se passar de algumas centenas)."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b06-ea91-73f2-b28d-fb34b95c7a60\",\n      \"streamId\": \"01a14ae3-3b7f-761c-8988-7ec965ecb563\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T18:01:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b7f-761c-8988-7ec965ecb563\",\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3b7f-7508-a39e-f2fe0cae4398\",\n            \"productId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n            \"description\": \"Caf\\u00E9 500g\",\n            \"quantity\": 2,\n            \"unitPriceCents\": 1899,\n            \"totalCents\": 3798\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"credit\",\n            \"amountCents\": 3798,\n            \"tefNsu\": \"009931\"\n          }\n        ],\n        \"totalCents\": 3798,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000002100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": null,
          "response": null,
          "note": "sem conexão com o servidor"
        },
        {
          "actor": "CX02",
          "method": "GET",
          "path": "/sync/catalog?since=8",
          "request": null,
          "status": null,
          "response": null,
          "note": "sem conexão com o servidor"
        }
      ],
      "effects": [
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "2",
          "after": "3"
        },
        {
          "key": "CX02 · outbox pendente",
          "before": "0",
          "after": "1"
        }
      ],
      "notes": [
        "CX02: venda ecb563 · 2 × Café 500g a R$ 18,99 = R$ 37,98 · total R$ 37,98 · troco R$ 0,00 · NFC-e authorized",
        "CX02: envio falhou (sem conexão com o servidor (simulado)); eventos continuam na outbox, próxima tentativa com backoff",
        "CX02: catálogo não atualizado (sem conexão com o servidor (simulado)); segue na versão 8"
      ]
    },
    {
      "number": 14,
      "phase": "Servidor fora",
      "situation": "Servidor volta, mas a resposta para o CX01 se perde na rede",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: POST /sync/events chega ao servidor, que grava o evento e projeta o estoque.",
        "Rede: a resposta 200 não chega ao caixa (timeout).",
        "Caixa: sem ack, não marca sent_at; o evento continua pendente para reenvio."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b04-2b71-7f0c-b961-0d8b7a8f1e9e\",\n      \"streamId\": \"01a14ae3-3b73-78b1-a331-04c8115f33bd\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:58:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b73-78b1-a331-04c8115f33bd\",\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3b73-765b-b45a-177bfb08aa15\",\n            \"productId\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n            \"description\": \"Arroz 5kg\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 2899,\n            \"totalCents\": 2899\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 2899,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 2899,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000003100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b04-2b71-7f0c-b961-0d8b7a8f1e9e\"\n  ]\n}",
          "note": "o servidor processou, mas a resposta se perdeu no caminho"
        }
      ],
      "effects": [
        {
          "key": "Servidor · estoque · Arroz 5kg",
          "before": "48",
          "after": "47"
        },
        {
          "key": "Servidor · events gravados",
          "before": "11",
          "after": "12"
        },
        {
          "key": "Servidor · turno CX01 · dinheiro esperado na gaveta",
          "before": "R$ 166,97",
          "after": "R$ 195,96"
        },
        {
          "key": "Servidor · turno CX01 · vendas",
          "before": "2 · R$ 95,44",
          "after": "3 · R$ 124,43"
        }
      ],
      "notes": [
        "CX01: envio falhou (resposta perdida (simulado)); eventos continuam na outbox, próxima tentativa com backoff"
      ]
    },
    {
      "number": 15,
      "phase": "Servidor fora",
      "situation": "Workers tentam de novo: CX01 reenvia, CX02 drena o que acumulou",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events",
        "GET /sync/catalog"
      ],
      "process": [
        "Caixa: reenvia o mesmo event_id (entrega pelo menos uma vez).",
        "Servidor: INSERT … ON CONFLICT (event_id) DO NOTHING → 0 linhas, projeção não roda de novo, mas o id volta em accepted.",
        "Caixa: marca sent_at. Nada duplicou no estoque nem no turno."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b04-2b71-7f0c-b961-0d8b7a8f1e9e\",\n      \"streamId\": \"01a14ae3-3b73-78b1-a331-04c8115f33bd\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T17:58:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b73-78b1-a331-04c8115f33bd\",\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3b73-765b-b45a-177bfb08aa15\",\n            \"productId\": \"01a14ae3-3862-7bf8-90e3-3c7e48a22941\",\n            \"description\": \"Arroz 5kg\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 2899,\n            \"totalCents\": 2899\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 2899,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 2899,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000003100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b04-2b71-7f0c-b961-0d8b7a8f1e9e\"\n  ]\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b06-ea91-73f2-b28d-fb34b95c7a60\",\n      \"streamId\": \"01a14ae3-3b7f-761c-8988-7ec965ecb563\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T18:01:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b7f-761c-8988-7ec965ecb563\",\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3b7f-7508-a39e-f2fe0cae4398\",\n            \"productId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n            \"description\": \"Caf\\u00E9 500g\",\n            \"quantity\": 2,\n            \"unitPriceCents\": 1899,\n            \"totalCents\": 3798\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"credit\",\n            \"amountCents\": 3798,\n            \"tefNsu\": \"009931\"\n          }\n        ],\n        \"totalCents\": 3798,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000002100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b06-ea91-73f2-b28d-fb34b95c7a60\"\n  ]\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "GET",
          "path": "/sync/catalog?since=8",
          "request": null,
          "status": 200,
          "response": "{\n  \"since\": 8,\n  \"latestVersion\": 8,\n  \"snapshotRequired\": false,\n  \"changes\": []\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · outbox pendente",
          "before": "1",
          "after": "0"
        },
        {
          "key": "CX02 · outbox pendente",
          "before": "1",
          "after": "0"
        },
        {
          "key": "Servidor · estoque · Café 500g",
          "before": "20",
          "after": "18"
        },
        {
          "key": "Servidor · events gravados",
          "before": "12",
          "after": "13"
        },
        {
          "key": "Servidor · turno CX02 · vendas",
          "before": "1 · R$ 7,40",
          "after": "2 · R$ 45,38"
        }
      ],
      "notes": [
        "CX01: worker enviou 1 evento(s)",
        "CX02: worker enviou 1 evento(s)",
        "CX02: catálogo local na versão 8"
      ]
    },
    {
      "number": 16,
      "phase": "Internet fora",
      "situation": "Fibra e 4G caem juntas. CX02 vende 1 leite em dinheiro",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: ACBr não alcança a SEFAZ → emite em contingência offline (tpEmis 9), imprime DANFE.",
        "Caixa: SaleCompleted com nfce_status = pending. O servidor local continua acessível pela rede interna.",
        "Servidor: Projector coloca a venda em nfce_queue (pendente de transmissão)."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b0f-27f1-74f4-a16a-7543ee537e81\",\n      \"streamId\": \"01a14ae3-3ba8-7e94-ae0a-86d5ed74e6b3\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T18:10:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3ba8-7e94-ae0a-86d5ed74e6b3\",\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3ba8-73de-8b7e-3887b1c71450\",\n            \"productId\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n            \"description\": \"Leite 1L\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 549,\n            \"totalCents\": 549\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 549,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 549,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000003900000000\",\n        \"nfceStatus\": \"pending\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b0f-27f1-74f4-a16a-7543ee537e81\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "3",
          "after": "4"
        },
        {
          "key": "Servidor · estoque · Leite 1L",
          "before": "24",
          "after": "23"
        },
        {
          "key": "Servidor · events gravados",
          "before": "13",
          "after": "14"
        },
        {
          "key": "Servidor · fila NFC-e · CX02",
          "before": null,
          "after": "pending"
        },
        {
          "key": "Servidor · turno CX02 · dinheiro esperado na gaveta",
          "before": "R$ 100,00",
          "after": "R$ 105,49"
        },
        {
          "key": "Servidor · turno CX02 · vendas",
          "before": "2 · R$ 45,38",
          "after": "3 · R$ 50,87"
        }
      ],
      "notes": [
        "CX02: venda 74e6b3 · 1 × Leite 1L a R$ 5,49 = R$ 5,49 · total R$ 5,49 · troco R$ 0,00 · NFC-e pending",
        "CX02: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 17,
      "phase": "Internet fora",
      "situation": "Internet volta; a NFC-e em contingência é transmitida e autorizada",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: worker de contingência (a implementar) transmite o XML e recebe protocolo.",
        "Caixa: grava NfceAuthorized no stream da venda (seq 2) e envia.",
        "Servidor: Projector remove a venda de nfce_queue."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b11-e711-78ee-8a12-6ad011cb3360\",\n      \"streamId\": \"01a14ae3-3ba8-7e94-ae0a-86d5ed74e6b3\",\n      \"streamSeq\": 2,\n      \"type\": \"NfceAuthorized\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T18:13:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3ba8-7e94-ae0a-86d5ed74e6b3\",\n        \"nfceKey\": \"3526100000000000000065001000000003900000000\",\n        \"protocol\": \"135260004417788\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b11-e711-78ee-8a12-6ad011cb3360\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "4",
          "after": "5"
        },
        {
          "key": "Servidor · events gravados",
          "before": "14",
          "after": "15"
        },
        {
          "key": "Servidor · fila NFC-e · CX02",
          "before": "pending",
          "after": null
        }
      ],
      "notes": [
        "CX02: evento NfceAuthorized gravado na outbox (event_id 01a14b11-e711…, seq 2)",
        "CX02: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 18,
      "phase": "Internet fora",
      "situation": "Uma NFC-e do CX01 volta rejeitada pela SEFAZ (NCM inválido)",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: retorno da SEFAZ vira evento NfceRejected (seq 2 no stream da venda).",
        "Servidor: Projector põe a venda em nfce_queue com status rejected e o motivo, para o back-office corrigir o cadastro e reenviar."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b14-a631-7fb5-8a4e-e5c7f58a1bd1\",\n      \"streamId\": \"01a14ae3-3b61-7d3b-b374-dcb031eee4ab\",\n      \"streamSeq\": 2,\n      \"type\": \"NfceRejected\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T18:16:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b61-7d3b-b374-dcb031eee4ab\",\n        \"nfceKey\": \"3526100000000000000065001000000002100000000\",\n        \"code\": \"778\",\n        \"reason\": \"Informado NCM inexistente\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b14-a631-7fb5-8a4e-e5c7f58a1bd1\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "10",
          "after": "11"
        },
        {
          "key": "Servidor · events gravados",
          "before": "15",
          "after": "16"
        },
        {
          "key": "Servidor · fila NFC-e · CX01",
          "before": null,
          "after": "rejected"
        }
      ],
      "notes": [
        "CX01: evento NfceRejected gravado na outbox (event_id 01a14b14-a631…, seq 2)",
        "CX01: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 19,
      "phase": "Conflitos",
      "situation": "Gerente tira o leite de linha enquanto o CX02 está sem rede",
      "routes": [
        "PUT /backoffice/products/01a14ae3-3924-7ef2-be96-1bc597278736",
        "GET /sync/catalog"
      ],
      "process": [
        "Back-office: PUT com active = false (exclusão é lógica, nunca DELETE).",
        "CX01: puxa o delta e o leite some da busca por código de barras.",
        "CX02: está fora da rede, não recebe; segue com o leite ativo no catálogo local."
      ],
      "exchanges": [
        {
          "actor": "Back-office",
          "method": "PUT",
          "path": "/backoffice/products/01a14ae3-3924-7ef2-be96-1bc597278736",
          "request": "{\n  \"id\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n  \"name\": \"Leite 1L\",\n  \"barcode\": \"7891000300305\",\n  \"plu\": null,\n  \"unit\": \"UN\",\n  \"priceCents\": 549,\n  \"ncm\": \"04012010\",\n  \"active\": false\n}",
          "status": 200,
          "response": "{\n  \"version\": 9\n}",
          "note": null
        },
        {
          "actor": "CX01",
          "method": "GET",
          "path": "/sync/catalog?since=8",
          "request": null,
          "status": 200,
          "response": "{\n  \"since\": 8,\n  \"latestVersion\": 9,\n  \"snapshotRequired\": false,\n  \"changes\": [\n    {\n      \"version\": 9,\n      \"entity\": \"product\",\n      \"entityId\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n      \"data\": {\n        \"id\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n        \"ncm\": \"04012010\",\n        \"plu\": null,\n        \"name\": \"Leite 1L\",\n        \"unit\": \"UN\",\n        \"active\": false,\n        \"barcode\": \"7891000300305\",\n        \"priceCents\": 549\n      }\n    }\n  ]\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "GET",
          "path": "/sync/catalog?since=8",
          "request": null,
          "status": null,
          "response": null,
          "note": "sem conexão com o servidor"
        }
      ],
      "effects": [
        {
          "key": "CX01 · versão do catálogo",
          "before": "8",
          "after": "9"
        },
        {
          "key": "Servidor · cadastro · Leite 1L",
          "before": "R$ 5,49",
          "after": "R$ 5,49 (inativo)"
        },
        {
          "key": "Servidor · versão do catálogo",
          "before": "8",
          "after": "9"
        }
      ],
      "notes": [
        "Leite 1L: R$ 5,49 · inativado (active = false)",
        "CX01: catálogo local na versão 9",
        "CX01: leite não é mais encontrado pelo leitor",
        "CX02: catálogo não atualizado (sem conexão com o servidor (simulado)); segue na versão 8"
      ]
    },
    {
      "number": 20,
      "phase": "Conflitos",
      "situation": "CX02, ainda offline, vende 2 leites; depois volta à rede",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events",
        "GET /sync/catalog"
      ],
      "process": [
        "Caixa: vende com o catálogo que tem; o evento carrega o preço praticado e a catalog_version antiga.",
        "Servidor: aceita o evento normalmente. Regra: exclusão lógica mantém o produto referenciável e o estoque baixa.",
        "Relatórios podem listar vendas feitas com versão de catálogo defasada."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b1a-2471-7ae1-9d57-826e8c1f124f\",\n      \"streamId\": \"01a14ae3-3c00-7d7e-a65c-6935e72bdbea\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T18:22:46.897633\\u002B00:00\",\n      \"catalogVersion\": 8,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3c00-7d7e-a65c-6935e72bdbea\",\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3c00-7a86-915c-3eff90a67938\",\n            \"productId\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n            \"description\": \"Leite 1L\",\n            \"quantity\": 2,\n            \"unitPriceCents\": 549,\n            \"totalCents\": 1098\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 1098,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 1098,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000004100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b1a-2471-7ae1-9d57-826e8c1f124f\"\n  ]\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "GET",
          "path": "/sync/catalog?since=8",
          "request": null,
          "status": 200,
          "response": "{\n  \"since\": 8,\n  \"latestVersion\": 9,\n  \"snapshotRequired\": false,\n  \"changes\": [\n    {\n      \"version\": 9,\n      \"entity\": \"product\",\n      \"entityId\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n      \"data\": {\n        \"id\": \"01a14ae3-3924-7ef2-be96-1bc597278736\",\n        \"ncm\": \"04012010\",\n        \"plu\": null,\n        \"name\": \"Leite 1L\",\n        \"unit\": \"UN\",\n        \"active\": false,\n        \"barcode\": \"7891000300305\",\n        \"priceCents\": 549\n      }\n    }\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "5",
          "after": "6"
        },
        {
          "key": "CX02 · versão do catálogo",
          "before": "8",
          "after": "9"
        },
        {
          "key": "Servidor · estoque · Leite 1L",
          "before": "23",
          "after": "21"
        },
        {
          "key": "Servidor · events gravados",
          "before": "16",
          "after": "17"
        },
        {
          "key": "Servidor · turno CX02 · dinheiro esperado na gaveta",
          "before": "R$ 105,49",
          "after": "R$ 116,47"
        },
        {
          "key": "Servidor · turno CX02 · vendas",
          "before": "3 · R$ 50,87",
          "after": "4 · R$ 61,85"
        }
      ],
      "notes": [
        "CX02: venda 2bdbea · 2 × Leite 1L a R$ 5,49 = R$ 10,98 · total R$ 10,98 · troco R$ 0,00 · NFC-e authorized",
        "CX02: worker enviou 1 evento(s)",
        "CX02: catálogo local na versão 9"
      ]
    },
    {
      "number": 21,
      "phase": "Conflitos",
      "situation": "Os dois caixas vendem a última unidade de azeite ao mesmo tempo",
      "routes": [
        "POST /sync/events"
      ],
      "process": [
        "Caixas: nenhum consulta o servidor para vender; ambos vendem.",
        "Servidor: estoque fica −1. Venda nunca é bloqueada; o saldo negativo é o alerta de divergência para conferência."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b1c-e391-77d0-86e1-88ccdd5b494b\",\n      \"streamId\": \"01a14ae3-3c1c-753c-96c7-73b291e5d4c8\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T18:25:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3c1c-753c-96c7-73b291e5d4c8\",\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3c1c-7464-8d8f-06880a1b2251\",\n            \"productId\": \"01a14ae3-3932-7c48-8d04-20ac4cf1b27a\",\n            \"description\": \"Azeite 500ml\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 3490,\n            \"totalCents\": 3490\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"debit\",\n            \"amountCents\": 3490,\n            \"tefNsu\": \"004513\"\n          }\n        ],\n        \"totalCents\": 3490,\n        \"changeCents\": 0,\n        \"nfceKey\": \"3526100000000000000065001000000004100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b1c-e391-77d0-86e1-88ccdd5b494b\"\n  ]\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b1c-e391-78ea-8e31-e05163e098d0\",\n      \"streamId\": \"01a14ae3-3c1e-766e-8997-df4b9499bcba\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T18:25:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3c1e-766e-8997-df4b9499bcba\",\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3c1e-76f7-9a6b-bd34ddc3e4de\",\n            \"productId\": \"01a14ae3-3932-7c48-8d04-20ac4cf1b27a\",\n            \"description\": \"Azeite 500ml\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 3490,\n            \"totalCents\": 3490\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 3500,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 3490,\n        \"changeCents\": 10,\n        \"nfceKey\": \"3526100000000000000065001000000005100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b1c-e391-78ea-8e31-e05163e098d0\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "11",
          "after": "12"
        },
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "6",
          "after": "7"
        },
        {
          "key": "Servidor · estoque · Azeite 500ml",
          "before": "1",
          "after": "-1"
        },
        {
          "key": "Servidor · events gravados",
          "before": "17",
          "after": "19"
        },
        {
          "key": "Servidor · turno CX01 · vendas",
          "before": "3 · R$ 124,43",
          "after": "4 · R$ 159,33"
        },
        {
          "key": "Servidor · turno CX02 · dinheiro esperado na gaveta",
          "before": "R$ 116,47",
          "after": "R$ 151,37"
        },
        {
          "key": "Servidor · turno CX02 · vendas",
          "before": "4 · R$ 61,85",
          "after": "5 · R$ 96,75"
        }
      ],
      "notes": [
        "CX01: venda e5d4c8 · 1 × Azeite 500ml a R$ 34,90 = R$ 34,90 · total R$ 34,90 · troco R$ 0,00 · NFC-e authorized",
        "CX02: venda 99bcba · 1 × Azeite 500ml a R$ 34,90 = R$ 34,90 · total R$ 34,90 · troco R$ 0,10 · NFC-e authorized",
        "CX01: worker enviou 1 evento(s)",
        "CX02: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 22,
      "phase": "Conflitos",
      "situation": "Cliente do CX01 desiste do feijão da primeira venda",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: SaleService.CancelItem valida que o item é da venda e que a devolução sai por uma forma usada nela; grava SaleItemCancelled (seq 2) com refundMethod = cash. A venda original não muda.",
        "Servidor: Projector procura o SaleCompleted do stream, estorna a quantidade (kind = item_cancelled), subtrai o valor do turno e, por ser em dinheiro, do dinheiro esperado na gaveta."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b1f-a2b1-76c3-a204-825b5214804e\",\n      \"streamId\": \"01a14ae3-3af5-789a-a919-69b8da0ee676\",\n      \"streamSeq\": 2,\n      \"type\": \"SaleItemCancelled\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T18:28:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3af5-789a-a919-69b8da0ee676\",\n        \"itemId\": \"01a14ae3-3af2-7a01-a3bd-cefb54f4a32d\",\n        \"reason\": \"cliente desistiu\",\n        \"refundMethod\": \"cash\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b1f-a2b1-76c3-a204-825b5214804e\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "12",
          "after": "13"
        },
        {
          "key": "Servidor · estoque · Feijão 1kg",
          "before": "36",
          "after": "37"
        },
        {
          "key": "Servidor · events gravados",
          "before": "19",
          "after": "20"
        },
        {
          "key": "Servidor · turno CX01 · dinheiro esperado na gaveta",
          "before": "R$ 195,96",
          "after": "R$ 186,97"
        },
        {
          "key": "Servidor · turno CX01 · vendas",
          "before": "4 · R$ 159,33",
          "after": "4 · R$ 150,34"
        }
      ],
      "notes": [
        "CX01: item Feijão 1kg cancelado (R$ 8,99)",
        "CX01: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 23,
      "phase": "Conflitos",
      "situation": "CX02 cancela a venda inteira da banana (PIX estornado)",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: SaleService.CancelSale grava SaleCancelled no stream da venda.",
        "Servidor: estorna todos os itens ainda não estornados, desconta a venda do turno e tira da fila de NFC-e."
      ],
      "exchanges": [
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b22-61d1-7676-af8c-45039e1a9806\",\n      \"streamId\": \"01a14ae3-3b25-7668-ab8e-b8ba0a777db6\",\n      \"streamSeq\": 2,\n      \"type\": \"SaleCancelled\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T18:31:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3b25-7668-ab8e-b8ba0a777db6\",\n        \"reason\": \"pagamento PIX estornado\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b22-61d1-7676-af8c-45039e1a9806\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "7",
          "after": "8"
        },
        {
          "key": "Servidor · estoque · Banana prata",
          "before": "28.765",
          "after": "30"
        },
        {
          "key": "Servidor · events gravados",
          "before": "20",
          "after": "21"
        },
        {
          "key": "Servidor · turno CX02 · vendas",
          "before": "5 · R$ 96,75",
          "after": "4 · R$ 89,35"
        }
      ],
      "notes": [
        "CX02: venda da banana cancelada",
        "CX02: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 24,
      "phase": "Preço agendado",
      "situation": "Passam 2 horas; CX01 vende 1 café sem falar com o servidor",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: LocalCatalog calcula o preço vigente no SQLite: agendamento com effective_at ≤ agora e versão posterior à última edição do produto.",
        "Caixa: todos os caixas mudam juntos, mesmo que algum esteja offline no horário."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b92-fdf1-7efc-a59b-a74f5e8d53e1\",\n      \"streamId\": \"01a14ae3-3cfa-783c-98b1-95e304607cfb\",\n      \"streamSeq\": 1,\n      \"type\": \"SaleCompleted\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T20:34:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"saleId\": \"01a14ae3-3cfa-783c-98b1-95e304607cfb\",\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"items\": [\n          {\n            \"itemId\": \"01a14ae3-3cfa-77d6-a3d4-4833eff098f3\",\n            \"productId\": \"01a14ae3-392c-7e08-9dc1-02ea25aa7a02\",\n            \"description\": \"Caf\\u00E9 500g\",\n            \"quantity\": 1,\n            \"unitPriceCents\": 1599,\n            \"totalCents\": 1599\n          }\n        ],\n        \"payments\": [\n          {\n            \"method\": \"cash\",\n            \"amountCents\": 2000,\n            \"tefNsu\": null\n          }\n        ],\n        \"totalCents\": 1599,\n        \"changeCents\": 401,\n        \"nfceKey\": \"3526100000000000000065001000000005100000000\",\n        \"nfceStatus\": \"authorized\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b92-fdf1-7efc-a59b-a74f5e8d53e1\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "13",
          "after": "14"
        },
        {
          "key": "Servidor · estoque · Café 500g",
          "before": "18",
          "after": "17"
        },
        {
          "key": "Servidor · events gravados",
          "before": "21",
          "after": "22"
        },
        {
          "key": "Servidor · turno CX01 · dinheiro esperado na gaveta",
          "before": "R$ 186,97",
          "after": "R$ 202,96"
        },
        {
          "key": "Servidor · turno CX01 · vendas",
          "before": "4 · R$ 150,34",
          "after": "5 · R$ 166,33"
        }
      ],
      "notes": [
        "CX01: venda 607cfb · 1 × Café 500g a R$ 15,99 = R$ 15,99 · total R$ 15,99 · troco R$ 4,01 · NFC-e authorized",
        "CX01: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 25,
      "phase": "Fechamento",
      "situation": "Sangria de R$ 50,00 no CX01",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: ShiftService.Withdraw grava CashWithdrawal no stream do turno.",
        "Servidor: soma em withdrawals_cents; o dinheiro esperado na gaveta cai."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b95-bd11-7270-a089-e1fd9b6e77c9\",\n      \"streamId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n      \"streamSeq\": 2,\n      \"type\": \"CashWithdrawal\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T20:37:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"amountCents\": 5000,\n        \"reason\": \"sangria para o cofre\"\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b95-bd11-7270-a089-e1fd9b6e77c9\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "14",
          "after": "15"
        },
        {
          "key": "Servidor · events gravados",
          "before": "22",
          "after": "23"
        },
        {
          "key": "Servidor · turno CX01 · dinheiro esperado na gaveta",
          "before": "R$ 202,96",
          "after": "R$ 152,96"
        },
        {
          "key": "Servidor · turno CX01 · sangrias",
          "before": "R$ 0,00",
          "after": "R$ 50,00"
        }
      ],
      "notes": [
        "CX01: sangria registrada",
        "CX01: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 26,
      "phase": "Fechamento",
      "situation": "Maria e João fecham os turnos contando a gaveta",
      "routes": [
        "local · SQLite do caixa",
        "POST /sync/events"
      ],
      "process": [
        "Caixa: ShiftService.Close grava ShiftClosed com o dinheiro declarado e limpa open_shift_id.",
        "Servidor: shift_totals guarda declarado × esperado (fundo + vendas em dinheiro − troco − sangrias) para conferência."
      ],
      "exchanges": [
        {
          "actor": "CX01",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b98-7c31-7d02-a6c4-45a72911c15a\",\n      \"streamId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n      \"streamSeq\": 3,\n      \"type\": \"ShiftClosed\",\n      \"terminalId\": \"CX01\",\n      \"occurredAt\": \"2026-10-17T20:40:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"shiftId\": \"01a14ae3-3aad-7ae5-a2ae-db62bec9e528\",\n        \"declaredCashCents\": 15296\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b98-7c31-7d02-a6c4-45a72911c15a\"\n  ]\n}",
          "note": null
        },
        {
          "actor": "CX02",
          "method": "POST",
          "path": "/sync/events",
          "request": "{\n  \"events\": [\n    {\n      \"eventId\": \"01a14b98-7c31-7d02-a6c4-45a72911c15b\",\n      \"streamId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n      \"streamSeq\": 2,\n      \"type\": \"ShiftClosed\",\n      \"terminalId\": \"CX02\",\n      \"occurredAt\": \"2026-10-17T20:40:46.897633\\u002B00:00\",\n      \"catalogVersion\": 9,\n      \"payload\": {\n        \"shiftId\": \"01a14ae3-3acb-7549-8fa7-892ecab48be6\",\n        \"declaredCashCents\": 15137\n      }\n    }\n  ]\n}",
          "status": 200,
          "response": "{\n  \"accepted\": [\n    \"01a14b98-7c31-7d02-a6c4-45a72911c15b\"\n  ]\n}",
          "note": null
        }
      ],
      "effects": [
        {
          "key": "CX01 · eventos na outbox (total)",
          "before": "15",
          "after": "16"
        },
        {
          "key": "CX01 · turno aberto",
          "before": "sim",
          "after": "não"
        },
        {
          "key": "CX02 · eventos na outbox (total)",
          "before": "8",
          "after": "9"
        },
        {
          "key": "CX02 · turno aberto",
          "before": "sim",
          "after": "não"
        },
        {
          "key": "Servidor · events gravados",
          "before": "23",
          "after": "25"
        },
        {
          "key": "Servidor · turno CX01 · status",
          "before": "aberto",
          "after": "fechado, declarado R$ 152,96"
        },
        {
          "key": "Servidor · turno CX02 · status",
          "before": "aberto",
          "after": "fechado, declarado R$ 151,37"
        }
      ],
      "notes": [
        "CX01: Maria conta R$ 152,96 (o feijão cancelado foi devolvido em dinheiro)",
        "CX02: João conta R$ 151,37",
        "CX01: worker enviou 1 evento(s)",
        "CX02: worker enviou 1 evento(s)"
      ]
    },
    {
      "number": 27,
      "phase": "Fechamento",
      "situation": "Auditoria: apagar e reconstruir todas as projeções a partir do log",
      "routes": [
        "POST /backoffice/projections/rebuild"
      ],
      "process": [
        "Back-office: POST /backoffice/projections/rebuild.",
        "Servidor: TRUNCATE stock_movements, shift_totals, nfce_queue e relê events em ordem de event_id.",
        "Esperado: nenhum número muda. É o que garante restaurar o servidor por pg_dump + replay das outboxes."
      ],
      "exchanges": [
        {
          "actor": "Back-office",
          "method": "POST",
          "path": "/backoffice/projections/rebuild",
          "request": null,
          "status": 204,
          "response": null,
          "note": null
        }
      ],
      "effects": [],
      "notes": [
        "Projeções reconstruídas"
      ]
    }
  ],
  "final": [
    {
      "key": "CX01 · eventos na outbox (total)",
      "value": "16"
    },
    {
      "key": "CX01 · outbox pendente",
      "value": "0"
    },
    {
      "key": "CX01 · turno aberto",
      "value": "não"
    },
    {
      "key": "CX01 · versão do catálogo",
      "value": "9"
    },
    {
      "key": "CX02 · eventos na outbox (total)",
      "value": "9"
    },
    {
      "key": "CX02 · outbox pendente",
      "value": "0"
    },
    {
      "key": "CX02 · turno aberto",
      "value": "não"
    },
    {
      "key": "CX02 · versão do catálogo",
      "value": "9"
    },
    {
      "key": "Servidor · cadastro · Arroz 5kg",
      "value": "R$ 28,99"
    },
    {
      "key": "Servidor · cadastro · Azeite 500ml",
      "value": "R$ 34,90"
    },
    {
      "key": "Servidor · cadastro · Banana prata",
      "value": "R$ 5,99"
    },
    {
      "key": "Servidor · cadastro · Café 500g",
      "value": "R$ 18,99"
    },
    {
      "key": "Servidor · cadastro · Feijão 1kg",
      "value": "R$ 9,49"
    },
    {
      "key": "Servidor · cadastro · Leite 1L",
      "value": "R$ 5,49 (inativo)"
    },
    {
      "key": "Servidor · estoque · Arroz 5kg",
      "value": "47"
    },
    {
      "key": "Servidor · estoque · Azeite 500ml",
      "value": "-1"
    },
    {
      "key": "Servidor · estoque · Banana prata",
      "value": "30"
    },
    {
      "key": "Servidor · estoque · Café 500g",
      "value": "17"
    },
    {
      "key": "Servidor · estoque · Feijão 1kg",
      "value": "37"
    },
    {
      "key": "Servidor · estoque · Leite 1L",
      "value": "21"
    },
    {
      "key": "Servidor · events gravados",
      "value": "25"
    },
    {
      "key": "Servidor · fila NFC-e · CX01",
      "value": "rejected"
    },
    {
      "key": "Servidor · turno CX01 · dinheiro esperado na gaveta",
      "value": "R$ 152,96"
    },
    {
      "key": "Servidor · turno CX01 · sangrias",
      "value": "R$ 50,00"
    },
    {
      "key": "Servidor · turno CX01 · status",
      "value": "fechado, declarado R$ 152,96"
    },
    {
      "key": "Servidor · turno CX01 · vendas",
      "value": "5 · R$ 166,33"
    },
    {
      "key": "Servidor · turno CX02 · dinheiro esperado na gaveta",
      "value": "R$ 151,37"
    },
    {
      "key": "Servidor · turno CX02 · sangrias",
      "value": "R$ 0,00"
    },
    {
      "key": "Servidor · turno CX02 · status",
      "value": "fechado, declarado R$ 151,37"
    },
    {
      "key": "Servidor · turno CX02 · vendas",
      "value": "4 · R$ 89,35"
    },
    {
      "key": "Servidor · versão do catálogo",
      "value": "9"
    }
  ]
};
