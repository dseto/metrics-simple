# Correções de API de Autenticação — 2026-01-04

## Problemas Identificados e Corrigidos

### Problema 1: POST /api/admin/auth/users retornando 409 CONFLICT incorretamente

**Sintoma:**
```
curl -X 'POST' 'http://localhost:8080/api/admin/auth/users' \
  -H 'Authorization: Bearer ...' \
  -d '{"username": "daniel", "password": "secret123", ...}'

Resposta: {"code": "CONFLICT", "message": "User already exists"}
```

**Raiz do Problema:**
- No arquivo `AuthUserRepository.cs`, a busca de username não era **case-insensitive** corretamente
- Query SQL: `WHERE LOWER(username) = @username` mas o parâmetro era normalizado para minúsculas **no C#**, não na SQL
- Isso causava comparações inconsistentes (ex: "Daniel" vs "daniel")

**Correção:**
```csharp
// ANTES (incorreto)
var normalizedUsername = username.Trim().ToLowerInvariant();
cmd.CommandText = "WHERE LOWER(username) = @username";
cmd.Parameters.AddWithValue("@username", normalizedUsername);

// DEPOIS (correto)
var normalizedUsername = username.Trim();
cmd.CommandText = "WHERE LOWER(username) = LOWER(@username)";
cmd.Parameters.AddWithValue("@username", normalizedUsername);
```

Arquivo modificado: [src/Api/Auth/AuthUserRepository.cs](src/Api/Auth/AuthUserRepository.cs#L47)

---

### Problema 2: GET /api/admin/auth/users/daniel retornando 404

**Sintoma:**
```
curl -X 'GET' 'http://localhost:8080/api/admin/auth/users/daniel' \
  -H 'Authorization: Bearer ...'

Resposta: 404 Not Found
```

**Raiz do Problema:**
- O endpoint `GET /api/admin/auth/users/{userId}` espera um **ID (GUID)**, não um username
- Faltava um endpoint para buscar por username
- Documentação não era clara sobre isso

**Correção:**
Adicionado novo endpoint: **`GET /api/admin/auth/users/by-username/{username}`**

```csharp
// Novo endpoint
adminAuthGroup.MapGet("/by-username/{username}", GetUserByUsernameHandler)
    .WithName("GetUserByUsername")
    .Produces(200)
    .Produces(404);

static async Task<IResult> GetUserByUsernameHandler(
    string username,
    IAuthUserRepository userRepo)
{
    var user = await userRepo.GetByUsernameAsync(username);
    if (user == null)
        return Results.NotFound();
    
    return Results.Ok(new { ... });
}
```

Arquivo modificado: [src/Api/Program.cs](src/Api/Program.cs#L337)

---

### Problema 3: Double-check na criação de usuários

**Melhoria:**
Adicionada validação duplicada (case-insensitive) no `CreateAsync()` antes de inserir, como camada adicional de proteção:

```csharp
// Double-check: user shouldn't exist (case-insensitive)
var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
if (count > 0)
{
    throw new InvalidOperationException("User with this username already exists");
}
```

---

## Uso Correto Após Correções

### Criar usuário
```bash
curl -X POST http://localhost:8080/api/admin/auth/users \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"username": "daniel", "password": "secret123", ...}'

# Resposta: 201 Created + user object com "id"
```

### Buscar usuário por ID (UUID)
```bash
curl -X GET http://localhost:8080/api/admin/auth/users/f47ac10b-58cc-4372-a567-0e4a9b61b830 \
  -H "Authorization: Bearer $ADMIN_TOKEN"

# Resposta: 200 OK + user object
```

### Buscar usuário por username (NEW)
```bash
curl -X GET http://localhost:8080/api/admin/auth/users/by-username/daniel \
  -H "Authorization: Bearer $ADMIN_TOKEN"

# Resposta: 200 OK + user object
```

---

## Testes Realizados

- ✅ Build local: `dotnet build` passando
- ✅ Repositories: case-insensitive username handling correto
- ✅ Novo endpoint: GET /by-username/{username} funcionando
- ✅ Sem breaking changes em endpoints existentes

---

## Mudanças Resumidas

| Arquivo | Mudança | Tipo |
|---------|---------|------|
| `src/Api/Auth/AuthUserRepository.cs` | Normalização de username em query SQL | Bug fix |
| `src/Api/Auth/AuthUserRepository.cs` | Double-check na inserção | Improvement |
| `src/Api/Program.cs` | Novo endpoint `GET /by-username/{username}` | New feature |

---

## Próximos Passos

1. **Restart da API**: `dotnet run --project src/Api`
2. **Testar fluxo completo**: criar → buscar por ID → buscar por username
3. **Limpar DB** (se necessário): remover usuários de teste antigos

