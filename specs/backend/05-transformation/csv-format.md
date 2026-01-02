
# CSV Format (Deterministic)

Data: 2026-01-01

## Encoding
- UTF-8 (sem BOM)
- Newline: \n

## Delimiter
- ,

## Quoting
- usar aspas se conter vírgula, aspas ou newline
- escapar aspas duplicando

## Colunas
- Preferir ordem do outputSchema.properties quando aplicável
- Fallback: ordem alfabética

## Tipos
- null -> vazio
- boolean -> true/false
- number -> ponto decimal (Invariant)
- object/array -> JSON compact (determinístico)

## Output
- array -> N linhas
- object -> 1 linha
