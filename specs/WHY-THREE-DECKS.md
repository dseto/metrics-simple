# Por que existem 3 decks?

## shared (contratos)
- Mantém **OpenAPI + JSON Schemas + exemplos** como **SSOT** (single source of truth).
- Evita drift entre backend e frontend.
- Mudanças aqui são tratadas como mudanças de contrato.

## backend (implementação)
- Regras de execução, DSL, persistência (SQLite), logs e CLI.
- Referencia `specs/shared/*` para modelos e endpoints.

## frontend (implementação UI)
- UI (Material 3), páginas, componentes, field catalog e integração com API.
- Referencia `specs/shared/*` para contratos e validações.



## Nota sobre IA
A geração assistida de DSL é **design-time** e depende de contratos em `shared`.


## Version
This structure is canonical for v1.0.
