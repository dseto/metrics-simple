# API Versioning Compliance Checklist

**Data:** 2026-01-03  
**Objetivo:** Garantir que todos os componentes do sistema sigam o padrão de versionamento `/api/v1`

## ✅ Status Atual

### Backend (src/Api/Program.cs)
- [x] Health endpoint `/api/health` sem versionamento
- [x] Auth endpoints `/api/auth/*` sem versionamento
- [x] API v1 group criado: `var v1 = app.MapGroup("/api/v1")`
- [x] Processes endpoints sob v1: `/api/v1/processes`
- [x] ProcessVersions endpoints sob v1: `/api/v1/processes/{processId}/versions`
- [x] Connectors endpoints sob v1: `/api/v1/connectors`
- [x] Preview endpoint sob v1: `/api/v1/preview/transform`
- [x] AI endpoint sob v1: `/api/v1/ai/dsl/generate`
- [x] Location headers em `Created()` usam `/api/v1`

### OpenAPI Spec (specs/shared/openapi/config-api.yaml)
- [x] Base URL: `http://localhost:8080/api/v1`
- [x] Descrição documenta versionamento e exceções
- [x] Paths não incluem `/api/v1` (está no baseUrl)
- [x] Health endpoint documentado como exceção

### Documentação
- [x] `specs/shared/README.md` - Seção "Versionamento de API (CRITICAL)"
- [x] `docs/DECISIONS.md` - Decisão completa documentada (2026-01-03)
- [x] `docs/API_VERSIONING.md` - Guia técnico completo
- [x] `.github/agents/spec-driven-dockerizer.agent.md` - Seção enforcement

### Testes
- [x] `tests/Integration.Tests/*.cs` - Todas as URLs atualizadas para `/api/v1`
- [x] Build passando
- [ ] Testes de integração executando (em andamento)

### Docker
- [x] Containers rebuilds com código atualizado
- [x] API rodando em http://localhost:8080
- [x] Endpoints versionados acessíveis via CORS

## 🔍 Validação Manual

### Health Check (sem versão)
```bash
curl -i http://localhost:8080/api/health
# Expected: HTTP/1.1 200 OK + {"status":"ok"}
```

### Endpoints Versionados (com auth)
```bash
# Sem token → 401
curl -i http://localhost:8080/api/v1/processes
# Expected: HTTP/1.1 401 Unauthorized

# Com token válido → 200
curl -i http://localhost:8080/api/v1/processes \
  -H "Authorization: Bearer <token>"
# Expected: HTTP/1.1 200 OK + []
```

### CORS Preflight
```bash
curl -i -X OPTIONS http://localhost:8080/api/v1/processes \
  -H "Origin: http://localhost:4200" \
  -H "Access-Control-Request-Method: GET"
# Expected: HTTP/1.1 204 No Content
# Expected header: Access-Control-Allow-Origin: http://localhost:4200
```

## 📋 Checklist para Novos Endpoints

Ao adicionar um novo endpoint de negócio:

1. **Backend Implementation**
   - [ ] Endpoint usa `v1.MapGroup()` ou subgrupo de v1?
   - [ ] Location header em `201 Created` inclui `/api/v1`?
   - [ ] Tag apropriada definida (`.WithTags()`)?
   - [ ] Política de autorização definida (`.RequireAuthorization()`)?

2. **OpenAPI Spec**
   - [ ] Path adicionado em `config-api.yaml` (sem `/api/v1` no path)?
   - [ ] Request/Response schemas referenciados corretamente?
   - [ ] Tags consistentes com backend?
   - [ ] Status codes documentados?

3. **Testes**
   - [ ] Integration tests usam `/api/v1` nas URLs?
   - [ ] Testes validam auth quando aplicável?
   - [ ] Testes validam CORS quando aplicável?
   - [ ] Location header validado em testes de create?

4. **Documentação**
   - [ ] Endpoint listado em `docs/API_VERSIONING.md`?
   - [ ] Decisão de design documentada se for comportamento novo?

## ⚠️ Exceções ao Versionamento

**APENAS estes endpoints podem ficar fora de `/api/v1`:**

1. **Health Check**: `/api/health`
   - Motivo: Infra-level, global, não deve versionar
   - Requer auth? **NÃO** (`.AllowAnonymous()`)

2. **Auth Endpoints**: `/api/auth/*`
   - Motivo: Infra-level, parte da camada de autenticação
   - Inclui: `/api/auth/token`, `/api/auth/users`
   - Requer auth? **Depende do endpoint**

**Todos os outros endpoints DEVEM usar `/api/v1`.**

## 🔄 Quando Criar v2?

Considere criar `/api/v2` quando:

1. **Breaking Change Inevitável:**
   - Mudar shape de DTO existente (remover campos, mudar tipos)
   - Mudar comportamento semântico de endpoint
   - Mudar códigos de status HTTP

2. **Não É Breaking (não precisa v2):**
   - Adicionar novos campos opcionais em DTOs
   - Adicionar novos endpoints
   - Corrigir bugs
   - Melhorar performance
   - Adicionar validações mais restritivas

3. **Processo de Migração:**
   ```csharp
   // Backend: manter v1 e adicionar v2
   var v1 = app.MapGroup("/api/v1");
   var v2 = app.MapGroup("/api/v2");
   
   // v1: comportamento antigo (deprecado mas funcional)
   v1.MapGet("/processes", GetAllProcessesV1);
   
   // v2: novo comportamento
   v2.MapGet("/processes", GetAllProcessesV2);
   ```

   - OpenAPI: criar `config-api-v2.yaml`
   - Frontend: migrar gradualmente durante período de transição
   - Deprecar v1 após período (ex: 6 meses)

## 📊 Métricas de Compliance

### Como Verificar Compliance

```powershell
# Backend: verificar se há endpoints sem v1 (exceto health e auth)
Get-Content src/Api/Program.cs | Select-String 'app\.Map.*"/api/(?!v1|health|auth)'
# Expected: Nenhum resultado

# Testes: verificar URLs antigas
Get-ChildItem tests/Integration.Tests/*.cs | Select-String '"/api/(?!v1|health|auth)'
# Expected: Nenhum resultado

# OpenAPI: verificar baseUrl
Get-Content specs/shared/openapi/config-api.yaml | Select-String 'url:.*api/v1'
# Expected: url: http://localhost:8080/api/v1
```

### Auditoria de Compliance

Execute periodicamente:

```bash
# 1. Build deve passar
dotnet build

# 2. Testes devem passar
dotnet test

# 3. Validação manual de endpoints
./scripts/test-versioning.sh  # (criar se não existir)
```

## 🎯 Conclusão

✅ **Sistema está em compliance com padrão de versionamento OpenAPI.**

- Todos os endpoints de negócio usam `/api/v1`
- Exceções bem definidas e documentadas
- Testes atualizados
- Documentação completa
- Agent instructions atualizadas para garantir compliance futura
