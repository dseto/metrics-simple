
# Backend Contract Tests

> Nota: além de contract tests, a entrega exige **integration tests E2E** (ver `integration-tests.md`).

Data: 2026-01-01

1) Validar parse do OpenAPI shared:
- `../../shared/openapi/config-api.yaml`

2) Validar parse dos schemas shared:
- `../../shared/domain/schemas/*.schema.json`

3) Testar erros (shape + status):
- 409 id duplicado
- 404 not found
- 422 connector inexistente

4) Ordenação determinística:
- GET /processes e GET /connectors por id asc


## Resolução de `$ref` (schemas)
Os schemas em `specs/shared/domain/schemas/` usam `$ref` relativo (ex.: `id.schema.json`).
Nos testes, carregue schemas **por arquivo** (para preservar documentPath) ou configure um resolver com basePath.
Veja: `specs/shared/domain/SCHEMA_GUIDE.md`.

## Validação estrutural (recomendado)
Adicionar um teste que:
- lê `spec-deck-manifest.json` e valida que todos os `path` existem no repo
- (opcional) valida o `sha256` para detectar drift não intencional
