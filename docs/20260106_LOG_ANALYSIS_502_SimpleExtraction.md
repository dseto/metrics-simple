# 📊 502 ERROR LOG ANALYSIS: SimpleExtraction Test

**Data**: 06 de Janeiro de 2026  
**Teste**: IT13_LLMAssistedDslFlowTests.LLM_SimpleExtraction_PortuguesePrompt  
**Status**: ❌ 502 Bad Gateway  
**Duração**: 15.9 segundos (2 LLM calls)

---

## 🔍 Fluxo de Execução Completo

### ✅ STEP 1: Login
```
[10:46:54 INF] Request starting HTTP/1.1 POST http://localhost/api/auth/token
[10:46:54 INF] Login successful. UserId=5ff0b5b62e4d40e78583a9fdc8d7fbdd, Username=admin
[10:46:54 INF] ApiRequestCompleted: 7e897ce5cb60 LocalJwt 200 292ms
```
✅ Token obtido com sucesso

---

### ❌ STEP 2: LLM DSL Generation (Tentativa 1)

#### Request
```
[10:46:54 INF] AI DSL Generate: CorrelationId=8608aa2316f8, Profile=jsonata, 
                GoalLength=78, InputHash=997EA4428B85A8CE
[10:46:54 INF] Skipping template-first (not yet enabled). Proceeding to LLM generation
[10:46:54 INF] DSL generation attempt 1/1: RequestId=2e6332ebace9, Model=mistralai/devstral-2512:free
```

**Goal Text**: "Quero extrair apenas o ID, nome e cidade de cada pessoa. Não preciso da idade."  
**DslProfile**: jsonata  
**Request ID**: 2e6332ebace9

#### OpenRouter HTTP Request
```
[10:46:54 INF] Sending HTTP request to AI provider (attempt 1): 
  Endpoint=https://openrouter.ai/api/v1/chat/completions
  Model=mistralai/devstral-2512:free
  StructuredOutputs=True
  RequestId=2e6332ebace9
[10:46:54 INF] Start processing HTTP request POST https://openrouter.ai/api/v1/chat/completions
```

#### OpenRouter HTTP Response
```
[10:47:04 INF] Received HTTP response headers after 9583.5234ms - 200
[10:47:04 INF] End processing HTTP request after 9588.0227ms - 200
```

**Status**: ✅ 200 OK  
**Latency**: 9.58 segundos  
**Response Time**: OK

#### Parse Response
```
[10:47:06 INF] LLM returned old contract (with outputSchema). Accepting for 
              backward compatibility, but backend will infer from preview
[10:47:06 INF] Successfully parsed DSL from LLM response (attempt 1, RequestId=2e6332ebace9): 
              DSL length=46, Profile=jsonata
```

**Parser Result**: ✅ Sucesso  
**DSL Length**: 46 caracteres  
**Contract Type**: Old (com outputSchema)

#### DSL Preview Failed
```
[10:47:06 INF] DSL preview failed, attempting repair: Array items must be objects
```

**Error Category**: ❌ Schema/Array validation  
**Error Message**: "Array items must be objects"  
**LLM DSL**: ~46 caracteres (provavelmente inválido)

---

### ❌ TENTATIVA 2: Repair Attempt

#### Repair Request
```
[10:47:06 INF] AI DSL Repair Attempt: CorrelationId=8608aa2316f8, Attempt=1
[10:47:06 INF] DSL generation attempt 1/1: RequestId=f715ef131840, Model=mistralai/devstral-2512:free
```

**Request ID da Repair**: f715ef131840

#### OpenRouter HTTP Request (Repair)
```
[10:47:06 INF] Sending HTTP request to AI provider (attempt 1): 
  Endpoint=https://openrouter.ai/api/v1/chat/completions
  Model=mistralai/devstral-2512:free
  StructuredOutputs=True
  RequestId=f715ef131840
[10:47:06 INF] Start processing HTTP request POST https://openrouter.ai/api/v1/chat/completions
```

#### OpenRouter HTTP Response (Repair)
```
[10:47:07 INF] Received HTTP response headers after 944.829ms - 200
[10:47:07 INF] End processing HTTP request after 945.346ms - 200
```

**Status**: ✅ 200 OK  
**Latency**: 945ms  
**Response Time**: Muito mais rápido (provavelmente cached)

#### Parse Response (Repair)
```
[10:47:09 INF] LLM returned old contract (with outputSchema). Accepting for 
              backward compatibility, but backend will infer from preview
[10:47:09 INF] Successfully parsed DSL from LLM response (attempt 1, RequestId=f715ef131840): 
              DSL length=48, Profile=jsonata
```

**Parser Result**: ✅ Sucesso  
**DSL Length**: 48 caracteres (ligeiramente mais longo, mudança mínima)

---

### ❌ STEP 3: Template Fallback (Último Recurso)

```
[10:47:09 INF] DSL failed after repair. Attempting template fallback...
[10:47:09 INF] Detected template: T1
[10:47:09 INF] Generated template DSL: data.{
  "id": id,
  "nome": nome,
  "idade": idade,
  "cidade": cidade                                                                                            
}
```

**Template Detectado**: T1 (Extract+Rename)  
**DSL Gerado**:
```jsonata
data.{
  "id": id,
  "nome": nome,
  "idade": idade,
  "cidade": cidade
}
```

#### Template Fallback Error
```
[10:47:09 WRN] Template fallback also failed: 'u' is an invalid start of a value. 
               LineNumber: 0 | BytePositionInLine: 0.
```

**Error**: JSON Parse Error  
**Message**: `'u' is an invalid start of a value`  
**Position**: Line 0, Byte 0

---

### ❌ FINAL RESPONSE

```
[10:47:09 WRN] AI-generated DSL preview failed after repair and template fallback
[10:47:09 INF] Setting HTTP status code 502.
[10:47:09 INF] Writing value of type 'AiError' as Json.
[10:47:09 INF] Executed endpoint 'HTTP: POST /api/v1/ai/dsl/generate => GenerateDsl'
[10:47:09 INF] ApiRequestCompleted: d9ab7f86e65a LocalJwt admin Metrics.Admin cdc981409d9748fa9c26280931fcd98c 
                POST /api/v1/ai/dsl/generate 502 15448ms
```

**HTTP Status**: 502 Bad Gateway  
**Total Latency**: 15.4 segundos  
**Error**: "AI-generated DSL preview failed after repair and template fallback"

---

## 🎯 Diagnóstico Detalhado

### PROBLEMA RAIZ #1: LLM DSL Inválido (Tentativa 1)

**O que aconteceu**:
1. LLM gerou DSL de 46 caracteres
2. Parser extraiu com sucesso (não foi problema de JSON)
3. Engine tentou executar e falhou: "Array items must be objects"

**Causa Provável**:
- LLM gerou algo como: `data[{id, nome, cidade}]` ou similar inválido
- Ou gerou: `data.filter(x => ...)` que retorna algo que não é array de objetos

**Output Esperado**: Array de objetos  
**Output Obtido**: Algo que não é array, ou array com items não-objetos

---

### PROBLEMA RAIZ #2: LLM NÃO APRENDEU com Repair (Tentativa 2)

**O que aconteceu**:
1. Repair tentou avisar LLM: "Array items must be objects"
2. LLM gerou DSL praticamente idêntico (48 vs 46 caracteres)
3. Erro provavelmente se repete

**Evidência**: `DslLength=48` vs `DslLength=46` = mudança MÍNIMA (2 chars)  
**Conclusão**: LLM NÃO entendeu feedback, apenas ajustou cosmético

---

### PROBLEMA RAIZ #3: Template Fallback JSON Parse Error

**O que aconteceu**:
```
Template DSL generated:
data.{
  "id": id,
  "nome": nome,
  "idade": idade,
  "cidade": cidade
}

Error: 'u' is an invalid start of a value. LineNumber: 0 | BytePositionInLine: 0.
```

**Causa**: O template DSL foi passado para validação, MAS há algo estranho no erro.

**Análise**:
- O DSL começa com `data.{`
- Erro diz: `'u' is an invalid start of a value` na posição 0
- Isso NÃO combina com "data"
- Provável: há caractere invisível ou encoding ruim

---

## 📈 Timeline

| Timestamp | Evento | Latência |
|-----------|--------|----------|
| 10:46:54.000 | Login request | - |
| 10:46:54.292 | Login response | 292ms |
| 10:46:54.463 | DSL generate request | - |
| 10:46:54.000 | LLM request 1 start | - |
| 10:47:04.000 | LLM response 1 | 9.588s |
| 10:47:06.000 | Preview failed, repair started | - |
| 10:47:06.000 | LLM request 2 start | - |
| 10:47:07.000 | LLM response 2 | 945ms |
| 10:47:09.000 | Template fallback tried | - |
| 10:47:09.000 | Final 502 response | 15.448s total |

---

## 🔧 Categoria de Erro

**Classificação**: `LlmResponseNotParseable` → Upgrade para `JsonataEvalFailed`

```csharp
ErrorCategory:
  - Layer 1: JSON Parse ✅ (sucesso)
  - Layer 2: Schema ✅ (old contract aceito)
  - Layer 3: Jsonata Eval ❌ (DSL invalid)
  - Layer 4: Template Fallback ❌ (Template DSL JSON error)

IsRetryable: FALSE (mudança mínima de 46→48 chars = não aprendeu)
```

---

## 💡 Insights

### ✅ O que Funcionou
1. **Login**: Sucesso
2. **OpenRouter HTTP**: HTTP 200 em ambas as chamadas
3. **LLM Response Parse**: JSON extraído corretamente (não era problema)
4. **Bad Pattern Detection**: Poderia ter detectado (se houvesse pattern)
5. **Repair Loop**: Tentou fazer repair (mas LLM não aprendeu)
6. **Template Fallback**: Foi acionado corretamente

### ❌ O que Falhou
1. **LLM DSL Quality**: Gerou algo inválido (Array items must be objects)
2. **Repair Learning**: LLM não aprendeu feedback (mudança cosmética)
3. **Template DSL JSON**: Erro misterioso com 'u' na posição 0

### ⚠️ Questões Abertas
1. Qual foi exatamente a DSL gerada pela LLM? (46 caracteres, não sabemos o conteúdo)
2. Por que o template DSL está dando JSON error com 'u'?
3. O encoding está correto ou há caracteres invisíveis?

---

## 📝 Recomendações

### 1. Adicionar Logging de DSL Content

```csharp
logger.LogInformation("LLM DSL content: {DslContent}", result.Dsl.Text);
// Mostrar o DSL completo, não só o length
```

### 2. Melhorar Error Message

```csharp
if (previewResult.Error == "Array items must be objects")
{
    logger.LogError("DSL returned non-object items. DSL={DslContent}, ErrorDetail={Detail}", 
        result.Dsl.Text, previewResult.ErrorDetails);
}
```

### 3. Detectar Padrões Inválidos ANTES de repair

```csharp
// Adicionar check anterior:
if (result.Dsl.Text.Contains("filter") || result.Dsl.Text.Contains("["))
{
    // Pode ser array filter (não array de objetos)
    logger.LogWarning("DSL may return wrong shape. Skipping repair, using template");
    // Jump to template directly
}
```

### 4. Template DSL Encoding Check

```csharp
// Debug o template DSL:
logger.LogInformation("Template DSL bytes: {Bytes}", 
    System.Text.Encoding.UTF8.GetBytes(templateDsl));
```

---

## 🎬 Próximas Ações

1. **Immediate**: Adicionar logs do conteúdo DSL (não só length)
2. **Short-term**: Implementar shape detection (JSON vs array vs scalar)
3. **Medium-term**: Melhorar repair prompt com exemplos claros
4. **Long-term**: Template-first (evita problema de LLM)

---

**Log Analysis**: Completo  
**Root Cause**: LLM gera DSL que retorna non-object items (provável `filter()` ou similar)  
**Severity**: 🔴 Critical - bloqueia todos os testes
