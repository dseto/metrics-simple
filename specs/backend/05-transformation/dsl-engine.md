
# DSL Engine Spec

Data: 2026-01-01

Perfis:
- jsonata (mínimo obrigatório)
- jmespath (opcional)
- custom (reservado; pode retornar "not supported")

## jsonata
- Input: JSON do FetchSource
- Expression: dsl.text
- Output: JSON

Erros:
- parse -> DSL_INVALID
- runtime -> TRANSFORM_FAILED

Requisito:
- determinismo (mesmo input + expressão -> mesmo output)
