# Contribuindo com o Synthia

Obrigado por querer ajudar! O Synthia é um PDV offline-first para supermercados: caixa (Avalonia + SQLite), servidor
da loja (ASP.NET Core + PostgreSQL) e retaguarda (Blazor). Toda contribuição é bem-vinda: correção, teste,
documentação, tradução de mensagens, integração com hardware ou fiscal.

## Antes de começar

- **Bug ou ideia pequena:** abra uma issue (há modelos) ou mande o PR direto.
- **Mudança grande** (evento novo, tabela nova, integração fiscal/TEF, tela nova): abra uma issue antes para
  combinarmos o desenho. Poupa retrabalho dos dois lados.
- **Falha de segurança:** não abra issue pública; veja [SECURITY.md](SECURITY.md).

## Ambiente

Requisitos: **.NET 10 SDK** e **PostgreSQL 16** (os testes do servidor rodam contra um Postgres de verdade).

```bash
createuser -s synthia --pwprompt          # senha: synthia
createdb -O synthia synthia
export SYNTHIA_TEST_PG="Host=localhost;Username=synthia;Password=synthia;Database=postgres"

dotnet build
dotnet test                                # caixa, servidor e tela do caixa (headless)
dotnet run --project src/Synthia.Server    # http://localhost:5290/docs e /retaguarda (admin / synthia)
dotnet run --project src/Synthia.Terminal.App -- --demo   # caixa com dispositivos simulados
```

O CI (GitHub Actions) roda build e testes com Postgres 16 em todo PR.

## Como o sistema pensa (leia antes de mexer no núcleo)

- **Eventos são fatos imutáveis.** Venda, cancelamento, entrada, contagem e perda viram eventos no log `events`.
  Nunca edite nem apague evento; corrija com outro evento.
- **Contratos são compartilhados** (`src/Synthia.Contracts`) e caixas antigos convivem com servidor novo:
  só acrescente campos opcionais ao final dos payloads ou crie um tipo de evento novo. Não renomeie nem remova.
- **Reenviar é seguro.** O servidor grava com `ON CONFLICT (event_id) DO NOTHING`; IDs são UUIDv7 gerados na origem
  (`Uuid7`). Qualquer escrita nova no sync precisa continuar idempotente.
- **Projeções são descartáveis.** Saldos (`stock_levels`), vendas para relatório (`sales`…), turnos e fila de
  NFC-e são derivados do log na mesma transação e reconstruídos por `RebuildProjectionsAsync`. Projeção nova
  precisa entrar no rebuild e ter teste mostrando que o resultado é igual antes e depois dele.
- **Migrações só para frente.** Arquivo novo `Data/Migrations/NNNN_nome.sql` com o próximo número; nunca edite uma
  migração já publicada. Elas rodam sem timeout, então carga de dados é permitida.
- **O caixa vende offline.** Nada no caminho da venda pode depender do servidor, da nuvem ou de rede (exceto TEF e
  autorização online de NFC-e, que têm contingência).
- **Dinheiro em centavos** (`long`), **datas em UTC**, quantidade em `decimal` (peso com 3 casas).
- **Volume importa.** Uma loja gera ~450 mil eventos por ano. Consulta da retaguarda não pode varrer o log inteiro;
  `tools/load/one-year.sql` gera um ano de dados para medir.

## Estilo

- **Commits em inglês**, no padrão [Conventional Commits](https://www.conventionalcommits.org/):
  `feat(server): …`, `fix(caixa): …`, `perf(server): …`, `docs: …`, `test: …`. Pequenos e focados: um assunto por
  commit, de preferência com o teste junto.
- **Código:** identificadores em inglês; comentários, mensagens de tela e docs em português, com o vocabulário do
  domínio (caixa, sangria, turno, retaguarda). Comente o *porquê*, não o *o quê*.
- **Testes:** todo comportamento novo vem com teste. Nomes de teste em português descrevendo o cenário
  (`Venda_atrasada_de_caixa_offline_anterior_a_contagem_nao_mexe_no_saldo`).
- **Telas:** mudou uma tela? Rode `tools/screenshots.sh` para regerar as capturas de `docs/img` e confira se nada
  estoura a largura em 1366 px.
- **Docs:** comportamento visível ou rota nova também atualiza o README e, se couber, `docs/index.html`.

## Pull requests

1. Faça um fork e crie um branch a partir de `main`.
2. `dotnet build` sem erros e `dotnet test` verde.
3. Descreva o que muda e por quê, e como você testou. O modelo de PR ajuda.
4. Mudança em fiscal, TEF ou sync: diga o que acontece com caixas que ainda estão na versão anterior.

## Licença das contribuições

O projeto é licenciado sob a [AGPL-3.0](LICENSE). Ao enviar uma contribuição, você concorda que ela seja distribuída
sob essa mesma licença.

## Conduta

Seguimos o [Código de Conduta](CODE_OF_CONDUCT.md). Respeito acima de tudo.
