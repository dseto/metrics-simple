# DSL Engine Spec

Data: 2026-01-02

Perfis:
- `jsonata` (**obrigatório**)
- `jmespath` (opcional)
- `custom` (reservado; pode retornar "not supported")

## Pipeline canônico (EngineService)
A Engine deve seguir exatamente esta ordem:

1) **Transform**: executar DSL (`dsl.profile` + `dsl.text`) sobre o JSON de input
2) **Normalize Rows**: normalizar o output para "rows array" (array-of-objects)
3) **Validate Schema**: validar o output normalizado contra `outputSchema`
4) **Generate CSV**: gerar CSV determinístico conforme `csv-format.md` (inclui ordem de colunas)

### Normalize Rows (obrigatório)
Motivo: alguns DSLs (incluindo JSONata) podem produzir um **singleton object** em casos de 1 item.

Regra:
- se output é `array` -> usar como está
- se output é `object` -> tratar como **uma linha** => `[object]`
- se output é `null` -> tratar como `[]` (0 linhas)
- qualquer outro tipo -> erro `TRANSFORM_FAILED`

## jsonata
- Input: JSON do FetchSource
- Expression: `dsl.text` (JSONata válido)
- Output: JSON

Erros:
- parse/compile -> `DSL_INVALID`
- runtime/eval -> `TRANSFORM_FAILED`

Determinismo:
- mesma entrada + mesma expressão -> mesmo output
- cache de expressão compilada é recomendado (por performance)

## outputSchema (validação)
- `outputSchema` é JSON Schema draft 2020-12
- MUST ser **self-contained**:
  - permitido: `$ref` interno (ex.: `#/definitions/...`)
  - proibido: `$ref` externo por arquivo/URL (sem contexto de path em runtime)

