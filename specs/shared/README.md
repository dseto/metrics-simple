# Shared Specs (Contracts)

**Version:** 1.1.3  
**Status:** ✅ Atualizado (2026-01-03)  
**Spec-Driven:** Sim | **OpenAPI:** 3.0.3 | **JSON Schema:** Draft 2020-12

---

## 📋 Propósito

Este deck contém os **contratos canônicos** (fonte de verdade) consumidos por **backend** e **frontend**:

| Artefato | Propósito | Consumidor |
|----------|-----------|-----------|
| `openapi/config-api.yaml` | Especificação REST completa (rotas, DTOs, erros, segurança) | Backend & Frontend |
| `domain/schemas/*.schema.json` | Modelos, validações, exemplos | Backend & Frontend |
| `domain/types/*` | Enumerações, constantes globais | Backend & Frontend |
| `examples/*` | Dados de teste, fixtures | Testes & Docs |

## 📁 Estrutura

```
specs/shared/
├── openapi/
│   └── config-api.yaml                    # OpenAPI 3.0.3 completo
├── domain/
│   ├── schemas/
│   │   ├── process.schema.json            # Modelo Process
│   │   ├── processVersion.schema.json     # Modelo ProcessVersion
│   │   ├── connector.schema.json          # Modelo Connector
│   │   ├── apiError.schema.json           # Erro padrão (HTTP)
│   │   ├── aiError.schema.json            # Erro de AI (design-time)
│   │   ├── previewRequest.schema.json     # Request de preview
│   │   ├── previewResult.schema.json      # Response de preview
│   │   ├── dslGenerateRequest.schema.json # Request AI DSL
│   │   └── dslGenerateResult.schema.json  # Response AI DSL
│   └── types/
│       └── enums.ts                       # Constantes e enums
├── examples/
│   ├── process.json                       # Ex: POST /api/v1/processes
│   ├── processVersion.json                # Ex: POST /api/v1/processes/{id}/versions
│   └── connector.json                     # Ex: POST /api/v1/connectors
├── SCHEMA_GUIDE.md                        # Guia completo dos schemas
├── FRONTEND_INTEGRATION.md                # Como usar specs no frontend
└── README.md                              # Este arquivo
```

## 🔐 Versionamento de API (CRITICAL)

### Base URL

```
Development:  http://localhost:8080/api/v1
Production:   https://api.metrics-simple.com/api/v1
```

### Convenção de Endpoints

**Todos os endpoints de negócio DEVEM usar `/api/v1` como prefixo:**

✅ **Versionado:**
```
GET    /api/v1/processes
POST   /api/v1/processes
GET    /api/v1/processes/{id}
PUT    /api/v1/processes/{id}
DELETE /api/v1/processes/{id}
GET    /api/v1/processes/{id}/versions
POST   /api/v1/processes/{id}/versions
GET    /api/v1/connectors
POST   /api/v1/connectors
POST   /api/v1/preview/transform
POST   /api/v1/ai/dsl/generate
```

❌ **SEM versionamento (infraestrutura):**
```
GET    /api/health                    # Health check global
POST   /api/auth/token                # Autenticação (LocalJwt)
GET    /api/auth/users                # Info de usuário
```

### Implementação no Backend

```csharp
// Program.cs - ASP.NET Core 10.0+
// 1. Agrupar endpoints de negócio sob /api/v1
var v1 = app.MapGroup("/api/v1")
    .WithTags("API v1")
    .AddEndpointFilter<CorrelationIdFilter>();

// 2. Subgrupos por recurso
var processes = v1.MapGroup("/processes")
    .WithTags("Processes");

processes.MapGet("/", GetAllProcesses)
    .Produces<List<ProcessDto>>()
    .WithSummary("List all processes");

// 3. Endpoints de infraestrutura SEM versionamento
app.MapGet("/api/health", GetHealth)
    .WithTags("Health")
    .AllowAnonymous();

app.MapPost("/api/auth/token", GetAuthToken)
    .WithTags("Authentication")
    .AllowAnonymous();
```

### Uso no Frontend

```typescript
// 1. Client HTTP com base URL versionada
import axios from 'axios';

const apiClient = axios.create({
  baseURL: 'http://localhost:8080/api/v1',
  headers: {
    'Accept': 'application/json'
  }
});

// 2. Adicionar token ao header
apiClient.interceptors.request.use(config => {
  const token = localStorage.getItem('auth_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 3. Fazer requests (sem /api/v1 no caminho)
const response = await apiClient.get('/processes');    // GET /api/v1/processes
const process = await apiClient.post('/processes', { name: '...' });
```

## 📊 Endpoints (Resumo Rápido)

```
╔═══════════════════════════════════════════════════════════════════╗
║ PROCESSES MANAGEMENT                          Auth: Reader/Admin  ║
╠═══════════════════════════════════════════════════════════════════╣
║ GET    /processes                    [listProcesses]              ║
║ POST   /processes                    [createProcess]              ║
║ GET    /processes/{id}               [getProcess]                 ║
║ PUT    /processes/{id}               [updateProcess]              ║
║ DELETE /processes/{id}               [deleteProcess]              ║
╠═══════════════════════════════════════════════════════════════════╣
║ VERSION MANAGEMENT                             Auth: Reader/Admin  ║
╠═══════════════════════════════════════════════════════════════════╣
║ GET    /processes/{id}/versions      [listVersions]               ║
║ POST   /processes/{id}/versions      [createVersion]              ║
║ GET    /processes/{id}/versions/{v}  [getVersion]                 ║
║ PUT    /processes/{id}/versions/{v}  [updateVersion]              ║
║ DELETE /processes/{id}/versions/{v}  [deleteVersion]              ║
╠═══════════════════════════════════════════════════════════════════╣
║ CONNECTOR MANAGEMENT                           Auth: Reader/Admin  ║
╠═══════════════════════════════════════════════════════════════════╣
║ GET    /connectors                   [listConnectors]             ║
║ POST   /connectors                   [createConnector]            ║
║ GET    /connectors/{id}              [getConnector]               ║
║ PUT    /connectors/{id}              [updateConnector]            ║
║ DELETE /connectors/{id}              [deleteConnector]            ║
╠═══════════════════════════════════════════════════════════════════╣
║ DESIGN-TIME OPERATIONS                        Auth: Reader/Admin  ║
╠═══════════════════════════════════════════════════════════════════╣
║ POST   /preview/transform            [previewTransform]           ║
║ POST   /ai/dsl/generate              [generateDslSuggestion]      ║
╚═══════════════════════════════════════════════════════════════════╝
```

## 🔗 Como Usar (Backend)

### 1. Validar Requests contra Schemas

```csharp
using NJsonSchema;
using System.Text.Json;

// Carregar schema
var schema = await JsonSchema.FromFileAsync("schemas/process.schema.json");

// Validar JSON
var process = JsonDocument.Parse(jsonInput);
var validationErrors = schema.Validate(process.RootElement);

if (validationErrors.Count > 0)
{
    return Results.BadRequest(new ApiError
    {
        Code = "VALIDATION_ERROR",
        Message = "Invalid process format",
        Details = validationErrors.Select(e => e.ToString()).ToList()
    });
}
```

### 2. Retornar DTOs Conformes

```csharp
// Garantir que o DTO segue o schema
public record ProcessDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; }
    
    [JsonPropertyName("name")]
    public string Name { get; init; }
    
    [JsonPropertyName("version")]
    public int Version { get; init; }
}

// Endpoint retorna schema-conform
app.MapGet("/processes/{id}", (string id) =>
{
    var process = _repository.GetById(id);
    return Results.Ok(new ProcessDto
    {
        Id = process.Id,
        Name = process.Name,
        Version = process.Version
    });
});
```

### 3. Erro Padrão (API)

```csharp
// Sempre retornar ApiError conforme schema
public class ApiError
{
    [JsonPropertyName("code")]
    public string Code { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; }
    
    [JsonPropertyName("details")]
    public List<string>? Details { get; set; }
    
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}

// Uso
return Results.BadRequest(new ApiError
{
    Code = "PROCESS_NOT_FOUND",
    Message = "Process not found",
    CorrelationId = correlationId
});
```

## 🔗 Como Usar (Frontend)

### Opção A: Gerar Cliente TypeScript (Recomendado)

```bash
# Instalar gerador
npm install -D @openapitools/openapi-generator-cli

# Gerar cliente
npx openapi-generator-cli generate \
  -i specs/shared/openapi/config-api.yaml \
  -g typescript-axios \
  -o src/api-client \
  -c openapi-config.json

# Usar cliente gerado
import { DefaultApi } from '@/api-client';

const api = new DefaultApi();
api.listProcesses().then(processes => {
  console.log(processes);
});
```

### Opção B: Implementar com Axios + TypeScript

```typescript
// 1. Definir tipos baseados em schemas
interface Process {
  id: string;
  name: string;
  version: number;
  enabled: boolean;
  connectorId: string;
  dsl: string;
  outputSchema: Record<string, unknown>;
}

// 2. Client HTTP
import axios from 'axios';

const apiClient = axios.create({
  baseURL: 'http://localhost:8080/api/v1',
});

// 3. Adicionar auth
apiClient.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 4. Usar
const processes = await apiClient.get<Process[]>('/processes');

// 5. Validar contra schema (opcional)
import Ajv from 'ajv';
import processSchema from '@/specs/shared/domain/schemas/process.schema.json';

const ajv = new Ajv();
const validate = ajv.compile(processSchema);
const isValid = validate(processes.data[0]);
```

### Validação com Schemas (AJV)

```typescript
import Ajv from 'ajv';

// 1. Importar schema
import processVersionSchema from '@/specs/shared/domain/schemas/processVersion.schema.json';

// 2. Compilar validador
const ajv = new Ajv();
const validateVersion = ajv.compile(processVersionSchema);

// 3. Validar dados
const newVersion = {
  version: 2,
  dsl: 'input | map(.)  ',
  outputSchema: { type: 'array' }
};

if (!validateVersion(newVersion)) {
  console.error('Invalid version:', validateVersion.errors);
  throw new Error('Version does not match schema');
}
```

## 📚 Documentação Associada

| Documento | Propósito |
|-----------|-----------|
| [FRONTEND_INTEGRATION.md](./FRONTEND_INTEGRATION.md) | **Guia completo para frontend:** autenticação, CORS, setup axios, geração client, error handling, exemplos de CRUD |
| [SCHEMA_GUIDE.md](./SCHEMA_GUIDE.md) | Documentação detalhada de cada schema: campos, tipos, restrições, exemplos |
| [openapi/config-api.yaml](./openapi/config-api.yaml) | Especificação OpenAPI 3.0.3: rotas, DTOs, status codes, headers, security schemes |
| `/backend/00-vision/spec-index.md` | Backend: implementação, persistência, runner, logging |
| `/frontend/00-vision/spec-index.md` | Frontend: UI, components, client, testing |

## 🔄 Regras de Contrato

1. **Nunca duplicar** contratos
   - Se backend ou frontend precisar de um contrato, deve estar aqui e ser referenciado

2. **Backend** deve validar conforme schemas
   - Todos os POSTs/PUTs validam contra `domain/schemas/*.schema.json`
   - Todos os responses conformam aos tipos definidos

3. **Frontend** deve usar como fonte de verdade
   - Tipos TypeScript gerados ou manuais baseados em schemas
   - Requests/responses validadas via AJV
   - HTTP client com base URL de versionamento

4. **Mudanças em spec são mudanças de contrato**
   - Atualizar versão em `VERSION.md`
   - Adicionar entrada em `DECISIONS.md`
   - Sincronizar backend e frontend juntos
   - Testar integração end-to-end

5. **Atualizar `spec-manifest.json`**
   - Manter lista de arquivos e checksums
   - Detectar mudanças não autorizadas

## 🧪 Validar Specs Localmente

### Validar OpenAPI YAML

```bash
# Instalar validador
npm install -g @apidevtools/swagger-cli

# Validar sintaxe
swagger-cli validate openapi/config-api.yaml

# Validar contra especificação 3.0.3
swagger-cli validate --spec 3.0 openapi/config-api.yaml
```

### Validar Schemas JSON

```bash
# Instalar AJV CLI
npm install -g ajv-cli

# Validar schema syntax
ajv validate -d domain/schemas/process.schema.json

# Validar exemplo contra schema
ajv validate -s domain/schemas/process.schema.json -d examples/process.json
```

### Validar no Backend (C#)

```csharp
using NJsonSchema;

// Carregar todos os schemas
var processSchema = await JsonSchema.FromFileAsync("schemas/process.schema.json");
var versionSchema = await JsonSchema.FromFileAsync("schemas/processVersion.schema.json");

// Validar exemplo
var processJson = File.ReadAllText("examples/process.json");
var doc = JsonDocument.Parse(processJson);
var errors = processSchema.Validate(doc.RootElement);

if (errors.Any())
{
    throw new InvalidOperationException(
        $"Example doesn't match schema: {string.Join(", ", errors)}");
}
```

## 🎯 Checklist para Mudanças em Specs

Ao adicionar/modificar endpoints ou schemas:

- [ ] Atualizar `openapi/config-api.yaml` (rotas, DTOs, status codes)
- [ ] Atualizar schema JSON relevante em `domain/schemas/`
- [ ] Adicionar exemplo em `examples/`
- [ ] Validar YAML/JSON (sem erros de sintaxe)
- [ ] Validar exemplos contra schemas (AJV)
- [ ] Testar no backend (contract tests em `tests/Contracts.Tests/`)
- [ ] Atualizar frontend (types, client, testes)
- [ ] Atualizar `spec-manifest.json` com checksums
- [ ] Adicionar decisão em `docs/DECISIONS.md`
- [ ] Atualizar `VERSION.md` (minor ou major)
- [ ] Sincronizar com team (pull request)

## 📊 Status Atual

| Artefato | Última Atualização | Status |
|----------|-------------------|--------|
| `openapi/config-api.yaml` | 2026-01-03 21:15 | ✅ Completo (13 endpoints + security) |
| `domain/schemas/` | 2026-01-03 21:00 | ✅ Validados contra exemplos |
| `examples/` | 2026-01-03 20:50 | ✅ Process, Version, Connector |
| `FRONTEND_INTEGRATION.md` | 2026-01-03 21:30 | ✅ Guia 400+ linhas com exemplos |
| `spec-manifest.json` | 2026-01-02 | 🔄 Pendente atualizar checksums |

## 📞 Suporte

- **OpenAPI Spec:** Dúvidas sobre rotas/DTOs → `openapi/config-api.yaml` + FRONTEND_INTEGRATION.md
- **Schemas:** Dúvidas sobre validação/tipos → `domain/schemas/` + SCHEMA_GUIDE.md
- **Integração:** Dúvidas sobre uso backend/frontend → FRONTEND_INTEGRATION.md
- **Decisões:** Histórico de mudanças → `docs/DECISIONS.md`

---

**Versão:** 1.1.3  
**Spec-Driven:** Sim  
**OpenAPI:** 3.0.3  
**JSON Schema:** Draft 2020-12  
**Última Atualização:** 2026-01-03 21:30
