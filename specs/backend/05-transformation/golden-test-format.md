# Golden Test Format (YAML)

Data: 2026-01-02  
Arquivo canônico: `specs/backend/05-transformation/unit-golden-tests.yaml`

## Objetivo
Definir casos de teste reprodutíveis (*golden*) para:
- transformação JSON -> JSON (via DSL)
- validação contra JSON Schema
- geração de CSV determinístico

## Estrutura
- `dslProfile`: perfil da DSL (v1.x requer `jsonata`)
- `dslFile`: arquivo com a expressão (ex.: `.jsonata`)
- `inputFile`: JSON de entrada
- `expectedOutputFile`: JSON esperado após transformação
- `expectedSchemaFile`: JSON Schema 2020-12 para validar o output
- `expectedCsvFile`: CSV esperado (bytes exatos)

## Regras
1) Paths são relativos ao YAML.
2) A engine deve falhar com:
   - `DSL_INVALID` em parse
   - `TRANSFORM_FAILED` em runtime
   - `OUTPUT_VALIDATION_FAILED` em violação de schema
3) CSV deve seguir `csv-format.md` (UTF-8, \n, escaping RFC4180).

## Como usar na implementação (sugestão)
- Test runner lê o YAML, executa cada caso e compara:
  - output JSON (normalizar ordenação se necessário)
  - CSV (comparação byte-a-byte)
