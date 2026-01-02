# Requisitos (Backend)

Data: 2026-01-02

## Objetivo
Implementar o **Config API** e o **Runner CLI** para executar exportação de métricas (sync), conforme contratos do deck `shared`.

## Obrigatórios
- CRUD de `Connector` e `Process` via HTTP (`/api/connectors`, `/api/processes`).
- CRUD de `ProcessVersion` (create/get/update) via HTTP (`/api/processes/{id}/versions`).
- `POST /api/preview/transform`: executar FetchSource (mockado ou real), Transform (DSL), validar schema, gerar preview CSV.
- Runner CLI sync:
  - `run --processId ... [--version ...]` para executar pipeline end-to-end e escrever CSV.
  - `validate` e `cleanup` conforme contrato.
- Persistência em SQLite conforme `06-storage/sqlite-schema.md`.

## Não permitidos (v1.x)
- Filas / orquestração assíncrona
- Azure Functions / timers / triggers
## Testes (obrigatório)
- Contracts + Golden + **Integration (E2E)**.
- Integration tests devem cobrir:
  - API via WebApplicationFactory
  - FetchSource via HTTP (mock server)
  - Runner CLI executado como processo real
  - SQLite real (arquivo temporário)

Ver: `../09-testing/integration-tests.md`.

