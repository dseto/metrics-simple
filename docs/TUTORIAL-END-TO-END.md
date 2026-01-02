# Tutorial end-to-end (Spec‑Driven)

Este tutorial é para executar a abordagem spec-driven com VS Code (Insiders) + Copilot (Agent Mode).

## 0) Preparação
- Abra o repositório no VS Code
- Garanta que a pasta `specs/` está presente
- Leia `specs/spec-index.md`

## 1) Validar contratos (Shared)
- OpenAPI: `specs/shared/openapi/config-api.yaml`
- Schemas: `specs/shared/domain/schemas/*.schema.json`
Checklist:
- `$ref` resolvem
- exemplos batem com schemas

## 2) Backend (ordem recomendada)
1. SQLite schema (`specs/backend/06-storage/sqlite-schema.md`)
2. Repositórios (`specs/backend/06-storage/repository-contracts.md`)
3. Engine + CSV (`specs/backend/05-transformation/*`)
4. API endpoints (`specs/backend/03-interfaces/*`)
5. Runner CLI (`specs/backend/04-execution/*`)
6. Logs (`specs/backend/07-observability/*`)

## 3) Frontend
1. Tokens (Material 3) (`specs/frontend/design/*`)
2. Rotas + state machine (`specs/frontend/11-ui/ui-routes-and-state-machine.md`)
3. Component specs + field catalog (`specs/frontend/11-ui/component-specs.md`, `ui-field-catalog.md`)
4. Integração com API (`specs/frontend/11-ui/ui-api-client-contract.md`)

## 4) Prompts
Use `PROMPTS.md` como roteiro de execução para o Copilot Agent, etapa por etapa.

## 5) Definition of Done
- Contratos shared sem drift
- Backend compila e executa fluxo mínimo
- Frontend renderiza telas principais e chama a API
- Golden vectors documentados

