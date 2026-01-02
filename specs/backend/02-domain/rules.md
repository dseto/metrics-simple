
# Domain Rules (Backend)

Data: 2026-01-01

Este documento descreve **regras de domínio** que não são totalmente capturadas apenas por JSON Schema.
Os contratos estruturais (DTOs/schemas) ficam em:
- `../../shared/domain/schemas/*.schema.json`

> Regra: o backend deve **validar** estas regras e retornar erros padronizados (ver `../03-interfaces/error-contract.md`).

---

## Entities
- Process
- ProcessVersion
- Connector

---

## Regras — Process

### R-P-001: `Process.id` é único
- **Descrição:** não podem existir dois Processes com o mesmo `id`.
- **Violação:** ao criar com `id` já existente, retornar **409 Conflict**.
- **Erro (code):** `PROCESS_ID_CONFLICT`
- **Mensagem:** `A process with id '{id}' already exists.`

### R-P-002: `Process.name` obrigatório
- **Descrição:** `name` não pode ser vazio após trim.
- **Violação:** **400 Bad Request**
- **Erro (code):** `PROCESS_NAME_REQUIRED`
- **Mensagem:** `Process name is required.`

### R-P-003: `Process.connectorId` deve existir
- **Descrição:** `connectorId` deve referenciar um Connector existente.
- **Violação:** **422 Unprocessable Entity**
- **Erro (code):** `PROCESS_CONNECTOR_NOT_FOUND`
- **Mensagem:** `Connector '{connectorId}' was not found.`

### R-P-004: `outputDestinations` deve ter ao menos 1 destino válido
- **Descrição:** array não vazio; cada destino deve cumprir regras de configuração.
- **Violação:** **400 Bad Request**
- **Erro (code):** `PROCESS_OUTPUT_DEST_INVALID`
- **Mensagem:** `At least one valid output destination is required.`

---

## Regras — ProcessVersion

### R-V-001: `version` é único por `processId`
- **Descrição:** não pode existir a mesma version para o mesmo Process.
- **Violação:** **409 Conflict**
- **Erro (code):** `PROCESS_VERSION_CONFLICT`
- **Mensagem:** `Version '{version}' already exists for process '{processId}'.`

### R-V-002: `sourceRequest.method` e `sourceRequest.path` obrigatórios
- **Descrição:** método deve ser um dos suportados; path não vazio após trim.
- **Violação:** **400 Bad Request**
- **Erro (code):** `SOURCE_REQUEST_INVALID`

### R-V-003: DSL obrigatória e perfil suportado
- **Descrição:** `dsl.profile` ∈ {jsonata, jmespath, custom} e `dsl.text` não vazio.
- **Violação:** **400 Bad Request**
- **Erro (code):** `DSL_INVALID`

### R-V-004: `outputSchema` deve ser JSON objeto/array e schema válido
- **Descrição:** `outputSchema` precisa ser parseável e validável por JSON Schema draft suportado.
- **Violação:** **400 Bad Request**
- **Erro (code):** `OUTPUT_SCHEMA_INVALID`

### R-V-005: `enabled=true` em version exige Process existente
- **Descrição:** não pode criar version para Process inexistente.
- **Violação:** **404 Not Found**
- **Erro (code):** `PROCESS_NOT_FOUND`

---

## Regras — Connector

### R-C-001: `Connector.id` é único
- Violação: 409
- code: `CONNECTOR_ID_CONFLICT`

### R-C-002: `baseUrl` deve ser URL válida
- Violação: 400
- code: `CONNECTOR_BASEURL_INVALID`

### R-C-003: `timeoutSeconds` >= 1
- Violação: 400
- code: `CONNECTOR_TIMEOUT_INVALID`

---

## Semântica de Delete

### D-001: Delete de Process
- **Opção padrão:** bloquear delete se existir ProcessVersion associada.
- **Violação:** 409 Conflict
- **code:** `PROCESS_DELETE_HAS_VERSIONS`
- **Mensagem:** `Process '{id}' cannot be deleted because it has versions.`

> Se você quiser permitir cascade delete no futuro, isso deve ser uma ADR.

---

## Concorrência / idempotência (mínimo)
- PUT é **substituição** do recurso (last-write-wins).
- Não há ETag nesta versão simples.
