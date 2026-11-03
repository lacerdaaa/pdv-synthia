# Segurança

O Synthia guarda vendas, dados fiscais da loja e senhas da retaguarda. Se você achou uma falha, obrigado por avisar
com cuidado.

## Como reportar

**Não abra issue pública.** Use o reporte privado do GitHub:
[Security › Report a vulnerability](https://github.com/lacerdaaa/pdv-synthia/security/advisories/new).

Conte o que encontrou, como reproduzir e qual o impacto que você imagina. Respondemos assim que possível e
combinamos com você a correção e a divulgação.

## O que nos interessa

- Acesso à retaguarda sem login ou com papel acima do permitido (admin, gerente, estoquista).
- Caixa enviando eventos em nome de outro caixa, ou sync aceitando eventos sem a chave do caixa fora de
  `Development`.
- Uso da API `/backoffice` sem login nem `X-Api-Key`.
- Injeção de SQL, XSS ou leitura de arquivos nas importações (CSV, XML de NF-e, logo da loja).
- Vazamento de dados fiscais da loja ou de senhas.

## Fora do escopo

- Instalação com as credenciais de desenvolvimento (`synthia`) expostas na internet, ou servidor rodando com
  `ASPNETCORE_ENVIRONMENT=Development` em produção.
- Ataques que exigem acesso físico ao mini PC do caixa ou do servidor.

## Boas práticas para quem instala

- Defina `Backoffice:InitialAdminPassword` (ou anote a senha gerada no primeiro start) e troque-a.
- Configure uma chave por caixa em `Sync:Terminals` e use `SYNTHIA_TERMINAL_KEY` no caixa.
- Mantenha o servidor só na rede da loja; a retaguarda não precisa estar exposta à internet.
- O certificado A1 fica no caixa; a senha vem de `SYNTHIA_CERT_PASSWORD`, nunca de arquivo versionado.
