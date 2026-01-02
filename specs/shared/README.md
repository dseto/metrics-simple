# Shared Specs (Contracts)

Data: 2026-01-02

## Propósito
Este deck contém os **contratos canônicos** consumidos por **backend** e **frontend**.

- OpenAPI: `specs/shared/openapi/config-api.yaml`
- Schemas (JSON Schema 2020-12): `specs/shared/domain/schemas/*.schema.json`
- Tipos & constantes: `specs/shared/domain/types/*`
- Exemplos: `specs/shared/examples/*`

## Regras
- **Nunca duplicar** contratos em outros decks.
- Backend e Frontend devem **referenciar** estes arquivos (paths relativos).
- Mudanças aqui devem ser tratadas como mudanças de contrato (potencialmente breaking).
- Sempre atualizar `spec-manifest.json` (lista de arquivos e checksums).

## Como usar
- Backend: validar requests/responses contra schemas; usar o OpenAPI como contrato de rotas.
- Frontend: usar o OpenAPI como fonte para o client HTTP; usar schemas para validações e/ou geração de tipos (opcional).

## AI (design-time)
Endpoints e schemas para geração assistida de DSL/Schema (apenas *suggestions*):
- `domain/schemas/dslGenerateRequest.schema.json`
- `domain/schemas/dslGenerateResult.schema.json`
- `domain/schemas/aiError.schema.json`

## Guia de Schemas
- `specs/shared/domain/SCHEMA_GUIDE.md`

Version: 1.1.3
