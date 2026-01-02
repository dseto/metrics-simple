
# Invariants (Backend)

Data: 2026-01-01

## I-001: Referência válida de connector
Para todo `Process` existente:
- deve existir `Connector` com `id == process.connectorId`.

## I-002: Process ativo deve ter destino de saída válido
Para todo `Process` com `status=Active`:
- `outputDestinations` não vazio e cada destino válido.

## I-003: ProcessVersion pertence a um Process existente
Para todo `ProcessVersion` existente:
- deve existir o Process correspondente.

## I-004: executionId deve existir em todos logs do Runner
Para toda execução do Runner:
- todos eventos MUST incluir `executionId` (ver `../07-observability/logging-schema.md`).
