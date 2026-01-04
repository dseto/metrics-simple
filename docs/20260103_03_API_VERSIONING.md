# API Versioning Guide

**Data:** 2026-01-03  
**Versão da API:** v1  
**Spec:** `specs/shared/openapi/config-api.yaml`

## Visão Geral

A API do Metrics Simple segue as melhores práticas de versionamento de APIs REST, usando prefixo `/api/v1` para todos os endpoints de negócio.

## Estrutura de URLs

### Base URL
```
http://localhost:8080/api/v1
```

### Endpoints Versionados

Todos os endpoints de negócio usam o prefixo `/api/v1`:

| Recurso | Endpoint Completo | Método | Auth |
|---------|-------------------|--------|------|
| **Processes** | | | |
| List processes | `/api/v1/processes` | GET | Reader |
| Create process | `/api/v1/processes` | POST | Admin |
| Get process | `/api/v1/processes/{id}` | GET | Reader |
| Update process | `/api/v1/processes/{id}` | PUT | Admin |
| Delete process | `/api/v1/processes/{id}` | DELETE | Admin |
| **Process Versions** | | | |
| Create version | `/api/v1/processes/{processId}/versions` | POST | Admin |
| Get version | `/api/v1/processes/{processId}/versions/{version}` | GET | Reader |
| Update version | `/api/v1/processes/{processId}/versions/{version}` | PUT | Admin |
| **Connectors** | | | |
| List connectors | `/api/v1/connectors` | GET | Reader |
| Create connector | `/api/v1/connectors` | POST | Admin |
| **Preview & AI** | | | |
| Preview transform | `/api/v1/preview/transform` | POST | Reader |
| Generate DSL (AI) | `/api/v1/ai/dsl/generate` | POST | Reader |

### Endpoints Sem Versionamento (Exceções)

Estes endpoints **não** usam o prefixo `/api/v1`:

| Recurso | Endpoint | Método | Auth | Motivo |
|---------|----------|--------|------|--------|
| Health check | `/api/health` | GET | None | Global, não versionado por design |
| Login | `/api/auth/token` | POST | None | Auth infra-level |
| List users | `/api/auth/users` | GET | Admin | Auth infra-level |
| Create user | `/api/auth/users` | POST | Admin | Auth infra-level |
| Update user | `/api/auth/users/{userId}` | PUT | Admin | Auth infra-level |

## Implementação Backend

### ASP.NET Core Minimal API

```csharp
// 1. Health check (sem versionamento)
app.MapGet("/api/health", GetHealth)
    .WithName("Health")
    .AllowAnonymous();

// 2. Auth endpoints (sem versionamento)
var authGroup = app.MapGroup("/api/auth")
    .WithTags("Auth");

authGroup.MapPost("/token", LoginHandler);

// 3. API v1 (TODOS os endpoints de negócio)
var v1 = app.MapGroup("/api/v1");

var processGroup = v1.MapGroup("/processes")
    .WithTags("Processes");

processGroup.MapGet("/", GetAllProcesses)
    .RequireAuthorization(AuthPolicies.Reader);

// Resultado: GET /api/v1/processes
```

### Location Headers

Respostas `201 Created` devem incluir `/api/v1` no Location:

```csharp
async Task<IResult> CreateProcess(ProcessDto process, IProcessRepository repo)
{
    var created = await repo.CreateProcessAsync(process);
    return Results.Created($"/api/v1/processes/{created.Id}", created);
}
```

## OpenAPI Specification

### Server Configuration

```yaml
openapi: 3.0.3
info:
  title: MetricsSimple Config API
  version: 1.1.0
  description: |
    **Versionamento:**
    - Todos os endpoints de negócio usam `/api/v1` como prefixo
    - Exceções (sem versionamento): `/api/health`, `/api/auth/*`

servers:
- url: http://localhost:8080/api/v1
  description: Local dev (API v1)
```

### Path Definitions

Os paths **não incluem** `/api/v1` pois isso está no `servers.url`:

```yaml
paths:
  /health:           # → http://localhost:8080/api/health (exceção)
    get: ...
  
  /processes:        # → http://localhost:8080/api/v1/processes
    get: ...
  
  /connectors:       # → http://localhost:8080/api/v1/connectors
    get: ...
```

## Integração Frontend

### Cliente HTTP (Axios)

```typescript
import axios from 'axios';

const apiClient = axios.create({
  baseURL: 'http://localhost:8080/api/v1',  // Base URL com versão
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json'
  }
});

// Uso:
apiClient.get('/processes');           // → GET http://localhost:8080/api/v1/processes
apiClient.post('/connectors', data);   // → POST http://localhost:8080/api/v1/connectors

// Health check (exceção - sem baseURL versionado)
axios.get('http://localhost:8080/api/health');
```

### Cliente Gerado pelo OpenAPI

Se usar um gerador de cliente (ex: openapi-generator):

```bash
openapi-generator-cli generate \
  -i specs/shared/openapi/config-api.yaml \
  -g typescript-axios \
  -o src/api-client
```

O cliente gerado usará automaticamente `http://localhost:8080/api/v1` como base URL.

## CORS

CORS está configurado para permitir requests do frontend:

```http
# Request
GET /api/v1/processes HTTP/1.1
Host: localhost:8080
Origin: http://localhost:4200

# Response
HTTP/1.1 200 OK
Access-Control-Allow-Origin: http://localhost:4200
Access-Control-Allow-Credentials: true
```

## Testes de Validação

### Health Check (sem versão)
```bash
curl -i http://localhost:8080/api/health
# HTTP/1.1 200 OK
# {"status":"ok"}
```

### Processo (com versão)
```bash
curl -i http://localhost:8080/api/v1/processes \
  -H "Authorization: Bearer <token>"
# HTTP/1.1 200 OK
# []
```

### CORS Preflight
```bash
curl -i -X OPTIONS http://localhost:8080/api/v1/processes \
  -H "Origin: http://localhost:4200" \
  -H "Access-Control-Request-Method: GET"
# HTTP/1.1 204 No Content
# Access-Control-Allow-Origin: http://localhost:4200
# Access-Control-Allow-Methods: GET
```

## Migração Futura (v2)

Quando precisar introduzir breaking changes:

1. **Backend:**
   ```csharp
   var v2 = app.MapGroup("/api/v2");
   var processGroupV2 = v2.MapGroup("/processes");
   // Novo comportamento
   ```

2. **OpenAPI:**
   - Criar novo arquivo: `config-api-v2.yaml`
   - Ou adicionar server alternativo:
     ```yaml
     servers:
     - url: http://localhost:8080/api/v1
     - url: http://localhost:8080/api/v2
     ```

3. **Frontend:**
   - Migrar gradualmente para `/api/v2`
   - Manter compatibilidade com v1 durante período de transição

## Checklist de Compliance

Ao adicionar novos endpoints:

- [ ] Endpoint de negócio usa `v1.MapGroup()` ou subgrupo de v1?
- [ ] Location header em `201 Created` inclui `/api/v1`?
- [ ] Endpoint documentado no `config-api.yaml` com path correto?
- [ ] Testes validam URL completa incluindo versão?
- [ ] CORS habilitado se endpoint será chamado do frontend?
- [ ] Política de autorização definida (Reader/Admin)?

## Referências

- **OpenAPI Spec:** `specs/shared/openapi/config-api.yaml`
- **Shared README:** `specs/shared/README.md`
- **Backend Code:** `src/Api/Program.cs`
- **Decisões:** `docs/DECISIONS.md`
- **Agent Instructions:** `.github/agents/spec-driven-dockerizer.agent.md`
