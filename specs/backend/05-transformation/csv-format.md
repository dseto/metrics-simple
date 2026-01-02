# CSV Format (Deterministic)

Data: 2026-01-02

## Encoding
- UTF-8 (sem BOM)

## Newline
- Newline **fixo**: `\n` (LF), independente do sistema operacional
- O arquivo gerado **deve terminar** com `\n`

## Delimiter
- `,`

## Quoting (RFC4180 pragmático)
- Envolver em aspas **se contiver**: vírgula (`,`), aspas (`"`), `\n` **ou** `\r`
- Escapar aspas duplicando: `"` -> `""`

## Colunas (ordem)
- Preferir a ordem declarada no `outputSchema`:
  - Se `outputSchema.type == "array"` e `items.type == "object"`:
    - ordem = `outputSchema.items.properties` **na ordem em que aparece no JSON**
  - Se `outputSchema.type == "object"`:
    - ordem = `outputSchema.properties` na ordem declarada
- Fallback determinístico:
  - união de chaves encontradas nas linhas + ordenação **alfabética** (ordinal)

## Tipos
- `null` -> vazio
- `boolean` -> `true`/`false`
- `number` -> texto raw do JSON (ponto decimal, independente de locale)
- `object/array` -> JSON compact (determinístico)

## Shape de output
- `array` -> N linhas
- `object` -> 1 linha (MUST ser tratado como `[object]` pelo pipeline antes de gerar CSV)
- outros tipos -> erro (`TRANSFORM_FAILED`)
