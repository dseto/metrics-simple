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
- Para resolver `Connector.REMOVIDO_REMOVIDO_authRef`, o runner deve suportar:
  - Env var: `METRICS_SECRET__<AUTHREF>`
- Exemplo:
  - REMOVIDO_REMOVIDO_authRef: `api_key_prod`
  - env: `METRICS_SECRET__api_key_prod=TEST_TOKEN`
- Regra:
  - Se não existir secret para o `REMOVIDO_REMOVIDO_authRef`, o runner falha com `SOURCE_ERROR` (exit code 40).
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
  - REMOVIDO_REMOVIDO_authRef = `api_key_prod`
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
> Se suportado, adicionar **IT06** usando **Azurite** (testcontainers) para validar upload. (Nota: o identificador IT04 foi reservado para Version Lifecycle.)



### IT04 — Process Version Lifecycle (CRUD completo)
Objetivo: garantir o ciclo de vida completo de versões via API, incluindo erros.

Casos mínimos (implementados):
- IT04-01 Create single version (201)
- IT04-02 Read version by id (200)
- IT04-03 List all versions (200)
- IT04-04 Update version DSL (200) + persistência
- IT04-05 Enable/Disable version (PATCH) (200)
- IT04-06 Multi-version scenario (max version selection)
- IT04-07 Conflict on create (409)
- IT04-08 Invalid schema returns 400
- IT04-09 Delete version (204) + NotFound após (404)
- IT04-10 Preview endpoint with version (200/400)
- IT04-11 Version not found (404)
- IT04-12 Schema validation in preview (400)

Critérios:
- Deve validar contratos `processVersion.schema.json` e comportamento do `/preview/transform`.
- Deve rodar sempre no CI (não condicional).

### IT05 — Real LLM Integration (OpenRouter)
Objetivo: validar integração real com provedor de LLM (sem mocks), com tolerância a falhas.

Requisitos:
- `METRICS_OPENROUTER_API_KEY` (ou `OPENROUTER_API_KEY`) presente no ambiente do teste.

Critérios:
- Aceitar **200 OK** (output válido) **ou** **502 Bad Gateway** (falha do provedor / output inválido).
- Se 200, o DSL + schema gerados devem ser executáveis (quando aplicável).
- Recomendado rodar no CI como job separado/condicional por segredo.

## Como rodar (exemplos)
- Apenas IT04:
  - `dotnet test tests/Integration.Tests --filter "FullyQualifiedName~IT04"`
- Apenas IT05:
  - `dotnet test tests/Integration.Tests --filter "FullyQualifiedName~IT05"`
- Todos:
  - `dotnet test tests/Integration.Tests`

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


---

## Delta 1.2.0 — Novos cenários obrigatórios

### Processes / Versions
- GET `/api/v1/processes/{processId}/versions` deve retornar 200 e lista ordenada por `version asc`.
- Deve nunca retornar 405 (rota deve existir com GET).

### Connectors / Delete
- DELETE `/api/v1/connectors/{id}` retorna 204 em sucesso.
- DELETE retorna 409 quando connector está em uso por processo/version.

### Connector Flex (Auth)
- Runner deve injetar auth BEARER quando connector authType=BEARER e token configurado.
- Runner deve suportar mais 1 tipo adicional (API_KEY ou BASIC) e validar injeção no request final.

### Body + Content-Type
- Para método POST: o runner deve enviar body conforme `sourceRequest.body` (ou defaults).
- Se body for objeto/array e contentType não for definido, usar application/json.
