# OpenAPI Spec - Frontend Integration Guide

**Data:** 2026-01-03  
**Spec Location:** `specs/shared/openapi/config-api.yaml`  
**API Version:** v1.1.0

## 📋 Overview

A especificação OpenAPI define contrato completo da API Metrics Simple, incluindo:
- Todos os endpoints de negócio (`/api/v1/*`)
- Esquemas JSON completos
- Políticas de autenticação
- Tratamento de erros
- CORS e headers esperados

## 🔗 Base URL

```
http://localhost:8080/api/v1
```

**Em Produção:**
```
https://api.metrics-simple.example.com/api/v1
```

## 🔐 Autenticação

### LocalJwt Mode (Development)

1. **Obter Token**
   ```bash
   POST /api/auth/token
   Content-Type: application/json
   
   {
     "username": "admin",
     "password": "your-password"
   }
   ```

2. **Usar Token**
   ```bash
   GET /api/v1/processes
   Authorization: Bearer {token}
   ```

### Security Schemes

OpenAPI define:
```yaml
securitySchemes:
  bearerAuth:
    type: http
    scheme: bearer
    bearerFormat: JWT
```

**Aplicável a todos endpoints exceto:**
- `GET /api/health` (sem auth)

## 📊 Endpoints Disponíveis

### Connectors
```
GET    /connectors              (Reader)
POST   /connectors              (Admin)
GET    /connectors/{id}         (Reader)
PUT    /connectors/{id}         (Admin)
DELETE /connectors/{id}         (Admin)
```

### Processes
```
GET    /processes                        (Reader)
POST   /processes                        (Admin)
GET    /processes/{id}                   (Reader)
PUT    /processes/{id}                   (Admin)
DELETE /processes/{id}                   (Admin)
GET    /processes/{id}/versions          (Reader)
POST   /processes/{id}/versions          (Admin)
GET    /processes/{id}/versions/{v}      (Reader)
PUT    /processes/{id}/versions/{v}      (Admin)
DELETE /processes/{id}/versions/{v}      (Admin)
```

### Design-Time Operations
```
POST   /preview/transform    (Reader)  - Preview transformation
POST   /ai/dsl/generate      (Reader)  - AI-assisted DSL generation
```

## 📦 Integração no Frontend

### 1. Geração de Cliente HTTP (OpenAPI Generator)

```bash
# Gerar cliente TypeScript/Axios
openapi-generator-cli generate \
  -i specs/shared/openapi/config-api.yaml \
  -g typescript-axios \
  -o src/api-client \
  -p apiPackage=api
```

Resultado: Cliente totalmente tipado com Axios

### 2. Configuração Manual (Axios)

```typescript
import axios from 'axios';

const apiClient = axios.create({
  baseURL: 'http://localhost:8080/api/v1',
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json'
  }
});

// Interceptor para adicionar token
apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('auth_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Exportar para uso
export default apiClient;
```

### 3. Operações CRUD

```typescript
// List processes
const processes = await apiClient.get('/processes');

// Create process
const newProcess = await apiClient.post('/processes', {
  id: 'proc-001',
  name: 'My Process',
  connectorId: 'conn-001',
  outputDestinations: []
});

// Get specific process
const process = await apiClient.get(`/processes/${processId}`);

// Update process
const updated = await apiClient.put(`/processes/${processId}`, {
  ...process,
  name: 'Updated Name'
});

// Delete process
await apiClient.delete(`/processes/${processId}`);
```

## 🔄 Fluxo de Autenticação

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. User submits credentials (UI Form)                           │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 2. POST /api/auth/token                                         │
│    Request: { username, password }                              │
│    Response: { accessToken, tokenType: "Bearer" }               │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 3. Store token (localStorage or sessionStorage)                 │
│    localStorage.setItem('auth_token', response.accessToken)     │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 4. Include token in all subsequent API calls                    │
│    Authorization: Bearer {token}                                │
│                                                                 │
│    GET /api/v1/processes                                        │
│    Headers: { Authorization: "Bearer xyz..." }                  │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ 5. Handle 401 responses                                         │
│    - Clear stored token                                         │
│    - Redirect to login page                                     │
│    - Show error message                                         │
└─────────────────────────────────────────────────────────────────┘
```

## 🛡️ Tratamento de Erros

### ApiError Response Format

```json
{
  "code": "PROCESS_NOT_FOUND",
  "message": "Process with ID 'proc-xyz' not found",
  "correlationId": "abc123def456"
}
```

### Error Codes

| Status | Código | Significado |
|--------|--------|-------------|
| 400 | `VALIDATION_FAILED` | Request inválido |
| 401 | `AUTH_UNAUTHORIZED` | Token inválido/expirado |
| 404 | `NOT_FOUND` | Recurso não encontrado |
| 409 | `CONFLICT` | Violação de constraint |
| 502 | `BAD_GATEWAY` | Erro do provider (AI) |
| 503 | `SERVICE_UNAVAILABLE` | Serviço indisponível |

### Exemplo - Tratamento de Erros

```typescript
try {
  await apiClient.post('/processes', data);
} catch (error) {
  if (error.response?.status === 401) {
    // Token inválido - fazer logout
    localStorage.removeItem('auth_token');
    router.push('/login');
  } else if (error.response?.status === 409) {
    // Conflito - mostrar mensagem específica
    const { code, message } = error.response.data;
    showError(`${code}: ${message}`);
  } else if (error.response?.status === 502) {
    // AI error - tentar novamente
    showWarning('AI provider error, please try again');
  }
}
```

## 📝 Implementação de Features

### Feature: Preview de Transformação

```typescript
async function previewTransform(dsl, sampleInput, outputSchema) {
  const response = await apiClient.post('/preview/transform', {
    dsl: {
      profile: 'jsonata',
      text: dsl
    },
    sampleInput: sampleInput,
    outputSchema: outputSchema
  });
  
  return {
    isValid: response.data.isValid,
    errors: response.data.errors || [],
    outputJson: response.data.outputJson,
    csvPreview: response.data.csvPreview
  };
}
```

### Feature: Geração de DSL via AI

```typescript
async function generateDslWithAI(goalText, sampleInput, dslProfile) {
  try {
    const response = await apiClient.post('/ai/dsl/generate', {
      dslProfile: dslProfile,
      goalText: goalText,
      sampleInput: sampleInput,
      existingDsl: null,
      hints: {}
    });
    
    return {
      dsl: response.data.dsl,
      outputSchema: response.data.outputSchema,
      confidence: response.data.confidence
    };
  } catch (error) {
    if (error.response?.status === 503) {
      throw new Error('AI service is disabled or unavailable');
    }
    throw error;
  }
}
```

## 🔄 CORS Handling

Frontend em `http://localhost:4200` pode fazer requests para `http://localhost:8080/api/v1`:

```typescript
// CORS é automaticamente permitido - nenhuma configuração adicional necessária
// Os headers CORS já estão configurados no backend

const response = await axios.get('http://localhost:8080/api/v1/processes', {
  withCredentials: true  // Importante para enviar cookies/auth
});
```

## 📊 Tipagem TypeScript (Gerado do OpenAPI)

Se usar openapi-generator:

```typescript
import { 
  ProcessDto, 
  ConnectorDto, 
  ProcessVersionDto,
  PreviewTransformRequestDto,
  DslGenerateRequest
} from '@/api-client';

// Tipagem completa
const process: ProcessDto = {
  id: 'proc-001',
  name: 'Example',
  status: 'active',
  connectorId: 'conn-001',
  outputDestinations: []
};
```

## 📖 Exemplos de Uso

### Create → Preview → Execute Flow

```typescript
// 1. Usuário cria um novo processo
const newProcess = await apiClient.post('/processes', {
  id: 'proc-test',
  name: 'Test Process',
  connectorId: 'conn-api',
  outputDestinations: [{
    type: 'localFilesystem',
    local: { basePath: '/output' }
  }]
});

// 2. Usuário cria versão com DSL
const version = await apiClient.post('/processes/proc-test/versions', {
  processId: 'proc-test',
  version: 1,
  enabled: true,
  sourceRequest: {
    method: 'GET',
    path: '/api/users'
  },
  dsl: {
    profile: 'jsonata',
    text: '{ "name": Name, "email": Email }'
  },
  outputSchema: { ... },
  sampleInput: { Name: "John", Email: "john@example.com" }
});

// 3. Usuário faz preview antes de executar
const preview = await apiClient.post('/preview/transform', {
  dsl: version.dsl,
  outputSchema: version.outputSchema,
  sampleInput: version.sampleInput
});

if (preview.isValid) {
  // Mostrar preview ao usuário
  console.log('CSV Preview:', preview.csvPreview);
}
```

## 🔍 Debugging

### Verificar Correlação de Requests

```typescript
// Todos os responses incluem X-Correlation-Id
apiClient.interceptors.response.use(response => {
  const correlationId = response.headers['x-correlation-id'];
  console.log(`Request [${correlationId}] completed`);
  return response;
});

// Útil para rastrear erros nos logs do servidor
```

### Health Check

```typescript
// Health check não requer auth
async function isAPIHealthy() {
  try {
    const response = await axios.get('http://localhost:8080/api/health');
    return response.data.status === 'ok';
  } catch {
    return false;
  }
}
```

## 📚 Referências

- **OpenAPI Spec:** `specs/shared/openapi/config-api.yaml`
- **Schemas:** `specs/shared/domain/schemas/`
- **Backend:** `src/Api/Program.cs`
- **Versionamento:** [docs/API_VERSIONING.md](../API_VERSIONING.md)
- **Decisões:** [docs/DECISIONS.md](../DECISIONS.md)

## ✅ Checklist de Implementação

- [ ] Cliente HTTP configurado (Axios ou equivalente)
- [ ] Bearer token interceptor implementado
- [ ] Tratamento de 401 implementado (logout + redirect)
- [ ] CORS funcionando (test com health check)
- [ ] Tipos TypeScript gerados ou definidos manualmente
- [ ] Interceptor de erros implementado
- [ ] Health check endpoint testado
- [ ] Feature de preview testada
- [ ] Feature de AI DSL testada (se AI habilitado)
- [ ] Tratamento de polling/retry implementado

---

**Próximos Passos:**

1. Gerar cliente HTTP a partir desta spec
2. Implementar tela de login
3. Integrar CRUD de processos
4. Testar fluxo end-to-end com runner

