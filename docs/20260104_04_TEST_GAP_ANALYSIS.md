# Test Gap Analysis — Auth API

Data: 2026-01-04

## 🔴 Problemas Encontrados em Produção

### 1. **Normalização case-insensitive de username**
- **Encontrado em**: Primeiro acesso à API
- **Sintoma**: `POST /api/admin/auth/users` retornava 409 CONFLICT incorretamente
- **Causa raiz**: Query SQL `WHERE LOWER(username) = @username` mas o C# passava `username.ToLowerInvariant()`
- **Teste que faltava**: Criar 2 usuários com mesmo username em case diferente (daniel vs DANIEL)

### 2. **Endpoint sem suporte a username**
- **Encontrado em**: Segunda chamada (GET por username)
- **Sintoma**: `GET /api/admin/auth/users/daniel` retornava 404
- **Causa raiz**: Endpoint esperava UUID, não username; faltava endpoint by-username
- **Teste que faltava**: Tentar buscar usuário recém-criado por username

### 3. **Autorização não testada**
- **Encontrado em**: Terceira chamada
- **Sintoma**: User com role Reader tentou acessar endpoint Admin
- **Teste que faltava**: Testar controle de acesso baseado em roles

---

## 🔍 Cobertura Atual de Testes

### ✅ O que já tem

```
tests/
├── Contracts.Tests/          ← Validam schemas OpenAPI
├── Engine.Tests/             ← Validam transformação JSON
└── Integration.Tests/
    ├── IT01_CrudPersistenceTests.cs   ← CRUD básico (SEM AUTH)
    ├── IT02_EndToEndRunnerTests.cs    ← Runner CLI (SEM AUTH)
    ├── IT03_SourceFailureTests.cs     ← Falhas (SEM AUTH)
    ├── IT04_AiDslGenerateTests.cs     ← AI (SEM AUTH)
    ├── IT05_RealLlmIntegrationTests.cs ← LLM (SEM AUTH)
    └── IT06_ConnectorApiTokenTests.cs ← Connector (SEM AUTH)
```

**PROBLEMA**: Todos os testes desabilitam autenticação por padrão!

```csharp
public TestWebApplicationFactory(string dbPath, bool disableAuth = true) // ← AUTH DESABILITADA!
```

### ❌ O que falta

| Cenário | Cobertura | Criticidade |
|---------|-----------|-------------|
| **Auth: Login com credenciais corretas** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Login com credenciais incorretas** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Criar usuário duplicado** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Case-insensitive username** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Validação de senha (min 8 chars)** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Alterar senha do usuário** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Controle de acesso por role** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Endpoint por username** | ❌ Nenhum teste | 🔴 CRÍTICO |
| **Auth: Usuário inativo não faz login** | ❌ Nenhum teste | 🟠 ALTO |
| **Auth: Lockout por tentativas falhadas** | ❌ Nenhum teste | 🟠 ALTO |
| **Auth: JWT claim normalization** | ❌ Nenhum teste | 🟠 ALTO |

---

## 📋 Plano de Testes Robusto

### IT07_AuthenticationTests.cs (NOVO)

**Objetivo**: Validar toda a pipeline de autenticação LocalJwt

```csharp
public class IT07_AuthenticationTests
{
    // Token Endpoint
    [Fact] public async Task Token_WithValidCredentials_Returns200()
    [Fact] public async Task Token_WithInvalidPassword_Returns401()
    [Fact] public async Task Token_WithNonExistentUser_Returns401()
    [Fact] public async Task Token_WithInactiveUser_Returns401()
    [Fact] public async Task Token_WithLockedUser_Returns429()
    [Fact] public async Task Token_WithEmptyUsername_Returns400()
    [Fact] public async Task Token_WithEmptyPassword_Returns400()
    [Fact] public async Task Token_JwtClaimsCorrect()
    
    // Password Validation
    [Fact] public async Task Token_WithPasswordLessThan8Chars_Fails()
    [Fact] public async Task Token_PasswordCaseSensitive()
}
```

### IT08_UserManagementTests.cs (NOVO)

**Objetivo**: Validar CRUD de usuários com autorização

```csharp
public class IT08_UserManagementTests
{
    // Create User
    [Fact] public async Task CreateUser_WithValidData_Returns201()
    [Fact] public async Task CreateUser_WithDuplicateUsername_Returns409()
    [Fact] public async Task CreateUser_CaseInsensitiveDuplicate_Returns409()
    [Fact] public async Task CreateUser_WithPasswordUnder8Chars_Returns400()
    [Fact] public async Task CreateUser_WithoutAdminRole_Returns403()
    [Fact] public async Task CreateUser_ResponseHasCorrectFields()
    
    // Get User by ID
    [Fact] public async Task GetUserById_WithValidId_Returns200()
    [Fact] public async Task GetUserById_WithInvalidId_Returns404()
    [Fact] public async Task GetUserById_WithoutAdminRole_Returns403()
    [Fact] public async Task GetUserById_DoesNotReturnPasswordHash()
    
    // Get User by Username (NEW)
    [Fact] public async Task GetUserByUsername_WithValidUsername_Returns200()
    [Fact] public async Task GetUserByUsername_WithInvalidUsername_Returns404()
    [Fact] public async Task GetUserByUsername_CaseInsensitive_Works()
    [Fact] public async Task GetUserByUsername_WithoutAdminRole_Returns403()
    
    // Update User
    [Fact] public async Task UpdateUser_ChangesDisplayName_Returns200()
    [Fact] public async Task UpdateUser_ChangesRoles_Returns200()
    [Fact] public async Task UpdateUser_DeactivatesUser_Returns200()
    [Fact] public async Task UpdateUser_WithoutAdminRole_Returns403()
    [Fact] public async Task UpdateUser_NonexistentUser_Returns404()
    
    // Change Password
    [Fact] public async Task ChangePassword_WithValidPassword_Returns200()
    [Fact] public async Task ChangePassword_WithPasswordUnder8Chars_Returns400()
    [Fact] public async Task ChangePassword_InvalidatesOldPassword()
    [Fact] public async Task ChangePassword_WithoutAdminRole_Returns403()
    [Fact] public async Task ChangePassword_NonexistentUser_Returns404()
}
```

### IT09_AuthorizationTests.cs (NOVO)

**Objetivo**: Validar controle de acesso baseado em roles

```csharp
public class IT09_AuthorizationTests
{
    // Reader Role
    [Fact] public async Task ReaderRole_CanAccess_GETEndpoints()
    [Fact] public async Task ReaderRole_CannotAccess_POSTEndpoints()
    [Fact] public async Task ReaderRole_CannotAccess_AdminAuthEndpoints()
    [Fact] public async Task ReaderRole_CanAccess_ApiAuthMe()
    
    // Admin Role
    [Fact] public async Task AdminRole_CanAccess_AllEndpoints()
    [Fact] public async Task AdminRole_CanManageUsers()
    
    // No Auth
    [Fact] public async Task NoAuth_CanAccess_HealthCheck()
    [Fact] public async Task NoAuth_CanAccess_TokenEndpoint()
    [Fact] public async Task NoAuth_CannotAccess_ProtectedEndpoints()
}
```

---

## 🎯 Checklist de Implementação

- [ ] Criar `IT07_AuthenticationTests.cs`
  - [ ] Token endpoint (sucesso e erros)
  - [ ] Password validation
  - [ ] JWT claims
- [ ] Criar `IT08_UserManagementTests.cs`
  - [ ] Create/Get/Update/Delete
  - [ ] Case-insensitive validation
  - [ ] Autorização
- [ ] Criar `IT09_AuthorizationTests.cs`
  - [ ] Role-based access control
  - [ ] Endpoint protection
- [ ] Habilitar auth nos testes
  - [ ] Ajustar `TestWebApplicationFactory.WithAuth()`
  - [ ] Helper para obter token em testes
- [ ] Adicionar testes parametrizados
  - [ ] Múltiplas permutações de entrada
  - [ ] Edge cases (strings vazias, null, etc.)
- [ ] Documentar falhas comuns
  - [ ] Quais erros esperar onde
  - [ ] Como diagnosticar problemas

---

## 📊 Impacto

| Métrica | Antes | Depois | Melhoria |
|---------|-------|--------|----------|
| Testes de Auth | 0 | 30+ | ∞ |
| Cobertura com Auth | 0% | 95%+ | ∞ |
| Bugs descobertos em Produção | 3 | Esperado: 0 | ✅ |

---

## 🔗 Referências

- [Backend Integration Tests Spec](../specs/backend/09-testing/integration-tests.md)
- [Auth Domain Spec](../specs/backend/02-domain/auth-domain.md)
- [Auth API Spec](../specs/backend/03-interfaces/auth-api.md)

