# Delta — Interfaces: Error codes (Auth)

Adicionar ao catálogo de `ApiError.code`:

- `AUTH_UNAUTHORIZED` => 401
- `AUTH_FORBIDDEN` => 403
- `AUTH_RATE_LIMITED` => 429
- `AUTH_DISABLED` => 503 (opcional, quando endpoint não existe no modo atual)

Todas as respostas devem incluir:
- `correlationId` (do `X-Correlation-Id` ou gerado)

E retornar header:
- `X-Correlation-Id` em todas as respostas, inclusive 401/403/429.
