# Delta — Domain: Auth (usuários, roles, claims)

## Conceitos
### AuthUser (persistido)
Representa um usuário local para autenticação `LocalJwt`.

Campos mínimos (ver storage):
- `id` (GUID/ULID string)
- `username` (único, vira `sub`)
- `displayName`, `email` (opcionais)
- `passwordHash` (BCrypt)
- `isActive` (bool)
- `failedAttempts` (int)
- `lockoutUntilUtc` (datetime ISO8601 UTC, opcional)
- `lastLoginUtc` (opcional)

### Role (do app)
Valores suportados:
- `Metrics.Admin`
- `Metrics.Reader`

## Invariantes
1) `username` é estável e único.
2) `passwordHash` nunca é texto puro.
3) `isActive=false` bloqueia login.
4) Lockout:
   - se `lockoutUntilUtc` > now => login bloqueado (429).
5) Roles do usuário devem ser subset de {Admin, Reader}.

## Claim interna padrão
- `app_roles` (lista)
- `sub` = username
- `jti` = id único do token

## Normalização de claims (para Okta/Entra)
Regra: sempre garantir `app_roles` no `ClaimsPrincipal`.
Ordem:
1) Se já existe `app_roles` => usar.
2) Se existe `roles` => mapear 1:1 para `app_roles`.
3) Se existe `groups` => mapear via configuração `GroupToRole`.
