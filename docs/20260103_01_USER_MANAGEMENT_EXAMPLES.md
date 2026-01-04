# User Management API Examples

## Resumo
A API de gerenciamento de usuários permite que administradores criem, atualizem e gerenciem usuários através de endpoints RESTful. Todos os endpoints de admin requerem autenticação com role `Metrics.Admin`.

## Setup

### 1. Obter Token de Admin

Primeiro, faça login com o usuário admin bootstrap:

```bash
curl -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "ChangeMe123!"
  }'
```

**Resposta (201 Created):**
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhZG1pbjEyMyIsImFwcF9yb2xlcyI6WyJNZXRyaWNzLkFkbWluIl0sImRpc3BsYXlfbmFtZSI6IkFkbWluIiwiZXhwIjoxNzMwNzcxMDAwLCJpc3MiOiJNZXRyaWNzU2ltcGxlIiwiYXVkIjoiTWV0cmljc1NpbXBsZS5BcGkifQ...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

Armazene o `access_token` para usar nos próximos comandos. Para os exemplos abaixo, use:
```bash
ADMIN_TOKEN="<seu_access_token_aqui>"
```

---

## Endpoints de User Management

### POST /api/admin/auth/users - Criar Novo Usuário

**Descrição:** Cria um novo usuário com username, password, displayName opcional, email opcional e roles.

**Requisição:**
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
| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| username | string | Sim | Username único (será usado para login) |
| password | string | Sim | Senha (mínimo 8 caracteres) |
| displayName | string | Não | Nome para exibição |
| email | string | Não | Email do usuário |
| roles | string[] | Não | Array de roles. Padrão: ["Metrics.Reader"]. Valores: "Metrics.Admin", "Metrics.Reader" |

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

**Erros Possíveis:**
- `400 Bad Request` - Username ou password vazios, ou password com menos de 8 caracteres
- `409 Conflict` - Username já existe
- `403 Forbidden` - Usuário não tem role Admin

---

### Exemplo: Criar Usuário com Role Admin

```bash
curl -X POST http://localhost:8080/api/admin/auth/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "carlos_admin",
    "password": "AdminPass999!",
    "displayName": "Carlos Oliveira",
    "email": "carlos@company.com",
    "roles": ["Metrics.Admin", "Metrics.Reader"]
  }'
```

---

### GET /api/admin/auth/users/{userId} - Obter Detalhes do Usuário

**Descrição:** Obtém detalhes completos de um usuário específico.

**Requisição:**
```bash
curl -X GET http://localhost:8080/api/admin/auth/users/f47ac10b58cc4372a5670e4a9b61b830 \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

**Resposta (200 OK):**
```json
{
  "id": "f47ac10b58cc4372a5670e4a9b61b830",
  "username": "daniel",
  "displayName": "Daniel Silva",
  "email": "daniel@example.com",
  "isActive": true,
  "roles": ["Metrics.Reader"],
  "createdAt": "2026-01-03T21:30:00Z",
  "lastLogin": null
}
```

**Erros Possíveis:**
- `404 Not Found` - Usuário não existe
- `403 Forbidden` - Usuário não tem role Admin

---

### PUT /api/admin/auth/users/{userId} - Atualizar Perfil e Roles do Usuário

**Descrição:** Atualiza displayName, email, isActive status e roles de um usuário.

**Requisição:**
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

**Parâmetros:**
| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| displayName | string | Não | Novo nome para exibição |
| email | string | Não | Novo email |
| isActive | boolean | Não | Ativar/desativar usuário |
| roles | string[] | Não | Novas roles (sobrescreve as anteriores) |

**Resposta (200 OK):**
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

**Erros Possíveis:**
- `404 Not Found` - Usuário não existe
- `403 Forbidden` - Usuário não tem role Admin

---

### PUT /api/admin/auth/users/{userId}/password - Trocar Senha do Usuário

**Descrição:** Altera a senha de um usuário. Redefine failed_attempts e lockout.

**Requisição:**
```bash
curl -X PUT http://localhost:8080/api/admin/auth/users/f47ac10b58cc4372a5670e4a9b61b830/password \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "newPassword": "NewSecurePass456!"
  }'
```

**Parâmetros:**
| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| newPassword | string | Sim | Nova senha (mínimo 8 caracteres) |

**Resposta (200 OK):**
```json
{
  "message": "Password updated successfully"
}
```

**Erros Possíveis:**
- `400 Bad Request` - Password vazio ou com menos de 8 caracteres
- `404 Not Found` - Usuário não existe
- `403 Forbidden` - Usuário não tem role Admin

---

## Fluxo Completo de Exemplo

### 1. Fazer Login (obter token admin)
```bash
curl -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username": "admin", "password": "ChangeMe123!"}'
```

### 2. Criar novo usuário
```bash
ADMIN_TOKEN="seu_token_aqui"

curl -X POST http://localhost:8080/api/admin/auth/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "username": "joao",
    "password": "JoaoPass2024!",
    "displayName": "João Santos",
    "email": "joao@company.com",
    "roles": ["Metrics.Reader"]
  }'
```

Resposta conterá o `id` do novo usuário.

### 3. Fazer login como novo usuário
```bash
curl -X POST http://localhost:8080/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username": "joao", "password": "JoaoPass2024!"}'
```

### 4. Acessar endpoint protegido com novo usuário
```bash
NEW_USER_TOKEN="token_do_joao"

curl -X GET http://localhost:8080/api/auth/me \
  -H "Authorization: Bearer $NEW_USER_TOKEN"
```

Resposta:
```json
{
  "sub": "joao",
  "roles": ["Metrics.Reader"],
  "displayName": "João Santos",
  "email": "joao@company.com"
}
```

### 5. Admin atualiza roles do novo usuário
```bash
USER_ID="id_do_joao"

curl -X PUT http://localhost:8080/api/admin/auth/users/$USER_ID \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "roles": ["Metrics.Admin", "Metrics.Reader"]
  }'
```

---

## Validações e Regras

### Password Requirements
- Mínimo 8 caracteres
- Recomendado: misture letras maiúsculas, minúsculas, números e símbolos

### Username
- Deve ser único
- Case-sensitive
- Sem espaços (recomendado: letras, números, underscore, hífen)

### Roles
Roles disponíveis:
- `Metrics.Reader` - Pode ler dados de métricas
- `Metrics.Admin` - Pode gerenciar usuários e executar operações administrativas

### Email (Opcional)
- Se fornecido, será armazenado para referência
- Não há validação de unicidade no email

---

## Códigos de Erro

| Status | Código | Descrição |
|--------|--------|-----------|
| 400 | BAD_REQUEST | Parâmetros inválidos (username/password vazios, password curta) |
| 401 | AUTH_UNAUTHORIZED | Token não fornecido ou inválido |
| 403 | AUTH_FORBIDDEN | Usuário não tem permissão (não tem role Admin) |
| 404 | - | Recurso não encontrado |
| 409 | CONFLICT | Username já existe |
| 500 | - | Erro interno do servidor |

---

## Armazenamento em SQLite

Todos os usuários criados via API são armazenados em:
- **Tabela:** `auth_users`
- **Campos:** id, username, display_name, email, password_hash (BCrypt), is_active, failed_attempts, lockout_until_utc, created_at_utc, updated_at_utc, last_login_utc
- **Roles:** Armazenadas em tabela relacional `auth_user_roles`
- **Segurança:** Passwords são hasheadas com BCrypt (WorkFactor=12), nunca armazenadas em plaintext

---

## Notas de Segurança

1. **Mude a senha do admin na primeira execução:** A senha padrão é `ChangeMe123!`
2. **Use HTTPS em produção:** Os exemplos usam http://localhost para desenvolvimento
3. **Tokens JWT:** Expiram em 1 hora por padrão. Configure via `Auth.AccessTokenMinutes` em appsettings.json
4. **Auditoria:** Todas as operações de criação/atualização de usuários são logadas com correlationId para rastreamento
5. **Rate Limiting:** Login limitado a 10 tentativas por minuto. Endpoints autenticados: 120 por minuto

---

## Troubleshooting

### "User already exists"
Verifique se o username já foi criado. Use um username diferente ou delete o usuário anterior.

### "Password must be at least 8 characters"
Aumente o comprimento da senha. Exemplo: `SecurePass123!` (14 caracteres)

### "Access denied" (403)
Verifique se você está usando um token de um usuário com role `Metrics.Admin`. Use o token do admin bootstrap.

### "Too many requests" (429)
Você excedeu o rate limit. Aguarde 1 minuto e tente novamente.
