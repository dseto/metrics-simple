# 📝 Guia para Preencher Gaps na Spec Deck

**Data:** 7 de Janeiro de 2026  
**Objetivo:** Eliminar tribal knowledge e documentar decisões arquiteturais

---

## 🔴 CRÍTICO (1-2 horas) - FAZER PRIMEIRO

### Gap 1: LLM Provider Abstraction

**Arquivo:** `specs/backend/08-ai-assist/02-llm-provider-abstraction.md`  
**Tempo Estimado:** 90 minutos

**Seções Recomendadas:**

```markdown
# LLM Provider Abstraction

## 1. Visão Geral
- O quê: Arquitetura de providers LLM
- Por quê: Permitir múltiplos provedores com mesma interface
- Quando: Design-time (geração de DSL)

## 2. Interface IAiProvider
```csharp
public interface IAiProvider
{
    Task<DslGenerateResult> GenerateDslAsync(DslGenerateRequest request, CancellationToken ct);
    string ProviderName { get; }
}
```

## 3. Implementações Disponíveis

### HttpOpenAiCompatibleProvider (OpenRouter) - ÚNICO EM USO
- **Endpoint:** `https://openrouter.ai/api/v1/chat/completions`
- **Configuração:** AiConfiguration (Provider, EndpointUrl, Model, ApiKey)
- **Modelos suportados:** DeepSeek, Hermes, GPT-4, etc.
- **Headers especiais:** OpenRouter-Title, OpenRouter-Tags
- **Features:** Structured outputs, response healing

### MockProvider (Futuro)
- Para testes determinísticos
- Não implementado ainda
- Seria registrado quando Provider == "MockProvider"

## 4. Configuração AiConfiguration

| Campo | Tipo | Padrão | Env Var | Obrigatório |
|-------|------|--------|---------|-------------|
| Enabled | bool | false | - | Não |
| Provider | string | "HttpOpenAICompatible" | - | Sim |
| EndpointUrl | string | "https://openrouter.ai/api/v1/chat/completions" | - | Sim |
| Model | string | "nousresearch/hermes-3-llama-3.1-405b" | - | Sim |
| ApiKey | string? | null | METRICS_OPENROUTER_API_KEY | Só se Enabled=true |
| TimeoutSeconds | int | 30 | - | Não |
| MaxRetries | int | 1 | - | Não |
| Temperature | double | 0.0 | - | Não |
| MaxTokens | int | 4096 | - | Não |
| EnableStructuredOutputs | bool | true | - | Não |
| EnableResponseHealing | bool | true | - | Não |

## 5. Error Handling
- AiProviderException
- Error codes: AI_DISABLED, AI_TIMEOUT, AI_RATE_LIMITED, AI_OUTPUT_INVALID
- Retry strategy: exponential backoff

## 6. Exemplos
- Como testar localmente com OpenRouter
- Como adicionar novo provider
- Troubleshooting
```

**Referências no código:**
- `src/Api/AI/IAiProvider.cs`
- `src/Api/AI/AiModels.cs`
- `src/Api/AI/Engines/Ai/AiLlmProvider.cs`
- `src/Api/Program.cs` (linhas 130-150)

---

## 🟡 ALTOS (2-4 horas cada) - FAZER SEGUNDO

### Gap 2: Dependency Injection Setup

**Arquivo:** `specs/backend/04-execution/02-dependency-injection.md`  
**Tempo Estimado:** 2 horas

**Seções Recomendadas:**

```markdown
# Dependency Injection & Service Registration

## 1. Por quê é importante?
Bug recente: IHttpClientFactory não era registrado → falha em 10 testes
Lição: Sem documentação clara, fácil esquecer registros críticos

## 2. Registros Obrigatórios em Program.cs

### HTTP Client (NOVO - CRÍTICO)
```csharp
// ✅ NECESSÁRIO para LLM provider
builder.Services.AddHttpClient("AI");
```

### Repositories
```csharp
builder.Services.AddScoped<IProcessRepository>(_ => new ProcessRepository(dbPath));
builder.Services.AddScoped<IProcessVersionRepository>(...);
// etc
```

### Engine Services
```csharp
builder.Services.AddScoped<ISchemaValidator, SchemaValidator>();
builder.Services.AddScoped<ICsvGenerator, CsvGenerator>();
builder.Services.AddScoped<EngineService>();
```

### Auth Services
```csharp
builder.Services.AddAuthServices(authOptions);
builder.Services.AddAuthRateLimiting(authOptions);
```

### AI Engine (Condicional)
```csharp
builder.Services.AddScoped<AiEngine>(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY");
    AiLlmProvider? llmProvider = null;
    if (!string.IsNullOrEmpty(apiKey))
    {
        llmProvider = new AiLlmProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("AI"),
            sp.GetRequiredService<AiConfiguration>(),
            sp.GetRequiredService<ILogger<AiLlmProvider>>());
    }
    return new AiEngine(..., llmProvider);
});
```

## 3. Variáveis de Ambiente Críticas

| Var | Exemplo | Obrigatório | Quando |
|-----|---------|-------------|--------|
| METRICS_OPENROUTER_API_KEY | sk-or-v1-... | Não | Se AI.Enabled=true |
| METRICS_SQLITE_PATH | /data/config.db | Não | Pode usar default |
| Auth__Mode | LocalJwt | Não | Padrão é LocalJwt |
| METRICS_SECRET_KEY | (32+ chars) | Sim | Token encryption |

## 4. Troubleshooting: IHttpClientFactory

### Erro: "No service for type 'System.Net.Http.IHttpClientFactory' has been registered"
```
✅ Solução: Adicionar builder.Services.AddHttpClient("AI");
   Localização: src/Api/Program.cs, antes de AddScoped<AiEngine>
```

## 5. Ordem de Registro (Por quê importa)
- AddHttpClient() deve vir ANTES de AddScoped<AiEngine>()
- AddAuthServices() deve vir ANTES de usar em middleware

## 6. Como Adicionar Novo Serviço
- Decidir Lifecycle: Singleton, Scoped ou Transient?
- Registrar ANTES de usar em outro serviço
- Testar que resolve sem erros
```

**Referências no código:**
- `src/Api/Program.cs` (linhas 70-155)
- Arquivo `.env` (se existir)

---

### Gap 3: IT13 Integration Tests

**Arquivo:** `specs/backend/09-testing/02-it13-llm-integration-tests.md`  
**Tempo Estimado:** 2 horas

**Seções Recomendadas:**

```markdown
# IT13 — LLM Assisted DSL Flow Tests

## 1. Propósito
- Testar fluxo completo: Goal Text → LLM Generate DSL → Transform → CSV
- Usa OpenRouter (LLM real)
- Valida que transformações funcionam end-to-end

## 2. Por quê 4 testes são skipped?
Testes de LLM requerem API key real:
- `LLM_SimpleExtraction_PortuguesePrompt`
- `LLM_Aggregation_EnglishPrompt`
- `LLM_WeatherForecast_RealWorldPrompt`
- `LLM_ComplexTransformation_MixedLanguage`

**Comportamento esperado:** Skip quando `METRICS_OPENROUTER_API_KEY` não configurada

## 3. Como Rodar IT13

### Apenas testes PLAN_V1 (determinísticos):
```bash
dotnet test --filter "PlanV1"
# Todos passam sem API key
```

### Com testes LLM (precisa API key):
```bash
export METRICS_OPENROUTER_API_KEY="sk-or-v1-..."
dotnet test --filter "IT13_LLMAssistedDslFlowTests"
```

## 4. Testes PLAN_V1 (Determinísticos)
- PlanV1_MapValue
- PlanV1_SelectAll_T1
- PlanV1_GroupBy_Avg
- PlanV1_SimpleExtraction_WithResultsWrapper
- PlanV1_WeatherForecast_NestedPath
- PlanV1_Limit_TopN
- PlanV1_SimpleExtraction_WithItemsWrapper
- PlanV1_Aggregation_EnglishPrompt
- PlanV1_SimpleExtraction_PortuguesePrompt_RootArray
- PlanV1_SelectWithFilter

**Resultado esperado:** ✅ Todos passam

## 5. Testes LLM (Requerem OpenRouter)
- LLM_* (4 testes)

**Resultado esperado:** ⏭️ Skipped se sem API key, ✅ Passam se com key

## 6. Estrutura do Teste
```csharp
[Fact]
public async Task PlanV1_MapValue()
{
    // 1. Arrange: Criar sample input + goal
    var sampleInput = new { data = new[] { 1, 2, 3 } };
    var goal = "Extract data from input";
    
    // 2. Act: Chamar GenerateDslAsync
    var dslResult = await GenerateDslAsync(sampleInput, goal);
    
    // 3. Transform: Executar DSL com input
    var transformed = await ExecuteTransformAsync(sampleInput, dslResult);
    
    // 4. Assert: Validar resultado
    transformed.Should().NotBeNull();
    transformed.CsvPreview.Should().NotBeEmpty();
}
```

## 7. Como Adicionar Novo Caso de Teste

1. Criar novo método [Fact]
2. Definir sampleInput e goalText
3. Chamar GenerateDslAsync e ExecuteTransformAsync
4. Validar resultado
5. Adicionar ao relatório de testes

## 8. Troubleshooting

### "Tests are skipped"
→ Normal se METRICS_OPENROUTER_API_KEY não configurada
→ Use `dotnet test --filter "PlanV1"` para testes determinísticos

### "API rate limited"
→ Aguarde alguns minutos
→ Verifique se API key tem saldo em OpenRouter

### "IHttpClientFactory not registered"
→ Bug já corrigido: AddHttpClient("AI") em Program.cs
→ Rodar `dotnet build` novamente
```

**Referências no código:**
- `tests/Integration.Tests/IT13_LLMAssistedDslFlowTests.cs`
- `tests/Integration.Tests/TestFixtures.cs`

---

### Gap 4: Environment Configuration

**Arquivo:** `specs/backend/04-execution/03-environment-configuration.md`  
**Tempo Estimado:** 1.5 horas

**Seções Recomendadas:**

```markdown
# Environment Configuration (Dev / Test / Prod)

## 1. Matriz de Configuração

| Setting | Development | Testing | Production |
|---------|-------------|---------|------------|
| **Database** | SQLite local | SQLite temp | SQLite/blob |
| **Auth Mode** | LocalJwt | Off | Okta/Entra |
| **OpenRouter** | Optional | Mock | REQUIRED |
| **HTTPS** | No | No | Yes (required) |
| **CORS** | * | * | Restricted |
| **Debug Logs** | Yes | Yes | No |

## 2. Development Setup

### appsettings.Development.json
```json
{
  "Database": { "Path": "./config/config.db" },
  "Auth": { "Mode": "LocalJwt" },
  "AI": { "Enabled": true, "Provider": "HttpOpenAICompatible" }
}
```

### Environment Variables
```bash
# Opcional - se quer testar com LLM real
export METRICS_OPENROUTER_API_KEY="sk-or-v1-..."

# Padrão é LocalJwt com bootstrap admin
export Auth__Mode="LocalJwt"
```

### Como Rodar
```bash
dotnet run --project src/Api
# API em http://localhost:8080
# Bootstrap admin: admin/ChangeMe123!
```

## 3. Testing Environment

### Ambiente: "Testing"
```csharp
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection(); // Skip em testes
}
```

### Variáveis de Teste
```bash
METRICS_SQLITE_PATH=/tmp/test.db
Auth__Mode=Off
```

### Resultado
- SQLite em arquivo temp
- Sem autenticação
- Sem HTTPS redirect
- Logs de debug

## 4. Production Checklist

- [ ] OpenRouter API key configurada (env var ou secrets)
- [ ] SQLite path em volume persistente
- [ ] HTTPS ativado (redirect)
- [ ] CORS restringido (AllowedOrigins configurado)
- [ ] Auth mode: Okta ou Entra (não LocalJwt!)
- [ ] Logs: apenas Info/Warn/Error (sem Debug)
- [ ] Database backup: configurado
- [ ] Monitoriamento: APM e alertas

## 5. Docker Deployment

### docker-compose.yaml
```yaml
services:
  api:
    environment:
      - METRICS_OPENROUTER_API_KEY=${OPENROUTER_KEY}
      - METRICS_SQLITE_PATH=/data/config.db
      - DOTNET_ENVIRONMENT=Production
```

### Como Deployer
```bash
export OPENROUTER_KEY="sk-or-v1-..."
docker-compose up -d
```

## 6. CI/CD Validation

Antes de mergear:
- [ ] Build passa em Release mode
- [ ] Testes passam (exceto LLM se sem key)
- [ ] Nenhum secret em appsettings.json
- [ ] HTTPS configurado para prod
```

**Referências no código:**
- `src/Api/appsettings*.json` (3 arquivos)
- `compose.yaml`
- `src/Api/Program.cs` (linhas 50-60)

---

## 🟡 MÉDIO (1 hora) - FAZER TERCEIRO

### Gap 5: Tech Debt & Incompletos

**Arquivo:** `docs/TECH_DEBT.md` (novo)  
**Tempo Estimado:** 1 hora

**Conteúdo Recomendado:**

```markdown
# Technical Debt & Incompletos

## 1. IT04_AiDslGenerateTests — Comentado/Incompleto

**Status:** 🔴 BLOQUEADO  
**Localização:** `tests/Integration.Tests/IT04_AiDslGenerateTests.cs`  
**Razão:** Depende de `MockAiProvider` nunca implementado

### Problema
- Documentação de Gemini mencionava classes `MockAiProvider` e `HttpOpenAiCompatibleProvider`
- Estas classes nunca foram implementadas
- Teste foi comentado integralmente para evitar erros de compilação

### Como Completar (Futuro)
1. Implementar `MockAiProvider` para testes determinísticos
2. Usar WireMock.Net para simular OpenRouter responses
3. Descomentar IT04 e adicionar casos de teste

### Prioridade
🟢 BAIXO — Não é crítico, temos IT13 funcionando com OpenRouter real

---

## 2. Outros Tech Debts (se houver)

(Adicionar conforme descobertos)
```

---

## 🟢 BAIXO (30 min) - FAZER POR ÚLTIMO

### Gap 6: OpenRouter Setup Guide

**Arquivo:** `specs/backend/08-ai-assist/01-openrouter-setup.md`  
**Tempo Estimado:** 30 minutos

**Conteúdo Recomendado:**

```markdown
# OpenRouter Setup & Configuration

## 1. Obter API Key

1. Acesse https://openrouter.ai
2. Criar conta ou fazer login
3. Ir em Dashboard → Keys
4. Gerar nova API key
5. Copiar (formato: `sk-or-v1-...`)

## 2. Adicionar Saldo

OpenRouter usa modelo de pay-as-you-go:
- Adicione crédito via cartão de crédito
- Ou: adicione seu próprio LLM provider

## 3. Modelos Disponíveis

| Modelo | Custo | Velocidade | Qualidade | Recomendação |
|--------|-------|-----------|-----------|--------------|
| deepseek/deepseek-chat-v3.1 | 💰 | ⚡⚡ | ⭐⭐⭐ | Produção |
| nousresearch/hermes-3-llama | 💰💰 | ⚡ | ⭐⭐⭐ | Alternativa |
| openai/gpt-4 | 💰💰💰 | ⚡ | ⭐⭐⭐⭐ | Casos complexos |

## 4. Local Testing

```bash
export METRICS_OPENROUTER_API_KEY="sk-or-v1-..."
dotnet run --project src/Api

# Em outro terminal:
curl -X POST http://localhost:8080/api/v1/ai/dsl/generate \
  -H "Content-Type: application/json" \
  -d '{
    "goalText": "Extract id and name from each person",
    "sampleInput": [{"id": 1, "name": "Alice"}],
    "dslProfile": "ir",
    "constraints": {"maxColumns": 50}
  }'
```

## 5. Rate Limits & Throttling

- OpenRouter: ~60 req/min (varia por modelo)
- Exponential backoff implementado em AiLlmProvider
- Se rate limited: erro, aguarde, tente novamente

## 6. Troubleshooting

### "401 Unauthorized"
→ API key inválida ou expirada
→ Verificar: $env:METRICS_OPENROUTER_API_KEY

### "429 Too Many Requests"
→ Rate limit atingido
→ Aguarde 1-2 minutos, tente novamente

### "500 Internal Server Error"
→ Pode ser problema no OpenRouter
→ Verificar status: https://status.openrouter.ai

### "Model not available"
→ Modelo inválido
→ Verificar lista de modelos: https://openrouter.ai/docs#models
```

---

## 📋 Checklist de Implementação

```markdown
- [ ] Criar 02-llm-provider-abstraction.md (CRÍTICO)
- [ ] Criar 02-dependency-injection.md (ALTO)
- [ ] Criar 02-it13-llm-integration-tests.md (ALTO)
- [ ] Criar 03-environment-configuration.md (ALTO)
- [ ] Criar docs/TECH_DEBT.md (MÉDIO)
- [ ] Criar 01-openrouter-setup.md (BAIXO)
- [ ] Revisar com team
- [ ] Mergear para main
- [ ] Anunciar que tribal knowledge foi documentada ✅
```

---

**Total Estimado:** 5-7 horas  
**Recomendação:** Fazer 1-2 por dia

