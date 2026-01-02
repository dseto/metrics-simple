# Golden Tests (Transformation)

Data: 2026-01-02

A suíte canônica está em:
- `specs/backend/05-transformation/unit-golden-tests.yaml`
- `specs/backend/05-transformation/fixtures/*`

## Requisitos
- Para cada caso:
  1) executar DSL (jsonata)
  2) **normalizar** output (object -> [object]) conforme `dsl-engine.md`
  3) validar contra `expectedSchemaFile`
  4) gerar CSV conforme `csv-format.md`
  5) comparar `expectedOutputFile` (JSON semântico) e `expectedCsvFile` (byte-a-byte)

## Comparação de JSON
- Comparar de forma **semântica** (parse -> re-serialize com opções fixas), evitando falhas por whitespace.

