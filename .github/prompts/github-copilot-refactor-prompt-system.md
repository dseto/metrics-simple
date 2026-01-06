# Prompt — Refatorar Prompt System (reduzir overload + few‑shot)

## 🧭 Precedência e Escopo

- Este prompt atua **somente** no prompt system da LLM.
- NÃO mexer no pipeline, retry, templates ou testes aqui.

---

## 🎯 Objetivo

- Reduzir prompt system (~150+ linhas → ~50–70)
- Aumentar obediência às regras críticas
- Eliminar causas diretas de falha:
  - `$group` inexistente
  - ordenação errada (`[date]`)

---

## ✅ Definition of Done

- Prompt final menor, claro e estável
- 3 exemplos few‑shot explícitos
- Regras críticas fáceis de localizar
- Nenhuma instrução conflitante

---

## 🔑 Regras obrigatórias a manter

- Raiz implícita (não usar `$.`)
- Ordenação SOMENTE com `^()`
- ❌ `$group` NÃO existe (usar `$distinct + $sum`)
- Não traduzir nomes de campos sem pedido explícito
- Validar caminhos contra sample input

---

## 📚 Few‑shot OBRIGATÓRIOS

Incluir exatamente estes 3 padrões:

1) **Extraction + rename (PT)**  
2) **Group by + sum (EN)**  
3) **Sort asc/desc (forecast/date)**  

Cada exemplo deve conter:
- Prompt do usuário
- Sample input reduzido
- DSL correta

---

## 🛠️ Estrutura recomendada

1. Missão curta (o que você é)
2. 6–8 regras críticas
3. Política de renomeação
4. Few‑shot examples
5. Output contract (somente DSL)

---

## ⚠️ Repair prompt

Separar:

- system prompt fixo (acima)
- repair prompt pequeno, dinâmico, com:
  - erro detectado
  - hint específico
  - no máximo 1 exemplo relacionado

---

## 📌 Arquivo alvo

- src/Api/AI/HttpOpenAiCompatibleProvider.cs

---

## 🚫 Não fazer

- Não voltar a criar prompt gigante
- Não misturar regras irrelevantes
- Não reintroduzir geração de outputSchema pela LLM
