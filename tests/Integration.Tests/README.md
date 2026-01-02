# Integration Tests — E2E Backend Validation

**Spec Reference:** `specs/backend/09-testing/integration-tests.md` v1.2.0  
**Status:** ✅ 12/12 tests passing  
**Stack:** xUnit 2.9.2 + WireMock.Net 1.6.9 + WebApplicationFactory

---

## Test Suites Overview

### IT01 - CRUD + Persistence (5 tests)

**Objetivo:** Validar persistência de dados sem executar Runner

**Testes:**
- `test_create_connector`: POST /api/connectors → 201, ID único, dados persistem
- `test_read_connector`: GET /api/connectors/{id} → 200, dados corretos
- `test_list_connectors`: GET /api/connectors → 200, lista ordenada (ASC)
- `test_update_connector`: PUT /api/connectors/{id} → 200, dados sobrescritos
- `test_delete_connector`: DELETE /api/connectors/{id} → 204; GET → 404

**Stack:**
- WebApplicationFactory em memória
- SQLite real (arquivo temporário isolado)
- HttpClient padrão do ASP.NET

---

### IT02 - E2E: API → Runner → CSV (2 tests - MANDATORY)

**Objetivo:** Validar fluxo completo end-to-end com HTTP real (mockado)

#### IT02-Happy Path
1. Criar Connector (baseUrl=WireMock, sem auth)
2. Criar Process
3. Criar ProcessVersion (com Jsonata DSL)
4. Executar Runner como processo externo
5. Validar:
   - Exit code = 0
   - WireMock registrou requisição GET /data
   - CSV gerado com headers corretos
   - Arquivo armazenado em output/

#### IT02-Auth (Autenticação via Environment Variables)
1. Connector com authRef="api_key_prod"
2. Injetar secret via `METRICS_SECRET__api_key_prod=TEST_TOKEN`
3. Runner lê secret e adiciona `Authorization: Bearer TEST_TOKEN`
4. WireMock valida header

**Decisão:** Secrets via env var em vez de arquivo local para isolar testes

---

### IT03 - Source Failures (5 tests - MANDATORY)

**Objetivo:** Validar comportamento correto em cenários de erro

| Teste | Cenário | Exit Code | Descrição |
|-------|---------|-----------|-----------|
| `test_not_found_connector` | Connector não existe | 20 | Process abortado imediatamente |
| `test_disabled_version` | version.enabled=false | 30 | Version desabilitada; não executa |
| `test_no_secret` | authRef existe, mas secret não | 40 | FetchSource recebe 401 Unauthorized |
| `test_source_bad_url` | WireMock retorna 404 | 40 | FetchSource falha; exit 40 |
| `test_bad_payload` | JSON não valida outputSchema | 50 | Transform falha; exit 50 |

**Exit Code Mapping (conforme `cli-contract.md`):**
```
0  = OK (CSV gerado com sucesso)
20 = NOT_FOUND (connector/process/version não existe)
30 = DISABLED (version.enabled=false)
40 = SOURCE_ERROR (FetchSource falhou: HTTP 4xx/5xx/timeout)
50 = TRANSFORM_ERROR (JsonataTransformer ou validação schema falharam)
60 = STORAGE_ERROR (Não conseguiu escrever arquivo)
70 = UNEXPECTED_ERROR (exceção não tratada)
```

---

## Problemas Encontrados e Soluções

### P1: "No such table: ProcessVersion" ao executar Runner

**Sintoma:**
```
dotnet run: Database error - no such table: ProcessVersion
```

**Causa-raiz:**  
Runner executado como processo separado (`dotnet run`) iniciava sem tabelas SQLite. A API criava as tabelas apenas em memória ao inicializar.

**Solução Implementada:**
```csharp
// Em PipelineOrchestrator.cs
public async Task<int> ExecuteAsync(...)
{
    _databaseProvider.InitializeDatabase(_dbPath);  // Cria schema antes de usar
    // ... resto da execução
}
```

**Arquivo:** `src/Runner/PipelineOrchestrator.cs`  
**Status:** ✅ Resolvido

---

### P2: Race Conditions (Testes em paralelo)

**Sintoma:**
```
Test1 sets METRICS_SQLITE_PATH=/tmp/test1.db
Test2 sets METRICS_SQLITE_PATH=/tmp/test2.db  // simultaneamente!
Test1 lê /tmp/test2.db em vez de /tmp/test1.db
Result: Random failures (~30%)
```

**Causa-raiz:**  
xUnit roda testes em paralelo. Testes compartilham variáveis globais (environment variables).

**Solução Implementada:**
```csharp
// Em AssemblyAttributes.cs
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
```

**Trade-off:**  
- Sem paralelização: ~20s (sequencial, determinístico) ✅
- Com paralelização: ~5s (rápido, mas flaky)

**Arquivo:** `tests/Integration.Tests/AssemblyAttributes.cs`  
**Status:** ✅ Resolvido

---

### P3: HTTPS Redirection Warnings

**Sintoma:**
```
[14:28:33 WRN] Failed to determine the https port for redirect.
[14:28:33 WRN] Failed to determine the https port for redirect.
... (repetido 20+ vezes)
```

**Causa-raiz:**  
Middleware `app.UseHttpsRedirection()` ativo em ambiente HTTP-only de testes.

**Solução Implementada:**
```csharp
// Em Program.cs
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}
```

E em TestWebApplicationFactory:
```csharp
builder.UseEnvironment("Testing");
```

**Arquivo:** `src/Api/Program.cs`, `tests/Integration.Tests/TestWebApplicationFactory.cs`  
**Status:** ✅ Resolvido (logs agora clean)

---

### P4: Injeção de Secrets em Testes

**Sintoma:**
```
Runner precisa autenticar com API mock.
Como injetar secret para teste sem ler arquivo local?
```

**Causa-raiz:**  
Testes isolados não têm acesso confiável a `secrets.local.json`.

**Solução Implementada:**
```csharp
// Precedência em PipelineOrchestrator:
var secret = Environment.GetEnvironmentVariable($"METRICS_SECRET__{authRef}")  // env var
             ?? new SecretsProvider().GetSecret(authRef)                        // arquivo
             ?? null;                                                            // sem secret
```

**Arquivo:** `src/Runner/PipelineOrchestrator.cs`  
**Status:** ✅ Resolvido

---

## Environment Variables

| Variable | Escopo | Descrição |
|----------|--------|-----------|
| `METRICS_SQLITE_PATH` | API + Runner | Path do arquivo SQLite (temp durante testes) |
| `METRICS_SECRET__<authRef>` | Runner | Secret para connector auth (e.g., api_key_prod) |

**Precedência (em ordem):**
1. CLI args (se aplicável)
2. Environment variables
3. Config file (appsettings.json)
4. Default value

---

## Executar os Testes

### Todos os testes
```bash
dotnet test
```

### Apenas Integration.Tests
```bash
dotnet test tests/Integration.Tests/Integration.Tests.csproj
```

### Teste específico por classe
```bash
dotnet test --filter "FullyQualifiedName~IT02"
```

### Ver logs detalhados
```bash
dotnet test -v n --logger "console;verbosity=detailed"
```

---

## Requisitos & Constraints

✅ **Sem Docker:** WireMock.Net roda in-process  
✅ **Sem internet:** Todos os HTTP calls vão para localhost:WireMock  
✅ **Determinístico:** Mesmas entradas → mesmos exit codes + CSV  
✅ **Isolado:** Cada teste usa arquivo SQLite separado  
✅ **Limpo:** Arquivos temporários deletados automaticamente  

---

## Performance

| Componente | Tempo |
|-----------|-------|
| IT01 (5 testes CRUD) | ~100ms |
| IT02-Happy | ~3s |
| IT02-Auth | ~3s |
| IT03 (5 testes) | ~7s |
| **Total** | ~21.5s |

---

## Validação contra Spec

| Requirement | Status |
|-------------|--------|
| IT01 CRUD smoke | ✅ |
| IT02 E2E mandatory | ✅ |
| IT03 Failures mandatory | ✅ |
| Exit codes (0,20,30,40,50) | ✅ |
| HTTP real (WireMock) | ✅ |
| SQLite real | ✅ |
| Runner como processo | ✅ |
| Sem Docker | ✅ |
| Sem internet | ✅ |

**Conclusão:** Spec totalmente satisfeita ✅

---

## Documentação Adicional

Para mais detalhes técnicos, consulte:
- [INTEGRATION_TESTS_TECHNICAL_NOTES.md](../../docs/INTEGRATION_TESTS_TECHNICAL_NOTES.md) — Arquitetura, padrões, debugging
- [DECISIONS.md](../../docs/DECISIONS.md) — Decisões de engenharia (seção Etapa 5)
