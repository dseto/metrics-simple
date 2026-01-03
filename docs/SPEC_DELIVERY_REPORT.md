# 📊 Spec-Driven Update Report — Shared Deck v1.1.3

**Data:** 2026-01-03  
**Status:** ✅ **CONCLUÍDO**  
**Sessão:** Atualização de specs para consumo pelo frontend  

---

## 🎯 Objetivo Atingido

Atualizar `specs/shared` com **OpenAPI completo, operationIds, e documentação frontend** para que o frontend possa:
- Gerar cliente TypeScript tipado via `openapi-generator`
- Entender fluxo de autenticação
- Implementar CRUD operações de forma confiável
- Lidar com erros de forma padronizada

**Resultado:** ✅ **Specs prontas para consumo direto**

---

## 📦 Artefatos Entregues

### 1️⃣ **OpenAPI Spec Reescrito** 
**Arquivo:** [specs/shared/openapi/config-api.yaml](specs/shared/openapi/config-api.yaml)

```yaml
openapi: 3.0.3
info:
  title: Metrics Simple Config API
  version: 1.1.3
servers:
  - url: http://localhost:8080/api/v1
    description: Development
  - url: https://api.metrics-simple.com/api/v1
    description: Production

securitySchemes:
  bearerAuth:
    type: http
    scheme: bearer
    bearerFormat: JWT

paths:
  /processes:                    ✅ listProcesses, createProcess
  /processes/{id}:              ✅ getProcess, updateProcess, deleteProcess
  /processes/{id}/versions:     ✅ listVersions, createVersion
  /processes/{id}/versions/{v}: ✅ getVersion, updateVersion, deleteVersion
  /connectors:                  ✅ listConnectors, createConnector
  /connectors/{id}:             ✅ getConnector, updateConnector, deleteConnector
  /preview/transform:           ✅ previewTransform
  /ai/dsl/generate:             ✅ generateDslSuggestion
```

**Destaques:**
- ✅ 17 endpoints públicos
- ✅ 17 operationIds (para openapi-generator)
- ✅ Security schemes (JWT Bearer)
- ✅ Error responses completas (400, 401, 404, 409, 500)
- ✅ Headers documentados (Authorization, Correlation-ID)
- ✅ Schemas referenciados (9 JSON Schemas)
- ✅ Exemplos inline

### 2️⃣ **Frontend Integration Guide** 🆕
**Arquivo:** [specs/shared/FRONTEND_INTEGRATION.md](specs/shared/FRONTEND_INTEGRATION.md)

```markdown
Seções Principais:
├── 🔗 Base URL Configuration
├── 🔐 Authentication Flow (LocalJwt)
├── 📦 Client Setup (OpenAPI Generator + Manual Axios)
├── 🌐 CORS Headers & Configuration
├── ❌ Error Handling (ApiError shape + correlationId)
├── 🎯 Feature Implementations
│   ├── Process CRUD
│   ├── Connector CRUD
│   ├── Preview/Transform
│   └── AI DSL Generation
├── 📘 TypeScript Patterns
├── 🧪 Testing & Debugging
└── ✅ Implementation Checklist
```

**Tamanho:** 400+ linhas com código examples

**Conteúdo:**
- Passo a passo: gerar cliente OpenAPI
- Padrão manual com Axios + interceptores
- Fluxo de autenticação com diagrama
- Exemplos CRUD em TypeScript
- Padrões de error handling com correlationId
- Features de design-time (AI DSL, Preview)

### 3️⃣ **README Expandido**
**Arquivo:** [specs/shared/README.md](specs/shared/README.md)

```markdown
Mudanças:
Antes:  58 linhas (básico)
Depois: 550+ linhas (profissional)

Seções:
├── 📋 Propósito (tabela de artefatos)
├── 📁 Estrutura (tree com descrições)
├── 🔐 Versionamento API (CRITICAL - expandido)
│   ├── Base URL (dev/prod)
│   ├── Convenção de endpoints (v1 vs infra)
│   ├── Implementação backend (código C#)
│   └── Uso frontend (código TS)
├── 📊 Endpoints Resumo (tabela visual)
├── 🔗 Como Usar (Backend + Frontend)
│   ├── Validação contra schemas
│   ├── DTOs conformes
│   ├── Erros padrão (ApiError)
│   ├── OpenAPI Generator
│   ├── Axios manual
│   └── Validação com AJV
├── 📚 Documentação Associada
├── 🔄 Regras de Contrato
├── 🧪 Validação de Specs (scripts)
├── 🎯 Checklist para Mudanças
├── 📊 Status Atual (tabela)
└── 📞 Suporte (quick links)
```

**Destaques:**
- Tabelas visuais com status
- Código exemplo para backend e frontend
- Scripts de validação prontos para usar
- Checklist para futuras mudanças

---

## 📊 Cobertura de Endpoints

### ✅ Todos os 17 Endpoints Documentados

```
PROCESSES (5 endpoints)
├── GET    /processes              ✅ listProcesses
├── POST   /processes              ✅ createProcess
├── GET    /processes/{id}         ✅ getProcess
├── PUT    /processes/{id}         ✅ updateProcess
└── DELETE /processes/{id}         ✅ deleteProcess

VERSIONS (5 endpoints)
├── GET    /processes/{id}/versions          ✅ listVersions
├── POST   /processes/{id}/versions          ✅ createVersion
├── GET    /processes/{id}/versions/{v}     ✅ getVersion
├── PUT    /processes/{id}/versions/{v}     ✅ updateVersion
└── DELETE /processes/{id}/versions/{v}     ✅ deleteVersion

CONNECTORS (5 endpoints)
├── GET    /connectors              ✅ listConnectors
├── POST   /connectors              ✅ createConnector
├── GET    /connectors/{id}         ✅ getConnector
├── PUT    /connectors/{id}         ✅ updateConnector
└── DELETE /connectors/{id}         ✅ deleteConnector

DESIGN-TIME (2 endpoints)
├── POST   /preview/transform       ✅ previewTransform
└── POST   /ai/dsl/generate         ✅ generateDslSuggestion
```

---

## 🔐 Segurança Documentada

### Security Schemes

```yaml
securitySchemes:
  bearerAuth:
    type: http
    scheme: bearer
    bearerFormat: JWT
    description: |
      LocalJwt token for development.
      Get token via: POST /api/auth/token
      Use: Authorization: Bearer {token}
```

### Endpoints por Acesso

| Tipo | Endpoints | Autenticação |
|------|-----------|--------------|
| Public | `/api/health` | Não |
| Auth | `/api/auth/token` | Não (login) |
| **Business** | `/api/v1/*` | ✅ Sim (Reader/Admin) |

### Roles

| Role | Operações |
|------|-----------|
| Reader | GET (read-only) |
| Admin | POST, PUT, DELETE (write) |

---

## 🎯 Schemas Referenciados

Todos os **9 schemas** estão documentados em OpenAPI:

| Schema | Função | Referências |
|--------|--------|-------------|
| `process.schema.json` | Modelo Process | GET, POST, PUT responses |
| `processVersion.schema.json` | Modelo Version | Version CRUD |
| `connector.schema.json` | Modelo Connector | Connector CRUD |
| `previewRequest.schema.json` | Request preview | POST /preview/transform |
| `previewResult.schema.json` | Response preview | Preview response |
| `dslGenerateRequest.schema.json` | Request AI | POST /ai/dsl/generate |
| `dslGenerateResult.schema.json` | Response AI | AI response |
| `apiError.schema.json` | Erro HTTP | Todas responses 4xx/5xx |
| `aiError.schema.json` | Erro AI | AI endpoint errors |

**Status:** ✅ Todos referenciados, validados, exemplos fornecidos

---

## 🚀 Como Frontend Usa as Specs

### Opção A: Code Generation (Recomendado) ⭐

```bash
# 1. Gerar cliente TypeScript tipado
npx openapi-generator-cli generate \
  -i specs/shared/openapi/config-api.yaml \
  -g typescript-axios \
  -o src/api-client

# 2. Usar no código
import { DefaultApi, ProcessDto } from '@/api-client';

const api = new DefaultApi();

// Listar processos
const processes = await api.listProcesses();

// Criar processo
const newProcess = await api.createProcess({
  name: 'My Process',
  connectorId: 'conn-123',
  dsl: 'input | map(.)',
  outputSchema: { type: 'object' }
});

// Erros tipados
try {
  await api.getProcess('invalid-id');
} catch (error) {
  // error: AxiosError<ApiError>
  console.log(error.response.data.correlationId);
}
```

### Opção B: Manual Setup

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

// 2. Setup HTTP client (Axios)
import axios from 'axios';

const apiClient = axios.create({
  baseURL: 'http://localhost:8080/api/v1',
  headers: { 'Accept': 'application/json' }
});

// 3. Auth interceptor
apiClient.interceptors.request.use(config => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// 4. Usar
const response = await apiClient.get<Process[]>('/processes');
```

---

## ✅ Checklist de Qualidade

| Critério | Status | Detalhes |
|----------|--------|----------|
| OpenAPI Syntax | ✅ | YAML válido, 3.0.3 spec |
| Endpoints | ✅ | 17 endpoints documentados |
| operationIds | ✅ | 17 operationIds únicos |
| Schemas | ✅ | 9 schemas referenciados |
| Security | ✅ | bearerAuth, JWT documentado |
| Error Handling | ✅ | 400, 401, 404, 409, 500 |
| Headers | ✅ | Authorization, Correlation-ID |
| Examples | ✅ | Inline em requisições |
| Frontend Doc | ✅ | FRONTEND_INTEGRATION.md |
| README | ✅ | 550+ linhas, seções completas |
| Validação | ✅ | Scripts prontos (YAML, JSON) |
| Checklist Mudanças | ✅ | 11 pontos |

---

## 📈 Impacto & Benefícios

### Para Frontend

| Antes | Depois |
|-------|--------|
| ❌ Sem OpenAPI spec | ✅ OpenAPI 3.0.3 completo |
| ❌ DTOs manuais | ✅ Gerados automaticamente via openapi-generator |
| ❌ Sem operationIds | ✅ 17 operationIds para code gen |
| ❌ Guia vago | ✅ 400+ linhas FRONTEND_INTEGRATION.md |
| ❌ Exemplos inexistentes | ✅ Exemplos CRUD completos |
| ❌ Sem validação | ✅ Schemas JSON para validação AJV |

### Para Backend

| Antes | Depois |
|-------|--------|
| ❌ Spec desatualizado | ✅ Spec sincronizado com implementação |
| ❌ DTOs não documentados | ✅ DTOs em schemas referenciados |
| ❌ Erros inconsistentes | ✅ Erros em shape ApiError documentado |
| ❌ Sem contrato formal | ✅ Contrato formal em OpenAPI |

### Para Team

| Aspecto | Benefício |
|--------|-----------|
| **Sincronização** | Backend/Frontend alinhados em 1 spec |
| **Code Gen** | Frontend economiza dias de implementação |
| **Debugging** | correlationId rastreia requests ponta a ponta |
| **Validação** | Schemas garantem conformidade |
| **Documentação** | 550+ linhas README + 400+ FRONTEND_INTEGRATION.md |
| **Manutenção** | Checklist de 11 pontos para futuras mudanças |

---

## 📁 Arquivos Criados/Atualizados

```
✅ specs/shared/
├── openapi/
│   └── config-api.yaml                    (REESCRITO: 17 endpoints, operationIds)
├── domain/
│   └── schemas/
│       ├── process.schema.json            (existente, agora referenciado)
│       ├── processVersion.schema.json     (existente, agora referenciado)
│       ├── connector.schema.json          (existente, agora referenciado)
│       ├── apiError.schema.json           (existente, agora referenciado)
│       ├── aiError.schema.json            (existente, agora referenciado)
│       ├── previewRequest.schema.json     (existente, agora referenciado)
│       ├── previewResult.schema.json      (existente, agora referenciado)
│       ├── dslGenerateRequest.schema.json (existente, agora referenciado)
│       └── dslGenerateResult.schema.json  (existente, agora referenciado)
├── README.md                              (EXPANDIDO: 58 → 550+ linhas)
└── FRONTEND_INTEGRATION.md                (NOVO: 400+ linhas, guia completo)

✅ docs/
├── SPEC_UPDATE_SUMMARY.md                 (NOVO: este sumário)
├── DECISIONS.md                           (atualizado com versionamento)
└── API_VERSIONING.md                      (existente, referência)
```

---

## 🎓 Próximos Passos (Frontend)

### Imediato (Day 1)

```bash
# 1. Clonar/atualizar specs
git pull origin main
cd specs/shared/openapi

# 2. Gerar cliente TypeScript
npx openapi-generator-cli generate \
  -i config-api.yaml \
  -g typescript-axios \
  -o ../../src/api-client

# 3. Validar OpenAPI
npm install -D swagger-cli
swagger-cli validate config-api.yaml
```

### Semana 1

- [ ] Setup Axios com auth interceptor
- [ ] Implementar login (POST /api/auth/token)
- [ ] Testar health check (GET /api/health)
- [ ] Implementar Process CRUD

### Semana 2

- [ ] Implementar Connector CRUD
- [ ] Integrar Preview/Transform
- [ ] Integrar AI DSL generation
- [ ] Tests de integração

---

## 🔍 Validação

### OpenAPI YAML

```bash
swagger-cli validate specs/shared/openapi/config-api.yaml
# Result: ✅ Valid
```

### Schemas JSON

```bash
ajv validate -s specs/shared/domain/schemas/process.schema.json \
             -d specs/shared/examples/process.json
# Result: ✅ Valid
```

### Compatibilidade Backend

```bash
# Tests já passando
dotnet test tests/Contracts.Tests/ApiContractTests.cs
# Result: ✅ All passed
```

---

## 📞 Suporte & Dúvidas

| Dúvida | Resposta em |
|--------|-----------|
| "Como gerar client TS?" | FRONTEND_INTEGRATION.md §3 |
| "Quais endpoints existem?" | config-api.yaml ou README.md §5 |
| "Como autenticar?" | FRONTEND_INTEGRATION.md §2 |
| "Qual error handling?" | FRONTEND_INTEGRATION.md §5 |
| "Schemas de request?" | domain/schemas/*.schema.json |
| "Exemplos CRUD?" | FRONTEND_INTEGRATION.md §6 |
| "Validação TypeScript?" | FRONTEND_INTEGRATION.md §7 |
| "Como debugar?" | FRONTEND_INTEGRATION.md §8 |
| "Histórico de mudanças?" | docs/DECISIONS.md |
| "Versionamento strategy?" | docs/API_VERSIONING.md |

---

## 🎯 Conclusão

✅ **Specs prontas para consumo direto pelo frontend**

Deliverables:
1. ✅ OpenAPI 3.0.3 completo (17 endpoints, operationIds)
2. ✅ Frontend Integration Guide (400+ linhas)
3. ✅ README expandido (550+ linhas)
4. ✅ Todos os 9 schemas referenciados e documentados
5. ✅ Security schemes documentados (JWT Bearer)
6. ✅ Error handling documentado (com correlationId)
7. ✅ Code examples para backend e frontend
8. ✅ Validação scripts prontos
9. ✅ Checklist de 11 pontos para mudanças futuras
10. ✅ Commit documentado em git

---

**Version:** 1.1.3  
**Status:** ✅ **COMPLETE**  
**Data:** 2026-01-03  
**Próxima Etapa:** Frontend Implementation (openapi-generator + Axios setup)

