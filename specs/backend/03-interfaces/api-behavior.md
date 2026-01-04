# API Behavior (Backend)

Data: 2026-01-02

Define comportamento da API além do OpenAPI (forma).

Contrato canônico:
- `../../shared/openapi/config-api.yaml`

---

## Convenções
- Base path: `/api`
- Content-Type: `application/json`
- `correlationId`:
  - Se request possuir `X-Correlation-Id`, reutilizar.
  - Caso contrário, gerar e devolver em `X-Correlation-Id` (response) e no corpo de erro (`ApiError.correlationId`).

---

## Processes

### GET /processes
- Sem paginação nesta versão.
- Ordenação: `id` ascendente (determinismo).

### POST /processes
Erros (mínimo):
- 400: payload inválido
- 409: id duplicado
- 422: connector inexistente *(opcional; pode ser 400)*

### GET /processes/{id}
- 404 se não existir

### PUT /processes/{id}
- 404 se não existir (sem upsert)

### DELETE /processes/{id}
- 404 se não existir
- 409 se tiver versions (D-001)

---

## ProcessVersions

### POST /processes/{id}/versions
- `version` é **inteiro** (1..n).
- 404 se process não existir
- 409 se version já existir
- 400 se DSL/schema inválidos

### GET /processes/{id}/versions/{version}
- 404 se process ou version não existir

### PUT /processes/{id}/versions/{version}
- 404 se não existir
- 400 se payload inválido

---

## Connectors

### GET /connectors
- Ordenação: `id` ascendente
- Retorna `hasApiToken: boolean` para cada Connector
- **Nunca** retorna o valor do token (`apiToken`)

### GET /connectors/{connectorId}
- Retorna `hasApiToken: boolean`
- **Nunca** retorna o valor do token (`apiToken`)
- 404 connectorId não encontrado

### POST /connectors
- Aceita `apiToken` opcional no payload
- Validação `apiToken` (quando presente e não-null): tamanho 1..4096
- 409 id duplicado
- 400 baseUrl inválida
- 400 apiToken inválido (tamanho)
- 500 se `METRICS_SECRET_KEY` estiver ausente e for necessário persistir token

### PUT /connectors/{connectorId}
- Semântica de update para `apiToken`:
  - **omitido** => mantém token atual
  - **string** => substitui token
  - **null** => remove token
- Validação `apiToken` (quando presente e não-null): tamanho 1..4096
- 404 connectorId não encontrado
- 400 apiToken inválido (tamanho)
- 500 se `METRICS_SECRET_KEY` estiver ausente e for necessário criptografar/persistir token

---
## Preview

### POST /preview/transform
- Deve executar a mesma engine do runner.
- 400 para JSON inválido, DSL inválida, schema inválida.
- Resposta (ver schema `PreviewResult`):
  - `isValid=false` + `errors[]` em validação
  - `output` e `previewCsv` quando possível

---

## Idempotência (mínimo)
- POST não é idempotente
- PUT é idempotente (mesmo payload -> mesmo estado)
