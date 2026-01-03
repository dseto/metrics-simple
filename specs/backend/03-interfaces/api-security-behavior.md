# Delta — Interfaces: API Security Behavior (401/403/CORS)

## Regras gerais
1) **Tudo protegido por padrão**:
   - Todos os endpoints do deck base devem exigir auth,
     exceto `POST /api/auth/token` (quando LocalJwt).
2) Autenticação é **Bearer**.

## Status codes
- Sem token / token inválido / expirado => 401 (`AUTH_UNAUTHORIZED`)
- Token válido, sem permissão => 403 (`AUTH_FORBIDDEN`)
- Rate limiting / lockout => 429 (`AUTH_RATE_LIMITED`)

## CORS
- CORS deve permitir apenas origens explicitadas em configuração (`Auth.AllowedOrigins`).
- CORS não substitui autenticação.

## Matriz de permissões (aplicação nas rotas)
- Leitura e Preview => policy `Reader`
- CRUD (create/update/delete) => policy `Admin`
- AI generate:
  - default: Reader (design-time), ou Admin se você preferir restringir

## Swagger
- Dev: permitido
- Interno: se habilitar, exigir `Admin`

## Headers e correlação
- Respeitar `X-Correlation-Id` se enviado; senão gerar.
- Retornar `X-Correlation-Id` em todas as respostas.
