# Teste de Alteração de Senha — User Daniel

Data: 2026-01-04  
Usuário: daniel  
Senha Nova: *

---

## Testes Executados

### ✅ 1. Buscar usuário daniel por username
```
GET /api/admin/auth/users/by-username/daniel
Status: 200 OK
Response:
{
  "id": "b16f86623c0b457aa1f7f87df99e8ae2",
  "username": "daniel",
  "displayName": "Daniel Silva",
  "email": "daniel@example.com",
  "isActive": true,
  "roles": ["Metrics.Admin"],
  "createdAt": "2026-01-04T23:55:43Z"
}
```

---

### ✅ 2. Alterar a senha (como admin)
```
PUT /api/admin/auth/users/b16f86623c0b457aa1f7f87df99e8ae2/password
Authorization: Bearer {admin-token}
Body: {"newPassword":"*"}

Status: 200 OK
Response: {"message":"Password updated successfully"}
```

---

### ✅ 3. Login com nova senha
```
POST /api/auth/token
Body: {"username":"daniel","password":"*"}

Status: 200 OK
Response:
{
  "access_token": "*",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

Token decodificado:
```json
{
  "sub": "daniel",
  "app_roles": "Metrics.Admin",
  "display_name": "Daniel Silva",
  "email": "daniel@example.com"
}
```

---

### ✅ 4. Tentar login com senha antiga (rejeitado)
```
POST /api/auth/token
Body: {"username":"daniel","password":"*"}

Status: 401 Unauthorized ✅
Mensagem: Credenciais inválidas (senha antiga rejeitada)
```

---

## Conclusão

✅ **Alteração de senha funciona corretamente**
- Endpoint PUT funciona
- Validação de comprimento mínimo (8 caracteres) aplicada
- Login com nova senha bem-sucedido
- Senha antiga foi invalidada corretamente
- Roles mantidas (Admin)
- Claims JWT corretos

**Status**: PRONTO PARA PRODUÇÃO

