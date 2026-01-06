# 🎬 Exemplo Prático: Testando Gemini End-to-End

**Data:** 2026-01-06  
**Objetivo:** Demonstração completa de uso do Gemini LLM Provider

---

## 📋 Cenário

Transformar uma lista de vendas em um relatório agregado usando **Gemini 2.5 Flash** como LLM.

---

## 🎯 Step-by-Step

### STEP 1: Setup Inicial

#### 1.1 Obter API Key Gemini

1. Acesse: https://aistudio.google.com/app/apikeys
2. Clique **"Create API Key"** (botão azul)
3. Selecione seu projeto (ou deixe default)
4. Copie a chave (ex: `AIzaSyCeHxPI2nOYZgQ9O2b5xsytN8OywVpQmBw`)

#### 1.2 Configurar Variável de Ambiente

**Windows PowerShell:**
```powershell
$env:METRICS_GEMINI_API_KEY = "AIzaSyCeHxPI2nOYZgQ9O2b5xsytN8OywVpQmBw"
echo $env:METRICS_GEMINI_API_KEY  # Verificar
```

**Linux/Mac Bash:**
```bash
export METRICS_GEMINI_API_KEY="AIzaSyCeHxPI2nOYZgQ9O2b5xsytN8OywVpQmBw"
echo $METRICS_GEMINI_API_KEY  # Verificar
```

#### 1.3 Atualizar appsettings.json

**Arquivo:** `src/Api/appsettings.json`

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
    "Temperature": 0.2,
    "MaxTokens": 4096,
    "TopP": 0.9
  }
}
```

---

### STEP 2: Rodar a API

#### 2.1 Build

```bash
cd c:\Projetos\metrics-simple
dotnet build src/Api/Api.csproj -c Debug
```

**Esperado:**
```
✅ Api net10.0 êxito (1.5s)
```

#### 2.2 Run

```bash
dotnet run --project src/Api/Api.csproj -c Debug
```

**Esperado (últimas linhas):**
```
[INF] Now listening on: http://localhost:5000
[INF] Application started. Press Ctrl+C to exit.
```

---

### STEP 3: Autenticação

#### 3.1 Obter JWT Token

**Terminal 2** (nova janela):

```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "admin",
    "password": "ChangeMe123!"
  }'
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-01-07T00:00:00Z"
}
```

**Copiar o token:**
```bash
$TOKEN = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

### STEP 4: Teste 1 - Agregação Simples

#### 4.1 Request

**Objetivo:** Agregar vendas por categoria e calcular total de revenue

```bash
curl -X POST http://localhost:5000/api/ai/dsl/generate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "goalText": "Calculate total revenue (price * quantity) grouped by category. Include average price per category.",
    "sampleInput": [
      {"product": "Laptop", "category": "Electronics", "price": 999.99, "quantity": 2},
      {"product": "Mouse", "category": "Electronics", "price": 29.99, "quantity": 10},
      {"product": "Desk", "category": "Furniture", "price": 299.99, "quantity": 5},
      {"product": "Chair", "category": "Furniture", "price": 199.99, "quantity": 8}
    ],
    "dslProfile": "jsonata",
    "constraints": {
      "maxColumns": 50,
      "allowTransforms": true,
      "forbidNetworkCalls": true,
      "forbidCodeExecution": true
    }
  }' | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

#### 4.2 Response Esperada

```json
{
  "dsl": {
    "profile": "jsonata",
    "text": "$group(data, function($v) { $v.category }).{\"category\": $[0].category, \"total_revenue\": $sum($[].price * $[].quantity), \"avg_price\": $average($[].price)}"
  },
  "outputSchema": "{\"type\":\"array\",\"items\":{\"type\":\"object\",\"properties\":{...}}}",
  "exampleRows": [
    {"category": "Electronics", "total_revenue": 2299.80, "avg_price": 514.99},
    {"category": "Furniture", "total_revenue": 3399.90, "avg_price": 249.99}
  ],
  "rationale": "Grouped sales by category, calculated total revenue (price * quantity), and computed average price",
  "warnings": [],
  "engineUsed": "legacy",
  "modelInfo": {
    "provider": "Gemini",
    "model": "gemini-2.5-flash",
    "promptVersion": "2.0.0"
  }
}
```

#### 4.3 Interpretar Resultado

- ✅ **Gemini gerou um plano válido**
- ✅ **Jsonata expression compila**
- ✅ **Example rows foram calculadas corretamente**
- ✅ **Provider = Gemini (sucesso!)**

---

### STEP 5: Teste 2 - Extração com Transformação

#### 5.1 Request

**Objetivo:** Extrair nomes de clientes em UPPERCASE e adicionar timestamp

```bash
curl -X POST http://localhost:5000/api/ai/dsl/generate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "goalText": "Extract customer names in uppercase with their purchase dates. Add current timestamp to each record.",
    "sampleInput": [
      {"name": "alice silva", "purchaseDate": "2026-01-05", "amount": 150.00},
      {"name": "bob santos", "purchaseDate": "2026-01-04", "amount": 200.00},
      {"name": "carol oliveira", "purchaseDate": "2026-01-06", "amount": 300.00}
    ],
    "dslProfile": "jsonata",
    "constraints": {
      "maxColumns": 50,
      "allowTransforms": true,
      "forbidNetworkCalls": true,
      "forbidCodeExecution": true
    }
  }' | ConvertFrom-Json | ConvertTo-Json -Depth 5
```

#### 5.2 Response Esperada

```json
{
  "dsl": {
    "profile": "jsonata",
    "text": "$.{\"customer_name\": $uppercase(name), \"purchase_date\": purchaseDate, \"timestamp\": $now()}"
  },
  "exampleRows": [
    {"customer_name": "ALICE SILVA", "purchase_date": "2026-01-05", "timestamp": "2026-01-06T19:30:00Z"},
    {"customer_name": "BOB SANTOS", "purchase_date": "2026-01-04", "timestamp": "2026-01-06T19:30:00Z"},
    {"customer_name": "CAROL OLIVEIRA", "purchase_date": "2026-01-06", "timestamp": "2026-01-06T19:30:00Z"}
  ],
  "rationale": "Extracted names and converted to uppercase using $uppercase function, preserved purchase dates",
  "warnings": [],
  "engineUsed": "legacy",
  "modelInfo": {
    "provider": "Gemini",
    "model": "gemini-2.5-flash",
    "promptVersion": "2.0.0"
  }
}
```

---

### STEP 6: Teste 3 - com PlanV1 Engine

#### 6.1 Request

**Objetivo:** Usar engine=plan_v1 (determinístico com fallback para templates)

```bash
curl -X POST http://localhost:5000/api/ai/dsl/generate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "goalText": "Extract id, name, and email from person records",
    "sampleInput": [
      {"id": 1, "name": "Alice", "email": "alice@example.com", "phone": "123-456"},
      {"id": 2, "name": "Bob", "email": "bob@example.com", "phone": "789-012"}
    ],
    "dslProfile": "jsonata",
    "engine": "plan_v1",
    "includePlan": true,
    "constraints": {
      "maxColumns": 50,
      "allowTransforms": true,
      "forbidNetworkCalls": true,
      "forbidCodeExecution": true
    }
  }' | ConvertFrom-Json | ConvertTo-Json -Depth 10
```

#### 6.2 Response Esperada

```json
{
  "dsl": {
    "profile": "jsonata",
    "text": "$[{\"id\": id, \"name\": name, \"email\": email}]"
  },
  "outputSchema": "{...}",
  "exampleRows": [
    {"id": 1, "name": "Alice", "email": "alice@example.com"},
    {"id": 2, "name": "Bob", "email": "bob@example.com"}
  ],
  "rationale": "Selected 3 mentioned fields (id, name, email) from input records",
  "warnings": [],
  "engineUsed": "plan_v1",
  "plan": {
    "planVersion": "1.0",
    "source": {"recordPath": "$"},
    "steps": [
      {
        "op": "select",
        "fields": ["id", "name", "email"]
      }
    ]
  },
  "modelInfo": {
    "provider": "Gemini",
    "model": "gemini-2.5-flash",
    "promptVersion": "2.0.0"
  }
}
```

---

## 📊 Observar Comportamento

### Log Lines Importantes

No Terminal onde a API está rodando:

```
[INF] Gemini request: RequestId=a1b2c3d4, Model=gemini-2.5-flash, GoalLength=45, Attempt=1
[INF] Gemini success: RequestId=a1b2c3d4, Model=gemini-2.5-flash, DslProfile=jsonata
```

### Timing

```
Request Time (curl) ────┐
                        ↓
[00:00] → JWT Token obtained
[00:02] → Request sent to /api/ai/dsl/generate
[00:03] → Gemini API called (latency 1-2s)
[00:05] → Response returned ✅
```

---

## 🔄 Comparar com OpenRouter

### Mudar para OpenRouter

**1. Configurar API Key OpenRouter:**
```powershell
$env:METRICS_OPENROUTER_API_KEY = "sua-openrouter-key"
```

**2. Atualizar appsettings.json:**
```json
{
  "AI": {
    "Provider": "HttpOpenAICompatible",
    "EndpointUrl": "https://openrouter.ai/api/v1/chat/completions",
    "Model": "mistralai/devstral-2512:free"
  }
}
```

**3. Comparar responses:**
- Latência
- Taxa de sucesso
- Qualidade de planos gerados

---

## 🐛 Debugging

### Verificar Gemini API Key

```powershell
# Antes de rodar API
$env:METRICS_GEMINI_API_KEY  # Deve retornar sua key
```

### Ver Logs em Detalhes

**Editar `appsettings.json`:**
```json
{
  "Serilog": {
    "MinimumLevel": "Debug"  // Mudar de "Information" para "Debug"
  }
}
```

**Esperado:**
```
[DBG] GeminiProvider: Building Gemini request...
[DBG] GeminiProvider: Parsing response candidates[0].content.parts[0].text...
[INF] GeminiProvider: Gemini success: RequestId=...
```

### Problemas Comuns

#### "API key not configured"
```bash
# Solução:
$env:METRICS_GEMINI_API_KEY = "sua-chave"
Write-Host $env:METRICS_GEMINI_API_KEY  # Verificar
```

#### "Gemini API returned HTTP 400"
```
Causa: Provavelmente modelo inválido
Solução: Usar "gemini-2.5-flash" (não "gpt-4" ou outro)
```

#### "Request timeout"
```
Solução: Aumentar TimeoutSeconds em appsettings.json
"TimeoutSeconds": 120
```

---

## ✅ Checklist de Teste

- [ ] API key obtida de https://aistudio.google.com/app/apikeys
- [ ] Env var METRICS_GEMINI_API_KEY configurada
- [ ] appsettings.json com Provider="Gemini"
- [ ] API rodando sem erros (`dotnet run`)
- [ ] Login bem-sucedido, token obtido
- [ ] Teste 1 (agregação) respondeu com success
- [ ] Teste 2 (transformação) respondeu com success
- [ ] Teste 3 (plan_v1) respondeu com engineUsed="plan_v1"
- [ ] Logs mostram "Gemini success"
- [ ] Response contém modelInfo.provider="Gemini"

---

## 📈 Próximos Passos

1. **Testar com dados reais do seu projeto**
2. **Medir latência** - `gemini-2.5-flash` vs `gemini-1.5-pro`
3. **Testar rate limits** - Quantas requisições/minuto?
4. **Comparar custo** - Gemini vs OpenRouter
5. **Implementar retry logic** - Em caso de timeout
6. **Adicionar caching** - Para goals similares

---

## 📚 Referências Rápidas

| Ref | URL |
|-----|-----|
| Google AI Studio | https://aistudio.google.com/app/apikeys |
| Gemini API Docs | https://ai.google.dev/api |
| Disponível Models | https://ai.google.dev/models |
| Rate Limits | https://ai.google.dev/pricing |

---

**Escrito por:** GitHub Copilot  
**Data:** 2026-01-06
