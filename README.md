# PDV Synthia

PDV offline-first para o Market SP: o caixa vende sozinho; servidor da loja e nuvem só consolidam.

Documentação interativa (arquitetura, simulador de sync, eventos, API, falhas): [`docs/index.html`](docs/index.html).

Simulação real de um dia de loja, passo a passo (situação → rota → processo → resposta → efeito): [`docs/simulacao.html`](docs/simulacao.html), gerada por `tools/simulate.sh`.

## Stack

- **.NET 10** em todas as camadas, contratos compartilhados em `Synthia.Contracts`
- **Caixa:** SQLite (WAL) com outbox transacional, workers de sync em background, NFC-e via ACBr (a integrar)
- **Servidor local:** ASP.NET Core minimal API + PostgreSQL 16, eventos idempotentes e projeções reconstruíveis
- **IDs:** UUIDv7 monotônico gerado na origem

## Estrutura

```
src/
  Synthia.Contracts       envelope de evento, payloads, mensagens de sync, Uuid7
  Synthia.Terminal.Core   SQLite local, outbox, catálogo, venda, turno, workers de sync
  Synthia.Server          API de sync, event store, projeções, catálogo versionado
tests/
  Synthia.Terminal.Tests  caixa com SQLite temporário e servidor falso
  Synthia.Server.Tests    servidor contra Postgres real + ponta a ponta caixa → servidor
docs/
  index.html              documentação interativa
```

## Rodando

Requer .NET 10 SDK e PostgreSQL.

```bash
# banco de desenvolvimento
createuser -s synthia && createdb -O synthia synthia   # senha: synthia

# servidor (aplica as migrações na subida)
dotnet run --project src/Synthia.Server

# testes; SYNTHIA_TEST_PG deve apontar para um usuário que possa criar bancos
export SYNTHIA_TEST_PG="Host=localhost;Username=synthia;Password=synthia;Database=postgres"
dotnet test
```
