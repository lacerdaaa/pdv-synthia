#!/usr/bin/env bash
# Regera as capturas da documentação (docs/img): a tela do caixa pelo teste headless e a retaguarda com um dia
# de loja montado num servidor descartável. Precisa de Postgres local e do Playwright (Chromium) instalados.
set -euo pipefail
cd "$(dirname "$0")/.."

PG_ADMIN="${SYNTHIA_TEST_PG:-Host=localhost;Username=synthia;Password=synthia;Database=postgres}"
export PGPASSWORD="${PGPASSWORD:-synthia}"
DB=synthia_docs
URL=http://localhost:5298

dotnet build -v q PdvSynthia.slnx >/dev/null

echo "Caixa"
SYNTHIA_SCREENSHOTS="$PWD/docs/img" dotnet test --no-build tests/Synthia.Terminal.App.Tests >/dev/null

echo "Retaguarda"
psql -h localhost -U "${PGUSER:-synthia}" -d postgres -qc "DROP DATABASE IF EXISTS $DB WITH (FORCE)" -c "CREATE DATABASE $DB"
TZ=America/Sao_Paulo ASPNETCORE_ENVIRONMENT=Development Backoffice__InitialAdminPassword=synthia \
  Sync__Terminals__CX01=docs-cx01 Sync__Terminals__CX02=docs-cx02 Sync__Terminals__CX03=docs-cx03 Sync__Terminals__CX04=docs-cx04 \
  ConnectionStrings__Synthia="${PG_ADMIN/Database=postgres/Database=$DB}" \
  dotnet run --no-build --project src/Synthia.Server --urls "$URL" >/tmp/synthia-docs-server.log 2>&1 &
SERVER=$!
trap 'kill $SERVER 2>/dev/null || true' EXIT
until curl -sf "$URL/health" >/dev/null; do sleep 0.5; done

NODE_PATH="${NODE_PATH:-$(npm root -g)}" node tools/screenshots/retaguarda.js "$URL" docs/img
