# Correlation (correlationId / executionId)

Data: 2026-01-02

- `correlationId` via header `X-Correlation-Id` (API/preview).
- Se ausente, o backend gera um `correlationId` e o inclui no `ApiError` e nos logs.
- `executionId` sempre gerado no runner (execução end-to-end).
- Preview pode ter `correlationId` sem `executionId`.
- Runner pode opcionalmente carregar `correlationId` se informado (ex.: CLI flag futura).
