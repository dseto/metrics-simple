# Delta — Observability: Audit logging por request

## Evento obrigatório: ApiRequestCompleted
Emitir 1 evento por request, incluindo 401/403/429/5xx.

Campos mínimos:
- `eventName`: ApiRequestCompleted
- `correlationId`
- `authMode`
- `actorSub` (sub) — null/empty se anônimo
- `actorRoles` (app_roles) — vazio se anônimo
- `tokenId` (jti) — null se anônimo
- `method`, `path`
- `statusCode`
- `durationMs`

## Regras
- **Nunca** logar header `Authorization` nem senha.
- Para payload potencialmente sensível (ex.: sampleInput), logar apenas:
  - tamanho
  - hash (opcional)

## Uso para métricas (metrics-lite)
A partir desses logs é possível derivar:
- req/min por endpoint
- req/min por usuário
- taxa de 401/403
- p95/p99 de duração por endpoint
