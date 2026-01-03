# Delta — Storage: SQLite schema (Auth)

## Novas tabelas
### auth_users
Armazena usuários locais (LocalJwt).

Campos:
- id (PK)
- username (unique) => vira `sub`
- display_name, email (opcionais)
- password_hash (BCrypt)
- is_active (0/1)
- failed_attempts
- lockout_until_utc (ISO8601 UTC)
- created_at_utc, updated_at_utc, last_login_utc

### auth_user_roles
- user_id (FK auth_users.id)
- role (Metrics.Admin | Metrics.Reader)
- PK (user_id, role)

## Migração
Aplicar migration:
- `migrations/001_auth_users.sql`

## Bootstrap admin (recomendado)
Se `EnableBootstrapAdmin=true` e não existe nenhum Admin:
- criar usuário admin inicial (com BCrypt)
- atribuir `Metrics.Admin`
- logar WARNING e recomendar desligar bootstrap após primeiro uso.
