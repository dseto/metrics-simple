# Gemini LLM Provider Integration Guide

**Data:** 2026-01-06  
**Status:** ✅ Implementado e testado  
**Versão:** 1.0

---

## 📋 Resumo

Este documento explica como usar o **Google Gemini** como provedor LLM alternativo no Metrics Simple. O suporte foi adicionado de forma modular, permitindo trocar entre OpenRouter (padrão) e Gemini configurando uma variável de ambiente e atualizando `appsettings.json`.

---

## 🚀 Quick Start (Testes com Gemini)

### 1. Obter API Key do Gemini

1. Vá para [Google AI Studio](https://aistudio.google.com/app/apikeys)
2. Clique em **"Create API Key"**
3. Selecione projeto (ou crie um novo)
4. Copie a chave gerada

### 2. Configurar Variável de Ambiente

```powershell
# Windows PowerShell
$env:METRICS_GEMINI_API_KEY = "*"

# Linux/Mac bash
export METRICS_GEMINI_API_KEY="*"

# Ou adicionar no .env / docker-compose.yaml
```

### 3. Atualizar `appsettings.json`

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "Gemini",
    "EndpointUrl": "https://generativelanguage.googleapis.com/v1beta/models",
    "Model": "gemini-2.5-flash",
    "PromptVersion": "2.0.0",
    "TimeoutSeconds": 60,
    "MaxRetries": 1,
    "Temperature": 0.0,
    "MaxTokens": 4096,
    "TopP": 0.9
  }
}
```

### 4. Rodar a API

```bash
dotnet run --project src/Api/Api.csproj -c Debug
```

### 5. Testar com cURL

```bash
curl -X POST http://localhost:5000/api/ai/dsl/generate \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "goalText": "Extract id and name from person records",
    "sampleInput": [{"id": 1, "name": "Alice", "age": 30}],
    "dslProfile": "jsonata",
    "constraints": {"maxColumns": 50, "allowTransforms": true, "forbidNetworkCalls": true, "forbidCodeExecution": true}
  }'
```

---

## 🔧 Configuração Detalhada

### Provedores Disponíveis

| Provider | Endpoint | Modelo Padrão | Requer |
|----------|----------|---------------|--------|
| **HttpOpenAICompatible** | openrouter.ai/api/v1 | mistralai/devstral-2512 | `METRICS_OPENROUTER_API_KEY` |
| **Gemini** | generativelanguage.googleapis.com | gemini-2.5-flash | `METRICS_GEMINI_API_KEY` |
| **MockProvider** | N/A (Mock) | N/A | Nenhuma |

### Variáveis de Ambiente

```yaml
# OpenRouter (padrão)
METRICS_OPENROUTER_API_KEY=*
OPENROUTER_API_KEY=*  # fallback

# Gemini
METRICS_GEMINI_API_KEY=*
GEMINI_API_KEY=*  # fallback
```

### Configuração em appsettings.json

#### Exemplo: OpenRouter (Padrão)
```json
{
  "AI": {
    "Enabled": true,
    "Provider": "HttpOpenAICompatible",
    "EndpointUrl": "https://openrouter.ai/api/v1/chat/completions",
    "Model": "mistralai/devstral-2512:free",
    "PromptVersion": "2.0.0",
    "TimeoutSeconds": 60,
    "MaxRetries": 1,
    "Temperature": 0.0,
    "MaxTokens": 4096,
    "TopP": 0.9,
    "EnableStructuredOutputs": true,
    "EnableResponseHealing": true
  }
}
```

#### Exemplo: Gemini Flash (Rápido)
```json
{
  "AI": {
    "Enabled": true,
    "Provider": "Gemini",
    "EndpointUrl": "https://generativelanguage.googleapis.com/v1beta/models",
    "Model": "gemini-2.5-flash",
    "PromptVersion": "2.0.0",
    "TimeoutSeconds": 60,
    "MaxRetries": 1,
    "Temperature": 0.0,
    "MaxTokens": 4096,
    "TopP": 0.9
  }
}
```

#### Exemplo: Gemini Pro (Mais Poderoso)
```json
{
  "AI": {
    "Enabled": true,
    "Provider": "Gemini",
    "EndpointUrl": "https://generativelanguage.googleapis.com/v1beta/models",
    "Model": "gemini-1.5-pro",
    "PromptVersion": "2.0.0",
    "TimeoutSeconds": 90,
    "MaxRetries": 2,
    "Temperature": 0.1,
    "MaxTokens": 8192,
    "TopP": 0.9
  }
}
```

---

## 📊 Comparação de Modelos Gemini

| Modelo | Latência | Qualidade | Custo | Ideal Para |
|--------|----------|-----------|-------|-----------|
| **gemini-2.5-flash** | ~1-2s | 🟢 Excelente | Baixo | Produção, testes rápidos |
| **gemini-1.5-flash** | ~1-3s | 🟢 Excelente | Baixo | Alternativa rápida |
| **gemini-1.5-pro** | ~3-5s | 🟢🟢 Muito Bom | Médio | Casos complexos |

---

## 🧪 Testes com Gemini

### Rodar Todos os Testes

```bash
dotnet test Metrics.Simple.SpecDriven.sln
```

### Rodar Testes Específicos com Gemini

Se a API key estiver configurada, os testes da integração usarão automaticamente Gemini:

```bash
dotnet test tests/Integration.Tests/Integration.Tests.csproj --filter "PlanV1"
```

### Marcadores de Teste

- `[Trait("RequiresLLM", "true")]` - Testes que precisam de LLM ativo
- `[Trait("Engine", "plan_v1")]` - Testes do engine plan_v1
- `[Trait("Engine", "legacy")]` - Testes do engine legacy

---

## 🔐 Segurança

### ✅ O Que É Feito

- ✅ **Nenhuma API key em hardcode** - Sempre carregada de env vars
- ✅ **Logs não expõem chaves** - Só registra requestId, modelo, latência
- ✅ **Testes usam mocks** - Não expõem chaves reais em testes
- ✅ **Suporte a múltiplos provedores** - Permite trocar sem recompilar

### 🛑 O Que NUNCA Fazer

```csharp
// ❌ ERRADO - Nunca hardcodear
var apiKey = "*";

// ✅ CORRETO - Usar variável de ambiente
var apiKey = Environment.GetEnvironmentVariable("METRICS_GEMINI_API_KEY");
```

---

## 🐛 Troubleshooting

### "API key not configured"

```bash
# Verificar se variável existe
powershell: $env:METRICS_GEMINI_API_KEY
bash: echo $METRICS_GEMINI_API_KEY

# Configurar:
$env:METRICS_GEMINI_API_KEY = "seu-key"
```

### "Gemini API returned HTTP 400"

Possíveis causas:
1. **Modelo inválido** - Checar se modelo existe
   ```json
   "Model": "gemini-2.5-flash"  // ✅ Válido
   "Model": "gpt-4"              // ❌ Inválido para Gemini
   ```

2. **Formato de request errado** - GeminiProvider faz parse automático

### "Request timed out"

Aumentar `TimeoutSeconds` em `appsettings.json`:

```json
{
  "AI": {
    "TimeoutSeconds": 120
  }
}
```

### Latência Alta

Tentar:
1. Usar `gemini-2.5-flash` em vez de `gemini-1.5-pro`
2. Aumentar `MaxRetries` para lidar com rate limits
3. Verificar conexão de rede

---

## 📈 Métricas e Logs

### Logs de Sucesso

```
[INF] Gemini request: RequestId=a1b2c3d4, Model=gemini-2.5-flash, GoalLength=45, Attempt=1
[INF] Gemini success: RequestId=a1b2c3d4, Model=gemini-2.5-flash, DslProfile=jsonata
```

### Logs de Erro

```
[WRN] Gemini request timeout: RequestId=a1b2c3d4
[WRN] Gemini rate limited: RequestId=a1b2c3d4
[ERR] Gemini unexpected error: RequestId=a1b2c3d4
```

---

## 🔄 Arquitetura

### Fluxo de Requisição

```
Client Request
    ↓
AiEngineRouter (seleciona engine)
    ↓
[LegacyAiDslEngine] ou [PlanV1AiEngine]
    ↓
IAiProvider (HttpOpenAiCompatibleProvider | GeminiProvider | MockProvider)
    ↓
HTTP → LLM (OpenRouter | Gemini)
    ↓
Parse Response → Validate → Return DslGenerateResult
```

### Seleção de Provider

1. **MockProvider** - Se `Provider == "MockProvider"`
2. **GeminiProvider** - Se `Provider == "Gemini"` e `METRICS_GEMINI_API_KEY` configurada
3. **HttpOpenAiCompatibleProvider** - Default (OpenRouter, OpenAI, Azure, etc.)

---

## 📝 Exemplo Prático: Testar Gemini vs OpenRouter

### Setup Rápido

```bash
# Terminal 1: OpenRouter
$env:METRICS_OPENROUTER_API_KEY = "*"
Set-Content appsettings.json -Value '{
  "AI": {
    "Enabled": true,
    "Provider": "HttpOpenAICompatible",
    "Model": "mistralai/devstral-2512:free"
  }
}'
dotnet run --project src/Api/Api.csproj

# Terminal 2: Gemini (nova sessão)
$env:METRICS_GEMINI_API_KEY = "*"
Set-Content appsettings.json -Value '{
  "AI": {
    "Enabled": true,
    "Provider": "Gemini",
    "Model": "gemini-2.5-flash"
  }
}'
dotnet run --project src/Api/Api.csproj
```

### Comparar Respostas

```bash
# Request para ambos (ajustar porta conforme necessário)
curl -X POST http://localhost:5000/api/ai/dsl/generate \
  -H "Content-Type: application/json" \
  -d '{"goalText":"...","sampleInput":[...],...}'
```

---

## 🎯 Próximos Passos

1. **Testar com dados reais** - Use dados do seu projeto
2. **Medir latência** - Compare OpenRouter vs Gemini
3. **Ajustar prompts** - Fine-tune system/user prompts se necessário
4. **Adicionar metrics** - Rastrear uso de tokens, custo, latência
5. **Considerar caching** - Cachear respostas de goals similares

---

## 📚 Referências

- [Google Generative AI API Docs](https://ai.google.dev/api)
- [Gemini Models](https://ai.google.dev/models)
- [OpenRouter API](https://openrouter.ai/docs)
- [Spec: AI Provider Contract](../specs/backend/08-ai-assist/ai-provider-contract.md)

---

**Autor:** GitHub Copilot  
**Atualização:** 2026-01-06
