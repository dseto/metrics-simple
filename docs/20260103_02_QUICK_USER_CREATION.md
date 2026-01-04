# Como Criar e Gerenciar Usuários via API

## Resumo Rápido

A API fornece endpoints para criar usuários, alterar senhas e atualizar roles. Todos os endpoints de admin requerem autenticação com role `Metrics.Admin`.

## Exemplo Completo

### 1. Fazer Login como Admin

```bash
curl -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "ChangeMe123!"
  }'
```

**Resposta:**
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiI...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

Salve o `access_token` para usar nos próximos comandos:
```bash
export ADMIN_TOKEN="seu_token_aqui"
```

---

### 2. Criar Novo Usuário

**Endpoint:** `POST /api/admin/auth/users`

```bash
curl -X POST http://localhost:8080/api/admin/auth/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "daniel",
    "password": "SecurePass123!",
    "displayName": "Daniel Silva",
    "email": "daniel@example.com",
    "roles": ["Metrics.Reader"]
  }'
```

**Parâmetros:**
- `username` (obrigatório): username único para login
- `password` (obrigatório): mínimo 8 caracteres
- `displayName` (opcional): nome para exibição
- `email` (opcional): email do usuário
- `roles` (opcional): ["Metrics.Reader"] por padrão. Pode ser ["Metrics.Admin"] ou ["Metrics.Admin", "Metrics.Reader"]

**Resposta (201 Created):**
```json
{
  "id": "f47ac10b58cc4372a5670e4a9b61b830",
  "username": "daniel",
  "displayName": "Daniel Silva",
  "email": "daniel@example.com",
  "isActive": true,
  "roles": ["Metrics.Reader"],
  "createdAt": "2026-01-03T21:30:00Z"
}
```

---

### 3. Usuário Faz Login

O novo usuário pode fazer login normalmente:

```bash
curl -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "daniel",
    "password": "SecurePass123!"
  }'
```

---

### 4. Admin Altera Senha do Usuário

**Endpoint:** `PUT /api/admin/auth/users/{userId}/password`

```bash
curl -X PUT http://localhost:8080/api/admin/auth/users/f47ac10b58cc4372a5670e4a9b61b830/password \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "newPassword": "NewPassword456!"
  }'
```

**Resposta:**
```json
{
  "message": "Password updated successfully"
}
```

---

### 5. Admin Atualiza Roles e Perfil

**Endpoint:** `PUT /api/admin/auth/users/{userId}`

```bash
curl -X PUT http://localhost:8080/api/admin/auth/users/f47ac10b58cc4372a5670e4a9b61b830 \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "displayName": "Daniel Silva Updated",
    "email": "daniel.new@example.com",
    "isActive": true,
    "roles": ["Metrics.Admin", "Metrics.Reader"]
  }'
```

**Resposta:**
```json
{
  "id": "f47ac10b58cc4372a5670e4a9b61b830",
  "username": "daniel",
  "displayName": "Daniel Silva Updated",
  "email": "daniel.new@example.com",
  "isActive": true,
  "roles": ["Metrics.Admin", "Metrics.Reader"],
  "updatedAt": "2026-01-03T21:35:00Z"
}
```

---

### 6. Admin Obtém Detalhes do Usuário

**Endpoint:** `GET /api/admin/auth/users/{userId}`

```bash
curl -X GET http://localhost:8080/api/admin/auth/users/f47ac10b58cc4372a5670e4a9b61b830 \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Resposta:**
```json
{
  "id": "f47ac10b58cc4372a5670e4a9b61b830",
  "username": "daniel",
  "displayName": "Daniel Silva Updated",
  "email": "daniel.new@example.com",
  "isActive": true,
  "roles": ["Metrics.Admin", "Metrics.Reader"],
  "createdAt": "2026-01-03T21:30:00Z",
  "lastLogin": "2026-01-03T21:32:45Z"
}
```

---

## Exemplo com PowerShell

```powershell
# 1. Get token
$token = (Invoke-WebRequest -Uri http://localhost:8080/api/auth/token `
  -Method POST `
  -Headers @{"Content-Type"="application/json"} `
  -Body '{"username":"admin","password":"ChangeMe123!"}' `
  -UseBasicParsing).Content | ConvertFrom-Json | Select-Object -ExpandProperty access_token

# 2. Create user
$user = (Invoke-WebRequest -Uri http://localhost:8080/api/admin/auth/users `
  -Method POST `
  -Headers @{
    "Content-Type"="application/json"
    "Authorization"="Bearer $token"
  } `
  -Body @{
    username = "joao"
    password = "JoaoPass2024!"
    displayName = "João Santos"
    email = "joao@company.com"
    roles = @("Metrics.Reader")
  } | ConvertTo-Json `
  -UseBasicParsing).Content | ConvertFrom-Json

Write-Host "User created: $($user.username) (ID: $($user.id))"

# 3. Update user roles
$updated = (Invoke-WebRequest -Uri "http://localhost:8080/api/admin/auth/users/$($user.id)" `
  -Method PUT `
  -Headers @{
    "Content-Type"="application/json"
    "Authorization"="Bearer $token"
  } `
  -Body @{ roles = @("Metrics.Admin", "Metrics.Reader") } | ConvertTo-Json `
  -UseBasicParsing).Content | ConvertFrom-Json

Write-Host "User roles updated to: $($updated.roles -join ', ')"
```

---

## Erros Comuns

| Erro | Causa | Solução |
|------|-------|---------|
| 400 Bad Request | Username/password vazios ou password curta | Verifique username e password (mín. 8 chars) |
| 401 Unauthorized | Token inválido ou expirado | Obtenha novo token com credenciais admin |
| 403 Forbidden | Usuário não tem role Admin | Use token de um usuário admin |
| 409 Conflict | Username já existe | Use username diferente |
| 404 Not Found | Usuário não existe | Verifique o ID do usuário |

---

## Dados Armazenados

Todos os usuários são persistidos em SQLite em:
- **Arquivo:** `/app/config/config.db`
- **Tabelas:** 
  - `auth_users` - Dados do usuário
  - `auth_user_roles` - Roles do usuário

**Segurança:**
- Senhas hasheadas com BCrypt (WorkFactor=12)
- Nunca retornadas em respostas de API
- Cada operação auditada com correlationId

---

## Próximos Passos

1. **Mude a senha admin:** A senha padrão é `ChangeMe123!`
2. **Configure HTTPS:** Use `https://` em produção
3. **Veja logs:** `docker compose logs csharp-api`
4. **Teste endpoints:** Veja [USER_MANAGEMENT_EXAMPLES.md](USER_MANAGEMENT_EXAMPLES.md) para detalhes completos

