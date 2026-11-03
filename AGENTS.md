# AGENTS.md

Guia para agentes de código (Claude Code, Codex, Cursor, Copilot...) trabalhando no Synthia. Pessoas: o mesmo
conteúdo, com mais contexto, está em [CONTRIBUTING.md](CONTRIBUTING.md). Resumo do projeto: [llms.txt](llms.txt).

## O projeto em uma frase

PDV offline-first para supermercados: o caixa (Avalonia + SQLite) vende sozinho e manda eventos imutáveis por uma
outbox; o servidor da loja (ASP.NET Core + PostgreSQL) grava os eventos de forma idempotente, mantém projeções e
serve a retaguarda (Blazor Server) e as docs.

## Mapa

```
src/Synthia.Contracts        eventos, payloads, mensagens de sync, Uuid7, JSON (compartilhado caixa ↔ servidor)
src/Synthia.Terminal.Core    regras do caixa: SQLite, outbox, catálogo, CheckoutSession, cupom, NFC-e, workers de sync
src/Synthia.Terminal.Devices balança Toledo, ESC/POS, leitor serial, simulados
src/Synthia.Terminal.App     tela do caixa (Avalonia, MVVM com CommunityToolkit.Mvvm)
src/Synthia.Server           Program.cs (rotas), Sync/ (EventStore, Projector, StockLevels), Backoffice/ (serviços),
                             Retaguarda/ (Blazor), Data/Migrations/*.sql, wwwroot/retaguarda
tests/                       Terminal.Tests (SQLite + servidor falso), Server.Tests (Postgres real), Terminal.App.Tests (headless)
tools/                       simulate.sh, screenshots.sh, load/one-year.sql, branding/
docs/                        index.html (docs + console de API), sistema.html (visão geral), simulacao.html
```

## Comandos

```bash
# Postgres 16 local com usuário synthia/synthia que possa criar bancos (os testes criam um banco por classe)
export SYNTHIA_TEST_PG="Host=localhost;Username=synthia;Password=synthia;Database=postgres"

dotnet build                       # precisa sair com "0 Error(s)"
dotnet test                        # tudo; ~1 min
dotnet test tests/Synthia.Server.Tests --filter StockLevels   # um grupo
dotnet run --project src/Synthia.Server                         # :5290, /docs e /retaguarda (admin / synthia em Development)
dotnet run --project src/Synthia.Terminal.App -- --demo         # caixa com dispositivos simulados
tools/screenshots.sh               # regera docs/img (caixa e retaguarda) com um dia de loja simulado
tools/simulate.sh                  # regera docs/simulacao-data.js
```

## Regras que não podem ser quebradas

1. **Eventos são imutáveis.** Nunca altere nem apague linhas de `events`; correção é outro evento.
2. **Compatibilidade de contratos.** Em `Synthia.Contracts`, só acrescente parâmetros opcionais no fim dos records
   ou tipos de evento novos. Caixas em versão antiga continuam mandando o formato antigo.
3. **Idempotência.** Toda escrita disparada pelo sync precisa tolerar o mesmo evento duas vezes
   (`ON CONFLICT DO NOTHING`, checagens de "já aplicado"). IDs de evento são `Uuid7.Next()`, crescentes: o servidor
   processa cada lote na ordem dos IDs.
4. **Projeções reconstruíveis.** Projeção nova entra em `TRUNCATE` e no replay de
   `EventStore.RebuildProjectionsAsync`, com teste comparando resultado antes e depois do rebuild.
5. **Migrações só para frente.** Novo arquivo `src/Synthia.Server/Data/Migrations/NNNN_nome.sql` com o próximo
   número (embutido automaticamente). Nunca edite migração existente.
6. **Venda não depende de rede.** Nada no caminho de `CheckoutSession`/`SaleService` pode chamar o servidor.
7. **Volume.** Consultas da retaguarda não podem varrer `events` ou `stock_movements` inteiros; use as projeções
   (`stock_levels`, `sales`, `sale_items`, `sale_payments`). Um ano de loja ≈ 450 mil eventos.
8. **Dinheiro em centavos (`long`), datas em UTC**, quantidade `decimal` (peso com 3 casas).
9. **CNPJ fiscal é o do caixa** (`terminal.json`, certificado). O cadastro da loja só serve para exibição e aviso.

## Convenções

- Commits pequenos, em inglês, Conventional Commits: `feat(server): …`, `fix(caixa): …`, `perf(server): …`,
  `test: …`, `docs: …`, `chore: …`.
- Identificadores em inglês; comentários, textos de tela e docs em português do domínio. Comente o porquê.
- Testes com nomes em português descrevendo o cenário; comportamento novo vem com teste.
- Servidor usa Dapper com SQL explícito (sem ORM). Records posicionais funcionam quando as colunas batem em nome e
  tipo; com somas, faça cast (`::bigint`, `::int`).
- Retaguarda: páginas interativas usam `@rendermode InteractiveServer`; autorização por papel com
  `[Authorize(Roles = ...)]` (`BackofficeRoles`). Teste larguras em 1366 px (fontes da marca são largas).
- Caixa: lógica no `Terminal.Core`, a tela só repassa teclas e mostra estado.

## Armadilhas conhecidas

- Sem Postgres rodando, os testes do servidor falham com `Connection refused`, o que não tem a ver com o código.
- `docs/` é copiada para a saída do build: mudou uma página, rode `dotnet build` antes de conferir em `/docs`.
- `pkill -f Synthia.Server` mata o próprio shell (o padrão aparece na linha de comando); filtre por PID.
- `Guid.CreateVersion7()` não é monotônico dentro do mesmo milissegundo; para eventos use `Uuid7.Next()`.
- Rodando o servidor fora de `src/Synthia.Server`, use `ASPNETCORE_ENVIRONMENT=Development` para servir os estáticos.
- O rebuild das projeções leva ~33 min com um ano de dados e segura o sync: não chame em teste de carga à toa.

## Antes de entregar

`dotnet build` sem erros, `dotnet test` verde, README/docs atualizados se mudou comportamento visível, capturas
regeradas se mudou tela.
