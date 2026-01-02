
# Output Validation (JSON Schema)

Data: 2026-01-01

Valida output da transformação contra outputSchema.

Regras:
- outputSchema deve ser schema válido
- em falha: OUTPUT_VALIDATION_FAILED + detalhes com paths
- Preview: retorna isValid=false + errors[]
