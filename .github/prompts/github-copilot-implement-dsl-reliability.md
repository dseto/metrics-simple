# Prompt — Implementar Confiabilidade da Geração de DSL (BACKEND)

## 🔴 PRIORIDADE MÁXIMA
Este prompt trata do **coração da solução**: geração de DSL Jsonata via LLM.
Qualquer instabilidade aqui invalida o produto.

---

## 🧭 Precedência e Escopo

- Este prompt **tem prioridade** sobre qualquer agente base.
- **Escopo fechado**:
  - SOMENTE backend
  - NÃO mexer em UI
  - NÃO criar novas features
  - NÃO alterar contratos públicos (OpenAPI) sem instrução explícita aqui.

---

## 🎯 Objetivo

Corrigir definitivamente:

- Respostas não‑JSON / structured outputs quebrados  
- `outputSchema` inválido vindo da LLM  
- Alucinação de sintaxe Jsonata (`$group`, `[field]` para ordenar)  
- Retry inútil (mesmo erro 3x)  
- Regressão de latência

---

## ✅ Definition of Done (mensurável)

- IT13 com **≥ 3/4 testes passando**
- Nenhum teste > **15s**
- Nenhuma resposta da LLM quebra parsing (sempre entra no retry)
- Aggregation NÃO pode repetir `$group` duas vezes → deve cair em fallback
- `outputSchema` SEMPRE é gerado pelo backend a partir do preview

---

## 🏗️ Entregáveis obrigatórios

### A) OpenRouter hardening (HttpOpenAiCompatibleProvider.cs)

Implementar no request:

- `response_format.type = "json_schema"`
- `json_schema.strict = true`
- `provider.require_parameters = true`
- `provider.allow_fallbacks = false`
- `plugins: [{ id: "response-healing" }]` (non‑streaming)

E logar por tentativa:
- model
- provider (se vier)
- request-id
- tentativa
- erro classificado

---

### B) Contrato mínimo da LLM

A LLM deve retornar **somente**:

```json
{
  "dsl": { "text": "..." },
  "notes": "optional"
}
```

❌ Proibido pedir `outputSchema` para a LLM.

O response final do endpoint **DEVE continuar contendo `outputSchema`**, mas **gerado no servidor**.

---

### C) Parse resiliente

Criar função utilitária:

- remove ```json
- extrai do primeiro `{` ao último `}`
- tenta 2–3 variações de parse
- se falhar → erro classificado `LLM_RESPONSE_NOT_JSON` → retry

Nenhum erro de parse pode “matar” o fluxo sem retry.

---

### D) Retry inteligente (default MaxAttempts = 2)

Classificar erros:

- LLM_RESPONSE_NOT_JSON
- LLM_CONTRACT_INVALID
- JSONATA_SYNTAX_INVALID
- JSONATA_EVAL_FAILED

Regras:

- Sempre tentar repair na 2ª tentativa
- Detectar repetição (mesma categoria + mesma DSL normalizada)
- Se repetir → **parar retry e ir para template fallback**

---

### E) Template fallback (mínimo viável)

Implementar inicialmente:

- T1 — Extract + Rename  
- T5 — Group + Sum  
- (opcional depois: T2 — Sort)

Criar:

- `DslTemplateLibrary`
- `DslTemplateMatcher` (heurístico simples por keywords)

Se template aplicar → gerar DSL sem LLM.

---

### F) Inferência determinística de outputSchema

Após preview válido:

- Inferir JSON Schema do output real
- Nunca confiar em schema vindo da LLM
- `IT13` não pode mais falhar por `outputSchema must be a JSON object`

---

### G) Política de renomeação

- NÃO traduzir nomes de campos (`date` → `data`)  
- SOMENTE renomear quando o usuário pedir explicitamente.

Enforce isso:
- no prompt system
- nos templates

---

## 🧪 Testes e regressões

- Atualizar código para permitir asserts de:
  - número de tentativas
  - categoria de erro
  - tempo de execução

- Não quebrar casos que já funcionavam.

---

## 🔧 Estratégia de implementação (obrigatória)

Implementar em **3 commits lógicos**:

1. OpenRouter hardening + parse resiliente + logs  
2. Inferência de outputSchema  
3. Retry pattern detection + templates

Cada commit deve compilar e rodar testes.

---

## 📌 Arquivos principais

- src/Api/AI/HttpOpenAiCompatibleProvider.cs  
- src/Api/Program.cs (ou controller equivalente)  
- tests/Integration.Tests/IT13_LLMAssistedDslFlowTests.cs  

---

## 🚫 Não fazer

- Não adicionar APM externo
- Não mudar contrato público sem atualizar testes
- Não criar abstrações genéricas desnecessárias
