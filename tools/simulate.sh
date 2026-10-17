#!/usr/bin/env bash
# Sobe o servidor contra um banco descartável e roda o simulador de um dia de loja.
# Gera docs/simulacao-data.js, lido por docs/simulacao.html.
set -euo pipefail
cd "$(dirname "$0")/.."

PG_ADMIN="${SYNTHIA_TEST_PG:-Host=localhost;Username=synthia;Password=synthia;Database=postgres}"
PG_USER="${PGUSER:-synthia}"
export PGPASSWORD="${PGPASSWORD:-synthia}"
DB=synthia_sim
URL=http://localhost:5299

psql -h localhost -U "$PG_USER" -d postgres -qc "DROP DATABASE IF EXISTS $DB WITH (FORCE)" -c "CREATE DATABASE $DB"

# Chaves usadas só nesta execução: o servidor exige chave por caixa e chave de API no back-office.
export Sync__Terminals__CX01=sim-cx01 Sync__Terminals__CX02=sim-cx02 Backoffice__ApiKeys__0=sim-backoffice
export SYNTHIA_TERMINAL_KEY_CX01=sim-cx01 SYNTHIA_TERMINAL_KEY_CX02=sim-cx02 SYNTHIA_API_KEY=sim-backoffice

dotnet build -v q PdvSynthia.slnx >/dev/null
ConnectionStrings__Synthia="${PG_ADMIN/Database=postgres/Database=$DB}" \
  dotnet run --no-build --project src/Synthia.Server --urls "$URL" >/tmp/synthia-sim-server.log 2>&1 &
SERVER=$!
trap 'kill $SERVER 2>/dev/null || true' EXIT

until curl -sf "$URL/health" >/dev/null; do sleep 0.5; done

dotnet run --no-build --project tools/Synthia.Simulator -- "$URL" "${PG_ADMIN/Database=postgres/Database=$DB}" docs/simulacao-data.js
