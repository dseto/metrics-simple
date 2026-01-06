# Plan Ops Reference (IR v1)

Este documento define como cada `op` do plano deve ser executado pelo backend.

## Convenções
- `recordPath` aponta para um array de objetos no sample input.
- Cada item do recordset é tratado como um "row object".
- JSON Pointers em `from`, `by`, `keys`, etc. são **relativos ao objeto row**.

## `select`
Projeção e rename.
- Entrada: array<object>
- Saída: array<object> com apenas as colunas especificadas.
- Para cada fieldSpec:
  - resolve o valor via JSON Pointer
  - se não existir, setar `null` (não falhar), e adicionar warning

## `filter`
Filtra rows por condição.
- Condições suportadas: eq, neq, gt, gte, lt, lte, in, contains, and, or, not
- Operandos:
  - literal
  - referência a campo `{ "field": "/status" }`

## `compute`
Cria novas colunas computadas.
- `expr` suporta:
  - identifiers (nomes) mapeados para campos do row (ex.: `price`, `quantity`)
  - literais numéricos
  - operadores: + - * / e parênteses
- Implementar parser simples (shunting-yard) ou avaliador seguro.
- Se campos não existirem ou não forem numéricos: resultado `null` + warning.

## `mapValue`
Mapeia valores por dicionário.
- Se valor atual existir em `mapping`, substitui
- Se não existir, usa `default` (se definido), senão mantém valor original

## `groupBy` + `aggregate`
`groupBy` define chaves.
`aggregate` calcula métricas por grupo.
- Chaves: lista de JSON pointers (ex.: ["/category"])
- Métricas:
  - count: conta rows do grupo
  - sum/avg/min/max: usa `expr` ou `field`
- Saída: array<object> com colunas:
  - uma coluna por key (usar nome do último segmento do pointer como default)
  - uma coluna por metric `as`

## `sort`
Ordena por campo.
- `by`: JSON pointer (ex.: "/date")
- `dir`: asc|desc
- Comparação:
  - number > string > boolean > null
  - para strings, usar ordinal ignore-case
  - para datas, v1 não tenta parse automático (tratar como string)

## `limit`
Trunca o resultado para N rows.

## Warnings (non-fatal)
- MissingField
- TypeMismatch
- AmbiguousRecordPath (resolvido automaticamente)
