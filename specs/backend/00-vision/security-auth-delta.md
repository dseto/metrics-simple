# Delta — Segurança + AuthN/AuthZ (LocalJwt SQLite -> Okta/Entra-ready)

## Objetivos
1) Bloquear acesso anônimo à API (exceto login) com **Bearer JWT**.
2) Permitir **identidade individual** (saber “quem chamou o quê”) e perfis:
   - `Metrics.Admin`
   - `Metrics.Reader`
3) Persistir usuários e roles no **SQLite** (não em config).
4) Manter compatibilidade futura com **Okta/Entra** com impacto mínimo:
   - trocar `Auth.Mode` para `ExternalOidc`
   - ajustar `Authority/Audience`
   - ajustar mapeamento de claims para `app_roles`

## Não objetivos
- MFA, refresh token, reset de senha por e-mail.
- SSO corporativo agora (Okta/Entra).
- Hardening avançado (WAF, mTLS, zero-trust).

## Risco aceito (explícito)
- Credenciais locais existem (hasheadas).
- Sem MFA.
- CORS não protege chamadas fora do navegador.

## Mitigações mínimas obrigatórias
- Hash forte (BCrypt recomendado).
- Expiração curta do token (default: 60 min).
- Lockout básico por falhas (default: 5 falhas => 5 min).
- Rate limiting no endpoint de login.
- Audit logging por request (sub/roles/jti/path/status/duration).
- HTTPS ao menos no gateway/proxy interno.

## Decisões de design
### Auth.Mode (novo)
- `LocalJwt` (agora): API emite token via `/api/auth/token` e valida HS256 com SigningKey local.
- `ExternalOidc` (futuro): API valida token via OIDC discovery/JWKS (Okta/Entra). Não emite token.
- `Off` (opcional, apenas dev): só se explicitamente habilitado.

### Claim interna padrão (chave para migração)
- `app_roles`: **única** fonte para policies e frontend.
- Normalização de claims deve preencher `app_roles` quando o token vier com `roles` ou `groups`.

## Entradas do delta (arquivos)
- Domain: `../02-domain/auth-domain.md`
- Interfaces: `../03-interfaces/auth-api.md`, `../03-interfaces/auth-error-codes.md`, `../03-interfaces/api-security-behavior.md`
- Execution: `../04-execution/auth-middleware-pipeline.md`
- Storage: `../06-storage/sqlite-auth-schema.md` + `../06-storage/migrations/001_auth_users.sql`
- Observability: `../07-observability/audit-logging.md`
- Testing: `../09-testing/security-auth-tests.md`
