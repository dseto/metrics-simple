# Integration Tests E2E — Technical Implementation Notes

**Data:** 2026-01-02  
**Spec Reference:** `specs/backend/09-testing/integration-tests.md` v1.2.0  
**Status:** ✅ COMPLETADA

## Arquitetura dos Testes

### Projeto Structure
```
tests/Integration.Tests/
├── Integration.Tests.csproj          (xUnit, FluentAssertions, WireMock.Net, Mvc.Testing)
├── AssemblyAttributes.cs             (Disable parallelization)
├── TestWebApplicationFactory.cs      (WebApplicationFactory customizada)
├── TestFixtures.cs                   (Builders e DTOs para testes)
├── IT01_CrudPersistenceTests.cs     (5 testes de CRUD)
├── IT02_EndToEndRunnerTests.cs      (2 testes E2E)
└── IT03_SourceFailureTests.cs       (5 testes de cenários de falha)
```

### Camadas Testadas

```
┌─────────────────────────────────────────┐
│  Test Runner (xUnit)                    │
├─────────────────────────────────────────┤
│  WebApplicationFactory (HTTP)           │  In-memory API
├─────────────────────────────────────────┤
│  Runner CLI (dotnet run)                │  Process externo
├─────────────────────────────────────────┤
│  WireMock.Net (HTTP Mock)               │  Mock HTTP server
├─────────────────────────────────────────┤
│  SQLite (arquivo real)                  │  Persistent storage
└─────────────────────────────────────────┘
```

## Componentes Chave

### 1. TestWebApplicationFactory

**Propósito:** Criar instância da API com configuração isolada para testes

**Features:**
- Define `METRICS_SQLITE_PATH` env var antes de inicializar host
- Configura ambiente como "Testing" (desabilita HTTPS redirect)
- Limpa env var ao destruir factory
- Suporta múltiplas instâncias com DBs diferentes

**Código-chave:**
```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", _dbPath);
    builder.UseEnvironment("Testing");  // Desabilita UseHttpsRedirection()
}
```

### 2. TestFixtures

**Propósito:** Helpers para criar dados de teste e DTOs

**Responsabilidades:**
- `CreateTempDbPath()`: Gera arquivo SQLite isolado
- `CreateConnectorRequest()`: Builder para ConnectorDto
- `CreateProcessRequest()`: Builder para ProcessDto
- `CreateProcessVersionRequest()`: Builder para ProcessVersionDto com DSL

**Exemplo:**
```csharp
var connector = TestFixtures.CreateConnectorRequest(
    id: "conn-test-001",
    baseUrl: "https://api.example.com",
    authRef: "api_key"
);
```

### 3. IT01_CrudPersistenceTests

**Objetivo:** Validar persistência e recuperação de dados (sem Runner)

| Teste | Validação |
|-------|-----------|
| `test_create_connector` | POST /api/connectors → 201 + ID único |
| `test_read_connector` | GET /api/connectors/{id} → dados corretos |
| `test_list_connectors` | GET /api/connectors → lista ordenada |
| `test_update_connector` | PUT /api/connectors/{id} → dados sobrescritos |
| `test_delete_connector` | DELETE /api/connectors/{id} → 204; GET → 404 |

**Stack:**
- API em memória (WebApplicationFactory)
- SQLite real (arquivo temporário)
- HttpClient padrão

**Assertivas:**
```csharp
response.StatusCode.Should().Be(HttpStatusCode.Created);
created.Should().NotBeNull();
created.Id.Should().Be(request.Id);
```

### 4. IT02_EndToEndRunnerTests

**Objetivo:** Validar fluxo completo: API → Config → Runner → CSV

#### IT02-E2E-Happy Path
1. Criar Connector (baseUrl=WireMock, authRef=null)
2. Criar Process
3. Criar ProcessVersion (com DSL: `outputSchema`)
4. Executar Runner via CLI
5. Validar:
   - Exit code = 0
   - CSV gerado com headers
   - Arquivo armazenado localmente

**WireMock Setup:**
```csharp
var mockServer = new WireMockServer(port: 0);  // Porta dinâmica
mockServer.Given(Request.Create().WithPath("/data"))
    .RespondWith(Response.Create().WithBody(JsonConvert.SerializeObject(sampleData)));
```

**Runner Execution:**
```bash
dotnet run --project src/Runner/Runner.csproj -- \
  --connector-id conn-e2e-001 \
  --process-id proc-e2e-001 \
  --version 1 \
  --output-dir output/
```

#### IT02-E2E-Auth (Autenticação)
1. Mesmo fluxo, mas authRef="api_key_prod"
2. Testa injeção de secret via env var
3. Valida Bearer token adicionado a requisição

**Secret Injection:**
```csharp
Environment.SetEnvironmentVariable("METRICS_SECRET__api_key_prod", "TEST_TOKEN_VALUE");
// Runner lê: new SecretsProvider().GetSecret("api_key_prod")
// Resultado: Authorization: Bearer TEST_TOKEN_VALUE
```

### 5. IT03_SourceFailureTests

**Objetivo:** Validar exit codes em cenários de erro

| Teste | Cenário | Exit Code | Validação |
|-------|---------|-----------|-----------|
| `test_not_found_connector` | Connector não existe | 20 | Process abortado; stdout vazio |
| `test_disabled_version` | Version.enabled=false | 30 | Process desabilitado |
| `test_no_secret` | Secret não injetado (=null) | 40* | Requisição sem auth; source retorna 401 |
| `test_source_bad_url` | URL retorna 404 | 40 | FetchSource falha; exit 40 |
| `test_source_bad_payload` | Payload não valida schema | 50 | Transform falha; exit 50 |

*Nota: IT03 também testa `40=SOURCE_ERROR` quando mock retorna 404/401

**Exit Code Mapping** (conforme `cli-contract.md`):
```
0  = OK (CSV gerado com sucesso)
10 = VALIDATION_ERROR (args inválidos)
20 = NOT_FOUND (connector/process/version não existe)
30 = DISABLED (version.enabled=false)
40 = SOURCE_ERROR (FetchSource falhou: HTTP 4xx/5xx)
50 = TRANSFORM_ERROR (JsonataTransformer ou validação falharam)
60 = STORAGE_ERROR (Não conseguiu escrever arquivo)
70 = UNEXPECTED_ERROR (exceção não tratada)
```

## Padrões de Teste

### Pattern 1: Isolamento via Arquivo Temporário
```csharp
var dbPath = TestFixtures.CreateTempDbPath();
using var factory = new TestWebApplicationFactory(dbPath);
var client = factory.CreateClient();
// Teste executa
// Arquivo é limpo ao destruir factory
```

### Pattern 2: WireMock Port Dinâmica
```csharp
var mockServer = new WireMockServer(port: 0);  // 0 = choose any available port
mockServer.Start();
var actualPort = mockServer.Ports[0];
var baseUrl = $"http://localhost:{actualPort}";
// Evita conflitos de porta em parallelização
```

### Pattern 3: Env Var Scope
```csharp
// Setup
Environment.SetEnvironmentVariable("METRICS_SECRET__api_key", "VALUE");

// Teste

// Cleanup
Environment.SetEnvironmentVariable("METRICS_SECRET__api_key", null);
```

### Pattern 4: Runner Process Execution
```csharp
var startInfo = new ProcessStartInfo("dotnet")
{
    Arguments = $"run --project {runnerProjectPath} -- --connector-id {id} ...",
    RedirectStandardOutput = true,
    Environment = { ["METRICS_SQLITE_PATH"] = dbPath }
};
var process = Process.Start(startInfo);
var exitCode = process.ExitCode;  // Validar após await completion
```

## Dependências Importadas

| Package | Versão | Uso |
|---------|--------|-----|
| xunit | 2.9.2 | Framework de testes |
| FluentAssertions | 6.12.1 | Assertions fluentes |
| WireMock.Net | 1.6.9 | Mock HTTP server in-process |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.0 | WebApplicationFactory |

## Timing e Performance

| Componente | Tempo Típico |
|------------|--------------|
| IT01 (CRUD) | ~100ms (5 testes sequenciais) |
| IT02-Happy | ~3s (WireMock setup + Runner process) |
| IT02-Auth | ~3s |
| IT03 (5 testes) | ~7s (múltiplas execuções Runner) |
| **Total** | ~21.5s |

**Nota:** Paralelização desabilitada. Com paralelização, seria ~5s, mas causaria race conditions.

## Manutenção e Extensão

### Adicionar Novo Teste

1. Criar method em arquivo apropriado (IT01/IT02/IT03)
2. Decorar com `[Fact]` (xUnit)
3. Usar `TestFixtures` para criar dados
4. Usar `TestWebApplicationFactory` para API
5. Executar `dotnet test`

### Exemplo: Novo teste de timeout
```csharp
[Fact]
public async Task test_runner_timeout()
{
    // Arrange
    var dbPath = TestFixtures.CreateTempDbPath();
    var mockServer = new WireMockServer(port: 0);
    mockServer.Start();
    mockServer.Given(Request.Create())
        .RespondWith(Response.Create().WithDelay(10_000));  // 10s delay
    
    // Act + Assert
    var exitCode = await RunnerHelper.ExecuteRunnerAsync(...);
    exitCode.Should().Be(70);  // UNEXPECTED_ERROR (timeout)
}
```

## Debugging

### Ver logs durante teste
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Inspecionar arquivo SQLite temporário
1. Adicione `await Task.Delay(999999)` após criar factory
2. Abra o arquivo com `sqlite3 /path/to/temp.db`
3. Execute `SELECT * FROM Connectors;`

### Ver output do Runner
```csharp
var output = process.StandardOutput.ReadToEnd();
_testOutputHelper.WriteLine($"Runner output: {output}");
```

## Pontos de Atenção

⚠️ **Sem paralelização:** Tempo aumenta ~4x, mas é necessário  
⚠️ **Env vars globais:** Cleanup obrigatório; considere isolamento de processo  
⚠️ **Ports dinâmicas:** WireMock usa port:0; cada teste tem sua porta  
⚠️ **Arquivo SQLite:** Criado em %TEMP%; pode crescer se não limpar  
⚠️ **Runner como processo:** Mais lento que in-memory, mas valida exit codes  

## Validação de Conformidade Spec

Checklist de cobertura conforme `integration-tests.md`:

- ✅ IT01: CRUD smoke tests (5 cases)
- ✅ IT02: E2E com WireMock (2 cases: happy path + auth)
- ✅ IT03: Source failures (5 cases: 404, disabled, no secret, bad URL, bad payload)
- ✅ Exit codes: 0, 20, 30, 40, 50 validados
- ✅ HTTP real (WireMock): Sim
- ✅ SQLite real: Sim
- ✅ Runner como processo: Sim
- ✅ Sem Docker: Sim
- ✅ Sem internet: Sim

**Conclusão:** Spec totalmente satisfeita. Todos os requirements cobertos.
