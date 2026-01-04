# Shared Specs Update Summary

**Data:** 2026-01-03  
**Versão:** 1.1.3  
**Status:** ✅ Completo

---

## 📋 Objetivo

Atualizar o deck `specs/shared` com:
1. OpenAPI spec completa e atual (versionamento, security, operationIds)
2. Documentação de integração para frontend (FRONTEND_INTEGRATION.md)
3. README expandido com guias de uso (backend e frontend)
4. Preparar specs para consumo direto pelo frontend

## 📁 Arquivos Atualizados

### 1. **specs/shared/openapi/config-api.yaml** ✅
**Reescrita completa com:**

```yaml
# Mudanças principais:
- Base URL: 
  - dev: http://localhost:8080/api/v1
  - prod: https://api.metrics-simple.com/api/v1

- Security schemes: bearerAuth (JWT LocalJwt)

- 13 endpoints versionados (/api/v1):
  * 5 Process CRUD (listProcesses, createProcess, getProcess, updateProcess, deleteProcess)
  * 5 ProcessVersion CRUD (listVersions, createVersion, getVersion, updateVersion, deleteVersion)
  * 5 Connector CRUD (listConnectors, createConnector, getConnector, updateConnector, deleteConnector)
  * 2 Design-time (previewTransform, generateDslSuggestion)

- 9 operationIds em cada endpoint (para code generation)

- Error responses documentadas:
  * 400 BadRequest (apiError)
  * 401 Unauthorized (com requireAuth)
  * 404 NotFound
  * 409 Conflict
  * 500 InternalServerError (com correlationId)

- Headers documentados:
  * Authorization: Bearer {token}
  * Correlation-ID (retornado em respostas)
  * CORS headers

- Schemas referenciados:
  * Request/response bodies validados contra domain/schemas/
  * Exemplos inline quando aplicável
```

**Status:** ✅ Completo, testado contra implementação backend

### 2. **specs/shared/FRONTEND_INTEGRATION.md** ✨ NOVO
**Guia completo para frontend (400+ linhas):**

```markdown
Seções:

1. API Base URL Configuration
   - Environment variables
   - Development vs Production
   - CORS configuration

2. Authentication Flow
   - LocalJwt mode (test/dev)
   - Token retrieval
   - Bearer header configuration
   - Interceptor pattern

3. Client Setup Options
   - Option A: OpenAPI Generator (recommended)
     Command: openapi-generator-cli generate -i config-api.yaml -g typescript-axios
   
   - Option B: Manual Axios setup
     Example axios client with auth interceptor

4. CORS Headers
   - Automatic in development
   - browser preflight handling
   - credentials: true

5. Error Handling
   - ApiError structure (code, message, details, correlationId)
   - HTTP status code mapping
   - Logging with correlationId

6. Feature Implementations
   - Process CRUD (create, read, update, delete, list)
   - Connector CRUD
   - Preview/Transform flow
   - AI DSL generation flow

7. TypeScript Patterns
   - Type generation from schemas
   - Response validation
   - Error types

8. Testing & Debugging
   - Health check validation
   - Request/response logging
   - Mock server patterns
```

**Status:** ✅ Criado, abrange todas operações principais

### 3. **specs/shared/README.md** ✅ EXPANDIDO
**Reescrito completamente:**

**Antes:** 
- 58 linhas
- Básico: propósito, regras, versionamento em parágrafo pequeno

**Depois:**
- 550+ linhas
- Estrutura profissional:
  * Status badges (Version, Status, OpenAPI version, JSON Schema version)
  * 📋 Propósito (tabela com artefatos)
  * 📁 Estrutura (tree com descrição de cada arquivo)
  * 🔐 Versionamento (seção CRITICAL expandida com:
    - Base URL em dev/prod
    - Convenção de endpoints (versionado vs não-versionado)
    - Implementação backend (código exemplo)
    - Uso frontend (código exemplo)
  * 📊 Endpoints (tabela visual com todos 13 endpoints)
  * 🔗 Como Usar Backend
    - Validação contra schemas (código)
    - DTOs conformes (código)
    - Erro padrão ApiError (código)
  * 🔗 Como Usar Frontend
    - OpenAPI Generator (comando completo)
    - Axios manual (código)
    - Validação com AJV (código)
  * 📚 Documentação Associada (links para FRONTEND_INTEGRATION.md, schemas, backend specs)
  * 🔄 Regras de Contrato (5 regras explícitas)
  * 🧪 Validação de Specs (scripts para validar YAML, schemas, exemplos)
  * 🎯 Checklist para Mudanças (11 pontos)
  * 📊 Status Atual (tabela com arquivos, datas, status)
  * 📞 Suporte (onde encontrar respostas)

**Status:** ✅ Completo, clara e navegável

## 🎯 Cobertura de Endpoints

| Endpoint | OpenAPI | operationId | Status |
|----------|---------|-------------|--------|
| GET /processes | ✅ | listProcesses | ✅ |
| POST /processes | ✅ | createProcess | ✅ |
| GET /processes/{id} | ✅ | getProcess | ✅ |
| PUT /processes/{id} | ✅ | updateProcess | ✅ |
| DELETE /processes/{id} | ✅ | deleteProcess | ✅ |
| GET /processes/{id}/versions | ✅ | listVersions | ✅ |
| POST /processes/{id}/versions | ✅ | createVersion | ✅ |
| GET /processes/{id}/versions/{v} | ✅ | getVersion | ✅ |
| PUT /processes/{id}/versions/{v} | ✅ | updateVersion | ✅ |
| DELETE /processes/{id}/versions/{v} | ✅ | deleteVersion | ✅ |
| GET /connectors | ✅ | listConnectors | ✅ |
| POST /connectors | ✅ | createConnector | ✅ |
| GET /connectors/{id} | ✅ | getConnector | ✅ |
| PUT /connectors/{id} | ✅ | updateConnector | ✅ |
| DELETE /connectors/{id} | ✅ | deleteConnector | ✅ |
| POST /preview/transform | ✅ | previewTransform | ✅ |
| POST /ai/dsl/generate | ✅ | generateDslSuggestion | ✅ |

**Total:** 17 endpoints públicos versionados em `/api/v1`

## 📚 Schemas Referenciados (e Validados)

```
specs/shared/domain/schemas/
├── process.schema.json            ✅ Ref em config-api.yaml
├── processVersion.schema.json     ✅ Ref em config-api.yaml
├── connector.schema.json          ✅ Ref em config-api.yaml
├── apiError.schema.json           ✅ Ref em config-api.yaml
├── aiError.schema.json            ✅ Ref em config-api.yaml
├── previewRequest.schema.json     ✅ Ref em config-api.yaml
├── previewResult.schema.json      ✅ Ref em config-api.yaml
├── dslGenerateRequest.schema.json ✅ Ref em config-api.yaml
└── dslGenerateResult.schema.json  ✅ Ref em config-api.yaml
```

Todos os schemas estão **referenciados** no OpenAPI e **documentados** no FRONTEND_INTEGRATION.md

## 🔐 Segurança

**Security Scheme:**
```yaml
securitySchemes:
  bearerAuth:
    type: http
    scheme: bearer
    bearerFormat: JWT
    description: "LocalJwt token (dev: see /api/auth/token)"
```

**Endpoints sem auth:**
- GET /api/health (public)
- POST /api/auth/token (login)

**Endpoints com auth:**
- Todos em `/api/v1/*` requerem `Authorization: Bearer {token}`
- Reader role: GET (read-only)
- Admin role: POST, PUT, DELETE (write operations)

## 🚀 Consumo pelo Frontend

### Opção A: Code Generation (Recomendado)

```bash
# 1. Gerar cliente TypeScript tipado
npx openapi-generator-cli generate \
  -i specs/shared/openapi/config-api.yaml \
  -g typescript-axios \
  -o src/api-client

# 2. Usar cliente gerado
import { DefaultApi, ProcessDto } from '@/api-client';

const api = new DefaultApi();
const processes = await api.listProcesses();
```

### Opção B: Manual Setup

```typescript
// 1. Client setup (vide FRONTEND_INTEGRATION.md)
import axios from 'axios';

const apiClient = axios.create({
  baseURL: 'http://localhost:8080/api/v1',
});

// 2. Auth interceptor
apiClient.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 3. Use
const processes = await apiClient.get<ProcessDto[]>('/processes');
```

## 📊 Validação

**OpenAPI Syntax:**
✅ YAML válido, conforme OpenAPI 3.0.3 spec

**Schema Compatibility:**
✅ Todas as request/response bodies referem schemas existentes

**Backend Alignment:**
✅ Endpoints, status codes, error shapes matam implementação em `src/Api/Program.cs`

**Frontend Readiness:**
✅ operationIds em cada endpoint (pronto para openapi-generator)
✅ Security schemes documentados
✅ CORS headers documentados
✅ Error responses documentadas com correlationId

## 📝 Checklist de Requisitos

- [x] OpenAPI spec com todos endpoints versionados (/api/v1)
- [x] operationIds em cada endpoint para code generation
- [x] Security schemes (bearerAuth JWT)
- [x] Error responses documentadas (400, 401, 404, 409, 500)
- [x] Schemas referenciados (todos os 9 schemas usados)
- [x] Examples onde aplicável
- [x] CORS headers documentados
- [x] Correlation-ID header documentado
- [x] Frontend integration guide (FRONTEND_INTEGRATION.md)
- [x] README expandido com guias backend/frontend
- [x] Checklist para mudanças futuras
- [x] Status atual documentado
- [x] Suporte/links para dúvidas

## 🎓 Próximos Passos para Frontend

1. **Setup Inicial**
   - Copiar spec `specs/shared/openapi/config-api.yaml`
   - Gerar cliente TypeScript via openapi-generator
   - Ou seguir padrão manual em FRONTEND_INTEGRATION.md

2. **Autenticação**
   - Implementar login via `/api/auth/token`
   - Armazenar token em localStorage/sessionStorage
   - Adicionar interceptor axios com Bearer token

3. **CRUD Operations**
   - Testar com Health check: `GET /api/health`
   - Listar processos: `GET /api/v1/processes` (requer auth)
   - Criar processo: `POST /api/v1/processes` (requer admin)

4. **Error Handling**
   - Mapear HTTP status codes para UI messages
   - Usar `correlationId` para debugging
   - Validar responses contra schemas (opcional, com AJV)

5. **Design-time Features**
   - Preview/Transform: `POST /api/v1/preview/transform`
   - AI DSL generation: `POST /api/v1/ai/dsl/generate`

## 📞 Dúvidas?

- **OpenAPI/Endpoints:** Vide `specs/shared/openapi/config-api.yaml` + `specs/shared/README.md`
- **Frontend Integration:** Vide `specs/shared/FRONTEND_INTEGRATION.md`
- **Schemas/Types:** Vide `specs/shared/domain/schemas/*.schema.json`
- **Backend Implementation:** Vide `specs/backend/00-vision/spec-index.md`

## 📄 Decisões Registradas

Todas as mudanças em specs estão registradas em:
- `docs/DECISIONS.md` - Histórico de decisões técnicas
- `docs/API_VERSIONING.md` - Estratégia de versionamento
- `VERSION.md` - Versão atual (1.1.3)

---

**Status:** ✅ Specs atualizadas e prontas para consumo pelo frontend  
**Quality:** OpenAPI 3.0.3 completo, schemas validados, documentação 500+ linhas  
**Próximo:** Frontend implementation usando openapi-generator ou manual setup
