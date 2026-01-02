# Guardrails & Validation

Data: 2026-01-02

## Regras (hard)
- IA nunca executa no runtime; apenas gera sugestão que vira versão após confirmação explícita do usuário.
- Saída de IA deve ser validada por:
  1) Parser DSL (Engine)
  2) JSON Schema parse/validação do schema
  3) Preview/Transform interno com `sampleInput`
- Se qualquer etapa falhar: retornar erro e **não** salvar versão automaticamente.

## Sanitização / Privacidade
- `sampleInput` pode conter PII. Evitar persistência por padrão.
- Logs: armazenar apenas hash + tamanho + contagem aproximada de tokens (quando disponível).

## Controles de custo
- Limitar tamanho do `sampleInput` (limite recomendado: 500k bytes após serialização).
- Limitar `goalText` (máx. 4k).
- Rate limit por IP/usuário (opcional).

## UX (mínimo)
- UI deve mostrar:
  - DSL sugerida
  - Schema sugerido
  - Warnings + rationale
  - Botão explícito: **Aplicar** (cria/atualiza versão)
