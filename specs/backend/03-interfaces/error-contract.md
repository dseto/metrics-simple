# Error Contract (Backend)

Data: 2026-01-02

O backend deve retornar erros em um formato consistente (API) e também produzir erros consistentes (Runner).

Contratos canônicos em shared:
- OpenAPI: `../../shared/openapi/config-api.yaml`
- Schemas: `../../shared/domain/schemas/*.schema.json`

---

## API Error shape (canônico)
Ver `ApiError` em `../../shared/domain/schemas/apiError.schema.json`.

Exemplo:
```json
{
  "code": "PROCESS_ID_CONFLICT",
  "message": "A process with id 'p1' already exists.",
  "details": [
    { "path": "id", "message": "Duplicate id" }
  ],
  "correlationId": "c-01H...",
  "executionId": null
}
```

## HTTP status mapping (mínimo)
- 400: validação sintática / JSON inválido / request inválido
- 404: recurso não encontrado
- 409: conflito (id duplicado, delete bloqueado)
- 422: validação semântica (ex.: connector inexistente) *(opcional; backend pode usar 400 consistentemente)*
- 500: erro inesperado

## CorrelationId
- Aceitar `X-Correlation-Id` como header opcional.
- Se ausente, gerar um `correlationId` (padrão: ULID/UUID curto).
- Sempre retornar `correlationId` no corpo de erro (e, opcionalmente, também em header).

## RunnerError shape (para logs)
```json
{
  "errorCode": "SOURCE_HTTP_5XX",
  "message": "Source endpoint returned 503",
  "step": "FetchSource",
  "httpStatus": 503,
  "exceptionType": "HttpRequestException",
  "correlationId": "c-01H..."
}
```
