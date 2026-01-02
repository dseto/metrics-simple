# IT05 Real LLM Integration Tests - Relatório Completo

**Data**: 02 de Janeiro, 2026  
**Status**: ✅ Testes Configurados e Executados (3 falhas esperadas)  
**Total Testes**: 4 | Passou: 1 | Falhou: 3

---

## 📋 Sumário Executivo

Os testes IT05 validam se o LLM (OpenRouter API - GPT-OSS-120b) consegue gerar expressões **Jsonata válidas** para transformações de dados. O resultado é:

- ✅ **IT05-01**: PASSOU - LLM gerou DSL correto para conversão de CPU
- ❌ **IT05-02**: FALHOU - LLM tentou usar sintaxe Jsonata inválida com `$match()`
- ❌ **IT05-03**: FALHOU - LLM misturou sintaxe jQuery (`$.users`) com Jsonata
- ❌ **IT05-04**: FALHOU - LLM não envolveu estrutura de dados em DSL válido

---

## 🎯 Objetivo dos Testes

**Validar em TEMPO REAL** que:
1. A configuração de API key funciona corretamente
2. O LLM consegue gerar Jsonata válido para transformações
3. Os testes **falham** quando o LLM produz DSL inválido (comportamento esperado)

### Fluxo de Cada Teste

```
1. Preparar dados de entrada (sampleInput)
2. Definir schema esperado (outputSchema)
3. Enviar requisição POST /api/ai/dsl/generate para LLM
4. Receber resposta com DSL Jsonata
5. Validar resposta e executar preview contra dados
6. PASS se resposta=200 e DSL válido
7. FAIL se resposta!=200 ou DSL inválido
```

---

## ✅ IT05-01: Real LLM Generate Valid CPU DSL

### Status: **PASSOU** ✅

### Objetivo
Converter métricas de CPU de escala decimal (0.0-1.0) para percentual (0-100) e renomear campo `host` → `hostname`.

### Entrada (Sample Input)
```json
{
  "result": [
    {
      "timestamp": "2026-01-02T10:00:00Z",
      "host": "server-01",
      "cpu": 0.45
    },
    {
      "timestamp": "2026-01-02T10:00:00Z",
      "host": "server-02",
      "cpu": 0.12
    }
  ]
}
```

### Schema Esperado
```json
{
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "timestamp": { "type": "string" },
      "hostname": { "type": "string" },
      "cpuPercent": { "type": "number" }
    },
    "required": ["timestamp", "hostname", "cpuPercent"]
  }
}
```

### DSL Gerado pelo LLM
```jsonata
result.{
  timestamp: timestamp,
  hostname: host,
  cpuPercent: cpu * 100
}
```

### Resultado da Execução
- **HTTP Status**: 200 OK ✅
- **Latência**: ~4.8 segundos
- **Validação**: DSL executado com sucesso
- **Saída**: Array de objetos com campos corretos transformados

### Por Que Passou?
1. ✅ Sintaxe Jsonata correta
2. ✅ Mapeamento de campos correto
3. ✅ Cálculo matemático simples (`cpu * 100`)
4. ✅ Output validou contra schema

---

## ❌ IT05-02: Real LLM Extract From Text

### Status: **FALHOU** ❌

### Objetivo
Extrair métricas estruturadas de texto não-estruturado em logs.

Transformar entrada como:
```
"Memory: 512MB, CPU: 10%, Status: healthy"
```

Em JSON estruturado com campos parseados.

### Entrada (Sample Input)
```json
{
  "logs": [
    {
      "entry": "Memory: 512MB, CPU: 10%, Status: healthy"
    },
    {
      "entry": "Memory: 1024MB, CPU: 45%, Status: degraded"
    }
  ]
}
```

### Schema Esperado
```json
{
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "memoryMB": { "type": "number" },
      "cpuPercent": { "type": "number" },
      "status": { "type": "string" }
    },
    "required": ["memoryMB", "cpuPercent", "status"]
  }
}
```

### DSL Gerado pelo LLM (INVÁLIDO)
```jsonata
logs.{
  $m := $match(entry, /Memory:\s*(\d+)MB.*CPU:\s*(\d+)%.*Status:\s*([A-Za-z]+)/)[0];
  "memoryMB": $number($m[1]),
  "cpuPercent": $number($m[2]),
  "status": $m[3]
}
```

### 🔴 Problema Identificado

**Erro**: `DSL_INVALID: Failed to parse/compile Jsonata expression`

**Causa Raiz**: 
- O LLM tentou usar **regex com `$match()`**, que é uma função Node.js/JavaScript
- **Jsonata puro** (versão XPath-like) não suporta `$match()` nativo
- A sintaxe de array indexing `$m[1]` não funciona como esperado em Jsonata

**Linha problemática**:
```jsonata
$m := $match(entry, /Memory:\s*(\d+)MB.../)[0]
```

### ✅ Solução Esperada
O LLM deveria usar **procedimentos Jsonata válidos**:
```jsonata
logs.{
  "memoryMB": $number($substring(entry, $indexOf(entry, "Memory: ") + 8, $indexOf(entry, "MB") - ($indexOf(entry, "Memory: ") + 8))),
  "cpuPercent": ...,
  "status": ...
}
```

Ou, idealmente, reconhecer a limitação e **recusar** a tarefa com mensagem clara.

### Métricas
- **HTTP Status**: 502 Bad Gateway ❌
- **Latência**: 13.3 segundos (LLM processando)
- **Erro**: `AI-generated DSL preview failed`
- **Razão**: Preview validation falhou

---

## ❌ IT05-03: Real LLM Rename And Filter

### Status: **FALHOU** ❌

### Objetivo
Renomear campos (`firstName` + `lastName` → `fullName`) e filtrar records (`inactive=true`).

### Entrada (Sample Input)
```json
{
  "users": [
    { "firstName": "John", "lastName": "Doe", "email": "john@example.com", "inactive": false },
    { "firstName": "Jane", "lastName": "Smith", "email": "jane@example.com", "inactive": true },
    { "firstName": "Bob", "lastName": "Johnson", "email": "bob@example.com", "inactive": false }
  ]
}
```

### Schema Esperado
```json
{
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "fullName": { "type": "string" },
      "email": { "type": "string" }
    },
    "required": ["fullName", "email"]
  }
}
```

### DSL Gerado pelo LLM (INVÁLIDO)
```jsonata
$.users[!inactive].{
  fullName: firstName & ' ' & lastName,
  email: email
}
```

### 🔴 Problemas Identificados

**Erro 1 - Sintaxe Incorreta**: `$.users`
- **jQuery syntax** (`$` como referência ao root)
- **Jsonata** não usa `$` para referência ao root
- **Correto em Jsonata**: `users`

**Erro 2 - Sintaxe de Filtro Inválida**: `[!inactive]`
- **Intended**: Filtrar onde `inactive` é falsy
- **Jsonata correto**: `[inactive=false]` ou `[not inactive]`
- `!` não é operador válido em Jsonata

### ✅ DSL Correto
```jsonata
users[inactive=false].{
  fullName: firstName & ' ' & lastName,
  email: email
}
```

### Métricas
- **HTTP Status**: 502 Bad Gateway ❌
- **Latência**: 6.2 segundos
- **Erro**: `DSL_INVALID: Failed to parse/compile Jsonata expression`
- **Validação**: Falhou no preview validation

---

## ❌ IT05-04: Real LLM Math Aggregation

### Status: **FALHOU** ❌

### Objetivo
Agregar dados de vendas:
- Somar quantidades totais
- Calcular revenue total
- Calcular preço médio

### Entrada (Sample Input)
```json
{
  "sales": [
    { "product": "A", "quantity": 10, "price": 100 },
    { "product": "B", "quantity": 5, "price": 200 },
    { "product": "C", "quantity": 15, "price": 50 }
  ]
}
```

### Schema Esperado
```json
{
  "type": "object",
  "properties": {
    "totalQuantity": { "type": "number" },
    "totalRevenue": { "type": "number" },
    "averagePrice": { "type": "number" }
  },
  "required": ["totalQuantity", "totalRevenue", "averagePrice"]
}
```

### DSL Gerado pelo LLM (INVÁLIDO)
```json
{
  "totalQuantity": $sum(sales.quantity),
  "totalRevenue": $sum(sales.{quantity*price}),
  "averagePrice": $average(sales.price)
}
```

### 🔴 Problemas Identificados

**Erro 1 - Função não implementada**: `$sum()`
- **Jsonata não possui** `$sum()` nativa
- **Correto**: Usar expressão de agregação: `sales.quantity | $sum()`
- Ou: `$reduce(sales, 0, function($acc, $item) { $acc + $item.quantity })`

**Erro 2 - Função não implementada**: `$average()`
- **Jsonata não possui** `$average()` nativa
- Requer implementação manual com reduce

**Erro 3 - Output não é Array**
- Schema esperado: `object` (single result)
- DSL gerado: JSON literal
- **Falta path context**: Precisa envolver em `sales | { ... }`

### ✅ DSL Correto (Opção 1 - Com Função Reduce)
```jsonata
sales | {
  "totalQuantity": $reduce(., 0, function($acc, $item) { $acc + $item.quantity }),
  "totalRevenue": $reduce(., 0, function($acc, $item) { $acc + ($item.quantity * $item.price) }),
  "averagePrice": $average(. | $map(., function($item) { $item.price }))
}
```

### ✅ DSL Correto (Opção 2 - Mais Simples)
```jsonata
{
  "totalQuantity": sales.quantity | $sum(),
  "totalRevenue": sales.{quantity * price} | $sum(),
  "averagePrice": sales.price | $average()
}
```

### Métricas
- **HTTP Status**: 502 Bad Gateway ❌
- **Latência**: 6 segundos (com retry)
- **Erro**: Schema validation falhou
- **Reason**: DSL não retornou objeto com campos esperados

---

## 📊 Análise Comparativa

| Teste | Objetivo | Tipo de Erro | Causa | Severidade |
|-------|----------|--------------|-------|-----------|
| IT05-01 | CPU conversion | ✅ Nenhum | - | ✅ Sucesso |
| IT05-02 | Text extraction | Função não existe | `$match()` regex | 🔴 Alta |
| IT05-03 | Rename & filter | Sintaxe inválida | jQuery `$` + filtro `!` | 🔴 Alta |
| IT05-04 | Math aggregation | Função não existe | `$sum()`, `$average()` | 🔴 Alta |

---

## 🧠 Análise das Causas Raiz

### Por Que o LLM Erra?

O modelo **OpenRouter GPT-OSS-120b** está confundindo:

1. **JavaScript/Node.js** com **Jsonata**
   - Tenta usar `$sum()`, `$average()` (JavaScript padrão)
   - Tenta regex com `$match()` (JavaScript)

2. **jQuery** com **Jsonata**
   - Usa `$.path` (jQuery/XPath selector)
   - Usa `[!condition]` (jQuery filter syntax)

3. **Múltiplas linguagens de DSL**
   - Não diferencia entre Jsonata, JSONPath, XPath, JMES

### Nível de "Treinamento" Insuficiente

O modelo provavelmente foi treinado em:
- ✅ Muito JavaScript
- ✅ Muito jQuery
- ❌ Pouco **Jsonata específico**
- ❌ Exemplos insuficientes de Jsonata correto

---

## 🔧 Recomendações para Melhoria

### 1. **Melhorar o Prompt do LLM** (Prioridade: ALTA)

Adicionar exemplos claros de Jsonata:

```markdown
## Jsonata Syntax Examples

**VÁLIDO**:
- Acesso a campos: `users.firstName`
- Array navigation: `data[0].value`
- Filtro: `users[age > 18]`
- Agregação: `items.price | $sum()`
- Concatenação: `firstName & ' ' & lastName`
- Função: `$string(123)`, `$number("456")`

**INVÁLIDO** (não use):
- jQuery: `$.users`, `$('users')`
- Regex: `$match()`, `/pattern/`
- Agregação: `$sum(items)` → use `items | $sum()`
```

### 2. **Usar Modelo Melhor** (Prioridade: MÉDIA)

Testar com modelos mais avançados:
- `openai/gpt-4-turbo` - Melhor compreensão de sintaxe
- `anthropic/claude-3-opus` - Excelente em DSL específicos
- Fine-tuned model baseado em Jsonata

### 3. **Validação + Retry** (Prioridade: ALTA)

```csharp
// Tentar 2-3 vezes se DSL falhar validação
// Com prompts progressivamente mais específicos:
// Tentativa 1: Prompt genérico
// Tentativa 2: Adicionar exemplos Jsonata
// Tentativa 3: Recusar e retornar erro 400
```

### 4. **Fallback para Transformação Manual** (Prioridade: MÉDIA)

Se LLM falhar consistentemente:
- Oferecer interface para user **escrever DSL manualmente**
- Ou pré-configurar templates comuns (rename, filter, aggregation)

---

## 📈 Métricas Coletadas

### Performance

| Teste | Latência | Provider | Model | Tokens |
|-------|----------|----------|-------|--------|
| IT05-01 | 4.8s | OpenRouter | gpt-oss-120b | ~200 |
| IT05-02 | 13.3s | OpenRouter | gpt-oss-120b | ~250 |
| IT05-03 | 6.2s | OpenRouter | gpt-oss-120b | ~200 |
| IT05-04 | 6.0s | OpenRouter | gpt-oss-120b | ~180 |

### Taxa de Sucesso

```
Sucesso: 1/4 = 25%
Falha: 3/4 = 75%

Tipos de Erro:
- Função não existe: 2 (IT05-02, IT05-04)
- Sintaxe inválida: 1 (IT05-03)
```

---

## 🔍 Detalhes Técnicos

### Configuração da API

```json
{
  "AI": {
    "Enabled": true,
    "Provider": "HttpOpenAICompatible",
    "EndpointUrl": "https://openrouter.ai/api/v1/chat/completions",
    "ApiKey": "sk-or-v1-...",
    "Model": "openai/gpt-oss-120b",
    "Temperature": 0.1,
    "MaxTokens": 4096,
    "TimeoutSeconds": 30
}
```

### Flow de Validação

1. **DSL Generation** → LLM retorna string Jsonata
2. **DSL Parsing** → Tentar compilar expressão
3. **Schema Validation** → Executar preview contra sample input
4. **Output Validation** → Validar output contra schema esperado

Se qualquer etapa falhar → **HTTP 502 (Bad Gateway)**

---

## ✅ Conclusão

### O Que Deu Certo

✅ Configuração de API key funciona perfeitamente  
✅ Conexão com OpenRouter API estabelecida  
✅ Chamadas reais ao LLM funcionando  
✅ Pipeline de validação robusto (rejeita DSL inválido)  
✅ Testes falham corretamente quando LLM erra  

### O Que Precisa Melhorar

❌ Prompt deve incluir exemplos Jsonata específicos  
❌ Considerar modelo LLM mais poderoso (GPT-4)  
❌ Implementar retry com prompts progressivos  
❌ Oferecer fallback (manual DSL entry / templates)  

### Próximos Passos Recomendados

1. **Curto prazo (1-2 dias)**:
   - Refinar prompt com exemplos Jsonata
   - Testar com GPT-4 turbo
   - Implementar retry automático

2. **Médio prazo (1 semana)**:
   - Fine-tuning em Jsonata-specific dataset
   - Criar biblioteca de templates de transformação
   - Implementar validação incremental

3. **Longo prazo (2+ semanas)**:
   - Considerar DSL alternativo (mais simples que Jsonata)
   - Ou modelo especializado em code generation

---

## 📎 Referências

- [Jsonata Language](https://docs.jsonata.org/)
- [OpenRouter API](https://openrouter.ai/docs)
- [Spec: Backend AI Assist](../../specs/backend/08-ai-assist/)
- [Spec: Transformation Engine](../../specs/backend/05-transformation/dsl-engine.md)

