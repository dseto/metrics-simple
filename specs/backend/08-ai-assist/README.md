# 08 — AI Assist (Design-time)

Data: 2026-01-02

Este módulo adiciona **geração assistida de DSL + Output Schema** via LLM **apenas no design-time** (Studio/UI).
O objetivo é acelerar a criação de versões (ProcessVersion), sem afetar o runtime.

Regras-chave:
- IA é **somente sugestão** (design-time).
- Execução/runtime continua **100% determinística** (runner não chama LLM).
- Toda saída de IA passa por validação: parser DSL + validação do schema + preview interno.

Arquivos:
- `ai-endpoints.md`
- `ai-provider-contract.md`
- `prompt-templates.md`
- `guardrails-and-validation.md`
- `ai-tests.md`
