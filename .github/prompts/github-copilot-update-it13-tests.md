# Prompt — Atualizar IT13 para pipeline resiliente

## 🧭 Precedência e Escopo

- Este prompt atua **somente** nos testes de integração IT13.
- NÃO mexer no pipeline nem no prompt system aqui.

---

## 🎯 Objetivo

Alinhar IT13 com o novo comportamento:

- outputSchema é inferido no servidor
- política de renomeação é explícita
- retry não pode ser inútil nem lento

---

## ✅ Definition of Done

- IT13 passa de forma determinística
- Nenhum teste depende de schema vindo da LLM
- Nenhum teste falha por tradução implícita de campo
- Aggregation não permite 3 tentativas idênticas
- Testes não demoram >15s

---

## 🔎 Ajustes obrigatórios

### 1. outputSchema

- Validar que:
  - é JSON Schema válido
  - é coerente com o preview
- NÃO validar estrutura textual exata.

---

### 2. WeatherForecast test

- Se o prompt não pedir rename de `date`, o output deve manter `"date"`.
- Ajustar asserts conforme policy *ExplicitOnly*.

---

### 3. Aggregation test

- Assertar que:
  - não houve 3 retries idênticos
  - fallback ocorreu (template ou correção)
  - tempo total não é excessivo.

---

### 4. Latência e tentativas

Sempre que possível, capturar e validar:

- attempts count
- error category
- elapsed time

---

## 📌 Arquivo alvo

- tests/Integration.Tests/IT13_LLMAssistedDslFlowTests.cs

---

## 🚫 Não fazer

- Não mockar o pipeline real
- Não enfraquecer asserts funcionais
- Não remover testes — apenas alinhá‑los
