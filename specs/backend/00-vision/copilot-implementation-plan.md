# GitHub Copilot — Plano de implementação (Delta: Segurança + Auth)

> Use este arquivo como roteiro para Copilot Chat/Agent aplicar o delta no repositório.

## Prompt pronto (copiar/colar no Copilot)
Você é um engenheiro sênior .NET (Minimal API) + Angular.
Implemente o delta spec "Segurança + AuthN/AuthZ (LocalJwt em SQLite, Okta/Entra-ready)" conforme os arquivos em:
`scripts/specs/backend/` deste delta pack.

Requisitos:
1) Adicionar migrations SQLite para `auth_users` e `auth_user_roles` (ver `06-storage/migrations/001_auth_users.sql`).
2) Implementar `Auth.Mode` com `LocalJwt` e `ExternalOidc` (ExternalOidc pode ficar apenas configurável, sem integração completa agora).
3) Implementar endpoint `POST /api/auth/token` com:
   - validação BCrypt
   - lockout (5 falhas => 5 min, configurável)
   - rate limiting (ver spec)
   - retorno `{access_token, token_type, expires_in}`
4) Implementar claim normalization para preencher `app_roles` quando token vier com `roles` ou `groups` (mapping por config).
5) Implementar policies `Reader` e `Admin` baseadas em `app_roles` e proteger rotas conforme `03-interfaces/api-security-behavior.md`.
6) Garantir 401/403/429 com `ApiError` (incluindo `correlationId`) e header `X-Correlation-Id` em todas as respostas.
7) Implementar audit logging por request (ver `07-observability/audit-logging.md`).
8) Ajustar o frontend Angular:
   - tela /login
   - sessionStorage do token
   - interceptor Bearer
   - guards Admin/Reader
9) Implementar testes mínimos (integração):
   - sem token => 401
   - token inválido => 401
   - reader em admin => 403
   - login ok => 200 com token
   - rate limit/lockout => 429

Valide cada etapa com build + run + curl.

## Checklist de validação rápida (manual)
- [ ] Rodar migrations e ver tabelas no SQLite
- [ ] Criar usuário admin (bootstrap ou admin endpoint)
- [ ] Login: obter token
- [ ] Chamar endpoint Reader: 200
- [ ] Chamar endpoint Admin com Reader: 403
- [ ] Sem token: 401
- [ ] Logs: contém sub/jti/app_roles/path/status/duration
- [ ] Login brute force: 429 (rate limit/lockout)
