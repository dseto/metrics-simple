
# Shared Contracts Reference (UI)

A UI deve consumir contratos canônicos em:

- OpenAPI: `../../shared/openapi/config-api.yaml`
- Schemas: `../../shared/domain/schemas/*.schema.json`

Regras:
- Não redefinir DTOs por conta própria.
- O `ui-api-client-contract.md` deve permanecer consistente com OpenAPI shared.
