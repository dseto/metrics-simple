# 📋 Relatório de Resultados: Implementação de Confiabilidade DSL

**Data**: 06 de Janeiro de 2026  
**Período**: 05/01 - 06/01 (Implementação Completa)  
**Status Geral**: 🟡 PARCIALMENTE CONCLUÍDO  
**Definition of Done**: ❌ NÃO ATINGIDO (0/4 em IT13, necessário ≥3/4)

---

## 📊 Resultado Final

### Testes Implementados

| Teste | Objetivo | Status | Qtd Testes | Taxa |
|-------|----------|--------|-----------|------|
| **IT10** | Transform com HGBrasil (Real API) | ✅ PASSA | 6/6 | 100% |
| **IT11** | Transformações Complexas AlphaVantage | ✅ PASSA | 10/10 | 100% |
| **IT12** | Full CRUD Flow (E2E) | ✅ PASSA | 2/2 | 100% |
| **IT13** | LLM-Assisted DSL Generation | ❌ FALHA | 0/4 | 0% |
| **TOTAL** | | | **18/22** | **81.8%** |

### Detalhamento IT13 (Alvo Principal)

| Teste | Descrição | Resultado | Erro | Tipo |
|-------|-----------|-----------|------|------|
| IT13_SimpleExtraction | Extract + Rename em PT-BR | ❌ 502 | DSL generation falhou | Backend Error |
| IT13_ComplexTransformation | Mixed PT-EN (era ✅) | ❌ Invalid | Transform validation failed | Validation Error |
| IT13_Aggregation | Group + Sum em EN | ❌ 502 | LLM gera `$group` inválido | LLM Error |
| IT13_WeatherForecast | Weather com sorting | ❌ 502 | Syntax inválida em output | LLM Error |

---

## 🔍 Análise de Falhas por Commit

### ❌ Commit 1: Parse Resiliente & Error Classification

**Implementado**: ✅ COMPLETO
- `LlmResponseParser.cs` com 3 estratégias de parse
- `DslErrorClassifier.cs` com classificação de erros
- Logging detalhado com RequestId
- Repeat detection

**Resultado em IT13**: ❌ SEM MELHORIA (1/4 → 1/4)

**Causa da Falha**:
```
┌─ Erro Original
│  └─ LLM retorna: sales.{$group: category, $sum: ...}
│     └─ Problema: $group não existe em Jsonata (deveria ser group-by)
│
├─ Por que Commit 1 não ajudou
│  └─ Commit 1 resolve: malformed JSON, markdown blocks, caracteres estranhos
│  └─ NÃO resolve: sintaxe Jsonata INVÁLIDA ($$group é válido JSON!)
│
└─ Conclusão
   └─ LlmResponseParser extrai JSON corretamente
   └─ Mas o conteúdo é sintaticamente inválido → compile failure
   └─ ErrorClassifier categoriza como "JsonataSyntaxInvalid"
   └─ Retry é executado, mas LLM retorna MESMA DSL inválida
```

**Logs Capturados**:
```
[09:48:27 INF] Successfully parsed DSL from LLM response (attempt 1)
[09:48:27 INF] DSL preview failed, attempting repair: 
   Failed to parse/compile Jsonata expression. $group is not defined
[09:48:28 INF] Successfully parsed DSL from LLM response (attempt 2)
[09:48:30 WRN] DSL preview failed after repair: $group is not defined (again!)
[09:48:30 INF] Setting HTTP status code 502
```

**Root Cause**: 
- ✅ Parser funciona (JSON extraído corretamente)
- ❌ DSL inválido não é problema de parse, é problema de **conhecimento do LLM**
- LLM não entende Jsonata dialect apesar de 1000+ linhas de prompt

---

### ❌ Commit 2: Server-Side OutputSchema Inference

**Implementado**: ✅ COMPLETO
- `OutputSchemaInferer.cs` com inferência determinística
- Sistema prompt atualizado (sem pedir outputSchema)
- ParseChatCompletionResponse backward-compatible
- GenerateDsl com schema inference

**Resultado em IT13**: ❌ REGRESSÃO (1/4 → 0/4) 😞

**Causa da Regressão**:

```
Fluxo Antigo:
  1. LLM response: {dsl, outputSchema, rationale, warnings}
  2. ParseChatCompletionResponse extrai tudo
  3. engine.TransformValidateToCsv(validSchema) passa por validação
  4. Um teste passava (ComplexTransformation)
  
Fluxo Novo (Commit 2):
  1. Nova prompt: LLM NÃO deve retornar outputSchema
  2. MAS LLM continua retornando old contract (outputSchema)
  3. ParseChatCompletionResponse recebe outputSchema
  4. Tenta fazer backward-compat
  5. Passa {} vazio para engine.TransformValidateToCsv()
  6. TransformValidateToCsv FALHA na validação com schema vazio
  7. Repair loop não recupera (LLM repete erro)
```

**Erro Específico**:
```
engine.TransformValidateToCsv(
  input, 
  dslProfile, 
  dslText,
  {} // ← Schema vazio = validação falha!
)

SchemaValidator.ValidateAgainstSchema(rows, {})
  └─ Erro: Cannot validate against empty schema
```

**Por que o Teste Anterior Passava**:
- Antes: LLM retornava outputSchema válido
- engine recebia schema válido (mesmo que gerado pela LLM)
- TransformValidateToCsv validava contra ele
- 1 em 4 vezes funcionava

**Por que Regression Ocorreu**:
- Commit 2 mudou sistema prompt
- Mas LLM ainda segue OLD contract (retorna outputSchema)
- Code tenta usar schema inferido (vazio) ANTES da preview
- Preview falha imediatamente
- Repair loop não consegue recuperar

---

### ❌ Commit 3: Template Fallback

**Implementado**: ✅ COMPLETO
- `DslTemplateLibrary.cs` com T1, T5, T7
- Template detection por keywords
- Parameter extraction heuristics
- Fallback integration em GenerateDsl

**Resultado em IT13**: ❌ NÃO ATINGIU (0/4)

**Motivos de Não-Ativação**:

```
Problema 1: Template Fallback Nunca Executado
────────────────────────────────────────────
Código: Apenas ativado APÓS maxRepairAttempts
└─ maxRepairAttempts = 1 (apenas 1 retry)
└─ Se LLM falha na tentativa 2, fallback ativado

MAS: Em 3/4 testes, erro acontece NA RESPOSTA
└─ LLM response parse falha OU contrato viola
└─ Erro acontece ANTES de repair loop
└─ Fallback code nunca é alcançado

Problema 2: Heurísticas de Template Inadequadas
──────────────────────────────────────────────
Template Detection:
  - Agregação: contém "sum" ou "total" ou "group"
  - Filter: contém "filter" ou "where" ou "status"
  - Extract (default): everything else

Teste IT13_Aggregation:
  Goal: "Calculate the total revenue (price * quantity) 
         for each category. Group by category and sum..."
  
  ✓ Contém "sum" e "group"
  ✓ DetectTemplate() retorna "T5"
  ✓ ExtractTemplate5Parameters() extrai fields
  ✓ Template5_GroupAggregate() gera DSL
  
  MAS NUNCA CHEGA AQUI porque:
  └─ LLM response parsing falha
  └─ 502 retorna antes de tentativa 2

Problema 3: Parameter Extraction Incompleto
─────────────────────────────────────────────
ExtractTemplate5Parameters tenta:
  1. Encontrar field para group-by
  2. Encontrar fields numéricos para agregação
  
Heurística atual: "primeiro string field" = group-by
└─ Nem sempre correto (ordem de campos varia)
└─ Sem informação semântica, adivinhação falha

Heurística atual: "primeiro numeric field" = agregar
└─ Muitos tests têm múltiplos numerics
└─ Qual agregar? Primeiro? Todos?
└─ Template precisa mais inteligência
```

**Impacto do Fluxo**:

```
Request → LLM → Parse/Validate → Repair → Template Fallback → Response

IT13_SimpleExtraction:
  ✓ Request OK
  ✓ LLM responde com outputSchema
  ✗ ParseChatCompletionResponse falha em backward-compat
  ✗ Repair loop nunca iniciado (erro antes)
  ✗ Template fallback nunca alcançado
  → 502 Bad Gateway

IT13_ComplexTransformation:
  ✓ Request OK
  ✓ LLM responde com válido JSON
  ✓ ParseChatCompletionResponse OK
  ✓ Repair loop iniciado
  ✗ LLM retorna MESMA DSL inválida
  ✗ Repeat detection para retry
  ✗ Template fallback tenta (T1)
  ✗ Template DSL também falha (parâmetros ruins)
  → 502 Bad Gateway
```

---

## 🎯 Diagnóstico Final

### Problema Raiz #1: LLM Knowledge Gap

**Sintoma**: LLM gera Jsonata inválida repetidamente
```jsonata
# LLM gera (inválido):
sales.{$group: category, $sum: $sum(price * quantity)}

# Deveria ser (válido):
sales.({category, totalRevenue: $sum(price * quantity)})
  ~> $group('category')
```

**Por que ocorre**:
- LLM treinou em múltiplos dialetos JS (jQuery, JSONPath, Jsonata)
- `$group` existe em JSON processing libraries, NÃO em Jsonata
- System prompt lista 1000+ regras, mas LLM não absorve
- Prompt não é efetivo contra treinamento pré-existente

**Por que retry não ajuda**:
- LLM não aprende de error messages em contexto
- Mesmo prompt + same-error-message = mesma resposta
- LLM não tem mecanismo de "aprendi, vou evitar"

---

### Problema Raiz #2: Fluxo de Fallback Inadequado

**Sintoma**: Commit 2 quebrou flow antes de fallback ativar

**Sequência**:
1. Commit 1 adicionou parsing robusto ✅
2. Commit 2 mudou system prompt (não pedir outputSchema) ✅
3. MAS: LLM ainda retorna OLD contract (com outputSchema) ⚠️
4. Code tenta ser backward-compatible
5. Mas passa schema VAZIO para engine ❌
6. Engine validation falha imediatamente
7. Fallback seria ativado, mas... LLM erro antes ❌

---

### Problema Raiz #3: Template Heuristics Fracas

**Sintoma**: Templates criados mas nunca usados efetivamente

```
Template Library Status:
  T1 (Extract+Rename):    Genérica, funciona
  T5 (Group+Aggregate):   Genérica, parâmetros ruins
  T7 (Filter+Map):        Genérica, parâmetros ruins
  
Problema:
  └─ Heurísticas muito simples
  └─ Sem análise semântica de goal
  └─ Sem validação de parâmetros
  └─ Template match sucede, mas instanciação falha
```

---

## 💡 Por Que Commits 1 & 2 Parecem Não Ajudar

### Commit 1: Fundamentalmente Correto, Alvo Errado

**O que Commit 1 resolve**:
- ✅ JSON malformado → parsed corretamente
- ✅ Markdown blocks → removidos
- ✅ Error categorization → smart retry decisions
- ✅ Repeat detection → não fica em loop infinito

**Por que não ajudou IT13**:
- ❌ Problema NÃO era parsing (JSON está OK)
- ❌ Problema era SINTAXE Jsonata (DSL inválida)
- ❌ LLM gera sintaxe inválida = parsing OK, compile FALHA

**Analogia**:
```
Commit 1 é como ter um "spell-checker" para capturar erros de ortografia
MAS o problema é gramatical: "chair are sitting" (sintaxe errada)
Spell-checker não detecta gramatical errors
```

---

### Commit 2: Mudança de Fluxo Quebrou Invariant

**O que Commit 2 tentava**:
- ✅ Remove outputSchema da responsabilidade LLM
- ✅ Infere schema do output real (determinístico)
- ✅ Nunca mais falha por schema inválido

**Por que PIOROU**:
- LLM ainda retorna outputSchema (não leu novo prompt)
- Code tenta ser backward-compatible
- Passa schema vazio para engine.TransformValidateToCsv()
- Engine falha: "Cannot validate with empty schema"
- Teste que passava antes agora falha

**O Erro Específico**:
```csharp
// Antes (Commit 1):
engine.TransformValidateToCsv(input, profile, dsl, outputSchemaValid)
// Validation: OK, DSL compile: FAIL → Retry

// Depois (Commit 2):
engine.TransformValidateToCsv(input, profile, dsl, {})
// Validation: FAIL (empty schema) → não chega em DSL compile!
```

---

## 🔧 O Que Deveria Ter Sido Feito

### Fix #1: Não Mudar System Prompt Enquanto LLM Não Responde

**Problema Atual**:
- Mudamos prompt para não pedir outputSchema
- MAS LLM v1 ainda retorna old contract
- Código precisa transição suave

**Solução Correta**:
```csharp
// Aceitar AMBOS contracts indefinidamente
if (response.HasProperty("outputSchema")) {
    // Old contract: use e log
    schema = response.outputSchema
    logger.LogInformation("LLM returned old contract (with outputSchema)")
} else {
    // New contract: infer
    schema = InferFromPreview()
    logger.LogInformation("LLM followed new contract (no outputSchema)")
}

// ✅ Nunca passar schema vazio!
```

### Fix #2: Detectar Template ANTES de LLM

**Problema Atual**:
- LLM gera DSL
- Se falha: tenta template

**Solução Melhor**:
```
1. Analisar goal antes de LLM
2. Se confiança alta (ex: "sum by category") → usar template direto
3. Se confiança média → LLM + fallback para template
4. Se confiança baixa → template + refinement por LLM

Benefits:
- 80% de casos resolvidos sem LLM (rápido!)
- 20% complexos: LLM = refinement, não geração
- LLM task simplificado = mais confiável
```

### Fix #3: Parameter Extraction Inteligente

**Problema Atual**:
```csharp
// Heurística: "primeiro string" = group field
var groupField = fields.FirstOrDefault(f => isString(f))
// Resultado: ❌ Ordem aleatória, sem semântica
```

**Solução**:
```csharp
// Usar goal text para identificar group field
var groupKeywords = ["category", "type", "status", "group", "by"]
var groupField = fields.FirstOrDefault(f => 
    goal.Contains(f) || groupKeywords.Any(kw => goal.Contains(kw)))
    ?? fields.FirstOrDefault(f => isString(f))

// Resultado: ✅ Match semântico + fallback heurístico
```

---

## 📈 Comparação: Antes vs Depois

### Antes (Sem Commits)

| Aspecto | Status |
|---------|--------|
| Parse Robustez | ❌ Malformed JSON mata | 
| Error Classification | ❌ Todos = retry |
| Retry Logic | ❌ Infinito ou 1x |
| Schema Inference | ❌ LLM, pode ser inválido |
| Fallback | ❌ Nenhum |
| IT13 | 1/4 (acaso) |

### Depois (Com Commits 1, 2, 3)

| Aspecto | Status |
|---------|--------|
| Parse Robustez | ✅ 3-strategy fallback |
| Error Classification | ✅ 5 categorias |
| Retry Logic | ✅ Smart com repeat detection |
| Schema Inference | ✅ Determinístico (server) |
| Fallback | ✅ 3 templates |
| IT13 | 0/4 (piora em curto prazo) |

**Análise**: 
- ✅ Infraestrutura MUITO melhor (commits 1 & 2 são sólidos)
- ❌ Mas não resolvem LLM core problem (gera sintaxe inválida)
- ⚠️ Commit 2 criou regressão (flow change sem validação)

---

## 🚨 Recomendações Imediatas

### 1️⃣ Revert Commit 2 Parcialmente

```csharp
// Manter OutputSchemaInferer (é bom!)
// MAS: Não quebrar flow existente

// FIX:
if (previewResult.IsValid && previewResult.OutputJson.HasValue) {
    // Só DEPOIS de validar preview, infer schema
    var schema = OutputSchemaInferer.InferSchema(...)
    result = result with { OutputSchema = schema }
}

// NÃO fazer:
// engine.TransformValidateToCsv(input, profile, dsl, {}) ❌
```

### 2️⃣ Ativar Template Fallback Mais Cedo

```csharp
// Current: Só ativa após 2 tentativas
// Better: Ativa quando LLM response é válido MAS preview falha

if (previewResult.IsValid == false && 
    dslErrorCategory == JsonataSyntaxInvalid) {
    logger.LogInformation("DSL syntax invalid, using template fallback")
    // Tentar template aqui
    // Não aguardar repair loop
}
```

### 3️⃣ Implementar Template + LLM Sequencial

Próximo commit:
```
1. Detectar transformation type (Extract, Aggregate, Filter)
2. Se confiança > 80%: usar template direto
3. Se 50-80%: template + LLM refinement
4. Se < 50%: pedir user para clarificar

Resultado esperado: IT13 ≥ 3/4
```

---

## 📊 Lições Aprendidas

### ❌ O Que Não Funcionou

1. **LLM não aprende de error messages** em contexto
   - Repeat detection para, mas não auto-corriges
   - Precisa mudança arquitetural (menos LLM, mais templates)

2. **System Prompt não é suficiente**
   - 1000+ linhas de regras não vence treinamento pré-existente
   - LLM preferir `$group` (que conhece) vs `group-by` (que aprendeu)

3. **Fluxo complexo com fallback é frágil**
   - Muitos pontos de falha antes de fallback ativar
   - Melhor: fallback é plano A, não plano C

4. **Inferência de parâmetros é difícil sem semântica**
   - Heurísticas simples (first string, first number) falham
   - Precisa NLP ou user input

### ✅ O Que Funcionou Bem

1. **Parse robusto** (Commit 1)
   - 3-strategy fallback é solid
   - Nunca quebra em JSON malformado

2. **Error classification** (Commit 1)
   - Smart retry decisions
   - Repeat detection é elegant

3. **Server-side schema** (Commit 2 - conceito)
   - OutputSchemaInferer é perfeito
   - Apenas flow integration que quebrou

4. **Template library** (Commit 3)
   - T1, T5, T7 são usáveis
   - Precisa melhor matching, não redesign

---

## 🎬 Conclusão

| Componente | Qualidade | Pronto Prod? |
|-----------|-----------|------------|
| Parse Resilience (C1) | ⭐⭐⭐⭐⭐ | ✅ Sim |
| Error Classification (C1) | ⭐⭐⭐⭐⭐ | ✅ Sim |
| Schema Inference (C2) | ⭐⭐⭐⭐ | ⚠️ Com Fix |
| Template Library (C3) | ⭐⭐⭐ | 🔄 Precisa Tuning |
| **IT13 Result** | ⭐⭐ | ❌ Não |

### Próximos Passos

1. **Immediate** (hoje):
   - Revert Commit 2 flow change (manter code, manter apenas OutputSchemaInferer)
   - Validar que volta a 1/4 passing

2. **Short-term** (próximas 2h):
   - Implement template detection com análise semântica
   - Ativar fallback mais cedo no flow

3. **Medium-term** (próximas 4h):
   - Add more templates (T2, T4)
   - Melhorar parameter extraction com NLP

4. **Long-term** (backlog):
   - Mudar paradigma: Template (90%) + LLM (10% refinement)
   - Implementar user guidance (dizer ao user qual tipo suportamos)

---

**Relatório Preparado**: 2026-01-06  
**Status Geral**: 🟡 Infra excelente, mas Definition of Done não atingida  
**Recommendation**: Proceder com fixes identificados, depois retest IT13
