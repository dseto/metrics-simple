# Release Notes — Plan Engine (IR v1)

**Data**: 2026-01-06

## Added
- Novo modo `engine=plan_v1` no endpoint `/api/v1/ai/dsl/generate`
- Execução determinística por plano (IR) com preview e schema inferido permissivo
- Detecção de recordset (arrayPath) e resolução de campos baseada no sample input

## Changed
- Roteamento do endpoint para selecionar o motor por request/config
- IT13 pode ser executado com `engine=plan_v1`

## Compatibility
- `engine` é opcional (default permanece `legacy` até promoção controlada)
- Resposta mantém campos existentes; `plan` só aparece quando solicitado explicitamente (`includePlan=true`)
