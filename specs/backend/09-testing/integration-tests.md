# Integration Tests (E2E Backend)

Data: 2026-01-02

Este documento torna **obrigatório** que o backend possua **integration tests** que validem o fluxo **end-to-end real** do produto.

> Regra não negociável: **sem integration tests cobrindo FetchSource (HTTP) + SQLite + runner**, os testes “não valem” e a entrega é considerada incompleta.

---

## Objetivo

Validar, de forma automatizada, o caminho real:

1) Configurar `Connector` / `Process` / `ProcessVersion` (via API HTTP)  
2) Executar **Runner CLI** (processo real)  
3) Runner faz **FetchSource via HTTP** (contra um mock server)  
4) Executa **Transform (Jsonata real)** → normaliza → valida schema → gera CSV  
5) Escreve CSV no destino local (e opcionalmente Blob)  
6) Retorna exit code conforme `04-execution/cli-contract.md`

---

## Requisitos obrigatórios

### Ferramentas
- `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)
- `xUnit`
- Mock HTTP para o source:
  - **Preferido (default)**: WireMock.Net (in-process, sem Docker)
  - **Opcional**: Testcontainers + WireMock container (quando Docker Desktop/CI suportar)

### O que NÃO pode
- Bater em internet/serviços reais.
- Depender de credenciais reais.
- “Mockar” o pipeline inteiro (ex.: substituir FetchSource por um retorno fixo sem HTTP).  
  O teste deve validar ao menos 1 chamada HTTP real contra um mock server.

---

## Configuração determinística para testes

Para viabilizar testes E2E repetíveis, **API e Runner** devem suportar estas configurações:

### SQLite
- Env var obrigatória para testes: `METRICS_SQLITE_PATH`
  - Ex.: `METRICS_SQLITE_PATH=/tmp/metrics-simple-it.db`
- Regra:
  - Se `METRICS_SQLITE_PATH` estiver setada, usar exatamente esse arquivo.
  - Caso contrário, pode usar um default (ex.: `./data/metrics.db`).

### Secrets (AuthRef)
- Para resolver `Connector.authRef`, o runner deve suportar:
  - Env var: `METRICS_SECRET__<AUTHREF>`
- Exemplo:
  - authRef: `api_key_prod`
  - env: `METRICS_SECRET__api_key_prod=TEST_TOKEN`
- Regra:
  - Se não existir secret para o `authRef`, o runner falha com `SOURCE_ERROR` (exit code 40).
  - Em requisições HTTP, enviar `Authorization: Bearer <secret>`.

---

## Suíte mínima obrigatória (casos)

### IT01 — CRUD + persistência via API (smoke)
- Sobe API com WebApplicationFactory
- Usa SQLite (arquivo temporário via `METRICS_SQLITE_PATH`)
- Executa:
  - POST `/api/connectors`
  - POST `/api/processes`
  - POST `/api/processes/{id}/versions`
  - GET para validar leitura
- Valida:
  - status codes corretos
  - dados persistidos (GET retorna o que foi criado)

### IT02 — E2E: API → Runner → FetchSource (HTTP mock) → CSV (obrigatório)
- Inicia **mock HTTP server** (WireMock.Net) com:
  - rota: `GET /v1/servers?limit=100&filter=active`
  - resposta: JSON fixture (ex.: `hosts-cpu-input.json`)
  - valida header: `Authorization: Bearer TEST_TOKEN`
- Sobe API com WebApplicationFactory (mesmo SQLite do teste)
- Cria via API:
  - Connector.baseUrl = URL do mock server
  - authRef = `api_key_prod`
  - Process + Version com:
    - SourceRequest: method GET, path `/v1/servers`, queryParams `limit=100`, `filter=active`
    - DSL: `hosts-cpu-dsl.jsonata`
    - OutputSchema: `hosts-cpu-output.schema.json`
- Executa o **Runner CLI como processo real**:
  - `run --processId <id> --version 1 --localBasePath <tempdir> --blob off`
  - env vars:
    - `METRICS_SQLITE_PATH` apontando para o mesmo DB
    - `METRICS_SECRET__api_key_prod=TEST_TOKEN`
- Valida:
  - exit code = 0
  - mock server registrou exatamente 1 request na rota (FetchSource ocorreu)
  - arquivo CSV foi criado no path definido em `06-storage/blob-and-local-storage.md`
  - conteúdo CSV é **byte-a-byte** igual ao fixture esperado (`hosts-cpu-expected.csv`)
    - normalização de newline deve ser conforme `csv-format.md` (\n).

### IT03 — Falha de source (obrigatório)
- Mock server retorna 500 ou timeout
- Runner deve falhar:
  - exit code = 40 (SOURCE_ERROR)
  - logs indicam step FetchSource e errorCode correspondente

> Observação: Blob/Azurite é opcional nesta versão (somente se `--blob on` for suportado).  
> Se suportado, adicionar IT04 usando **Azurite** (testcontainers) para validar upload.

---

## Implementação recomendada (não normativa)

### API in-memory
- Usar `WebApplicationFactory<Program>` e `CreateClient()` para chamar os endpoints.

### Mock HTTP (preferido)
- WireMock.Net:
  - iniciar em porta dinâmica
  - configurar stubs e verificação de requests

### Runner como processo real
- Invocar `dotnet run --project src/Runner ...` (ou executável buildado)
- Passar env vars do teste para o processo

---

## Critério de conclusão (DoD)
- `dotnet test` executa e passa:
  - Contracts.Tests
  - Engine.Tests
  - **Integration.Tests** (incluindo IT02 e IT03)
- Integration tests validam **FetchSource via HTTP** (mock server) e geração real de CSV.
