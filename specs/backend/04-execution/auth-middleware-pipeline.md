# Delta — Execution: pipeline/middlewares (auth, rate limit, audit)

## Ordem recomendada (Minimal API)
1) CorrelationId middleware (ler/gerar `X-Correlation-Id`)
2) Exception handling (transformar em ApiError quando aplicável)
3) CORS
4) Authentication (`UseAuthentication`)
5) Authorization (`UseAuthorization`)
6) Rate limiting (por endpoint/route)
7) Audit logging middleware (ApiRequestCompleted)
8) Endpoints

> Observação: alguns frameworks posicionam rate limiting antes/after auth.
Recomendação deste delta:
- rate limit do login pode ser aplicado antes de auth (login é anônimo)
- rate limit geral por `sub` requer auth (aplicar após auth)

## Auth.Mode
- LocalJwt: validar HS256 e emitir token via endpoint de auth
- ExternalOidc: validar via Authority/JWKS (sem emissão)

## Shaping de erros 401/403/429
- Garantir retorno em formato `ApiError` com correlationId
- Não vazar detalhes do motivo de falha de token
