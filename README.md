# Metrics Simple — Spec Deck (v1.1.0)

Este repositório contém um **spec deck** completo para desenvolver a aplicação **Metrics Simple** usando metodologia **Spec Driven Design**.

## Estrutura
- `specs/shared/` — contratos canônicos (OpenAPI + JSON Schemas + exemplos)
- `specs/backend/` — comportamento e requisitos do backend/runner
- `specs/frontend/` — UI (Angular + Material 3): rotas, estados, componentes e validações

## Regras de escopo (v1.x)
- Implementação **sincrona** (sem filas e sem Azure Functions).
- Persistência local em SQLite.
- Exportação de CSV para Local File System e/ou Azure Blob Storage (opcional).
- IA (LLM) **apenas design-time** (sugestão de DSL/Schema), com validação no backend.

## Fonte da verdade
- OpenAPI: `specs/shared/openapi/config-api.yaml`
- Schemas: `specs/shared/domain/schemas/*.schema.json`

## Como usar
1. Comece por `SCOPE.md` e `TECH_STACK.md`.
2. Navegue pelos decks via `specs/spec-index.md`.
3. Implemente primeiro os contratos do deck `shared` (API + schemas).
