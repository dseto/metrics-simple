# Metrics Simple — Spec Deck (v1.2.0)

Este repositório contém um **spec deck** completo para implementar a solução **Metrics Simple** usando metodologia **Spec Driven Design**.

## Estrutura
- `specs/shared/` — contratos canônicos (OpenAPI + JSON Schemas + exemplos)
- `specs/backend/` — comportamento e requisitos do backend/runner/engine
- `specs/frontend/` — UI (Angular + Material 3): rotas, estados, componentes e validações

## Regras de escopo (v1.x)
- Execução **síncrona** (sem filas e sem Azure Functions).
- Persistência local em **SQLite**.
- Exportação de CSV para **Local File System** e/ou **Azure Blob Storage** (opcional).
- IA (LLM) **apenas design-time** (sugestão de DSL/Schema), com validação no backend.

## Qualidade mínima (obrigatório)
- Contract tests + golden tests **e** **integration tests** devem rodar em CI/local:
  - Contract: OpenAPI + schemas + DTOs
  - Golden: transformação (Jsonata real) + schema + CSV (RFC4180)
  - Integration (E2E): WebApplicationFactory + mock HTTP para **FetchSource** + persistência SQLite + execução do runner

> A spec **exige** que exista ao menos 1 suíte de **integration tests** que valide o caminho real:
> connector/process/version → fetch HTTP (mockado) → transform → validate → csv → persistência/outputs.

## Fonte da verdade
- OpenAPI: `specs/shared/openapi/config-api.yaml`
- Schemas: `specs/shared/domain/schemas/*.schema.json`

## Como usar
1. Comece por `SCOPE.md` e `TECH_STACK.md`.
2. Navegue pelos decks via `specs/spec-index.md`.
3. Implemente primeiro os contratos do deck `shared` (API + schemas).
4. Em seguida: backend (engine + runner + storage + logs).
5. Por fim: **tests obrigatórios**, incluindo integration tests (ver `specs/backend/09-testing/integration-tests.md`).
