
# Specs — 3 Decks (shared / backend / frontend)

Data: 2026-01-01

Este repositório usa **3 decks de especificação** para maximizar a estabilidade da geração autônoma (spec-driven) e evitar divergência entre camadas.

## Por que existem 3 decks?
Quando frontend e backend evoluem, é comum ocorrer **drift de contrato** (o front espera um campo/enum e o back entrega outro).
Separar em 3 decks força uma regra simples:

- **`shared`**: define *o que é o contrato* (fonte de verdade única)
- **`backend`**: define *como o servidor executa* (engine, runner, storage, logs, etc.)
- **`frontend`**: define *como a UI se comporta* (telas, componentes, validações client-side, state machines)

Isso reduz ambiguidade para agentes e permite trabalhar com chats/agentes separados por domínio.

---

## Deck 1 — `specs/shared/` (Fonte de verdade dos contratos)
**Objetivo:** manter contratos canônicos consumidos por frontend e backend.

### O que fica aqui
- OpenAPI (Config API / Preview): `specs/shared/openapi/config-api.yaml`
- JSON Schemas do domínio: `specs/shared/domain/schemas/*.schema.json`

### Regras de uso (IMPORTANTES)
1. **Não duplicar contrato**: OpenAPI e Schemas **não** devem existir em `backend/` ou `frontend/` (apenas referências).
2. **Mudanças em shared são deliberadas**:
   - alterações de enums/campos podem quebrar consumidores
   - ideal versionar `shared` (SemVer) e registrar breaking changes
3. **Tests de contrato**:
   - backend deve validar OpenAPI+Schemas (parseáveis)
   - frontend deve validar que suas validações/catálogos estão consistentes com shared

> Em resumo: `shared` define o “**formato do mundo**” para todos.

---

## Deck 2 — `specs/backend/` (Execução server-side)
**Objetivo:** especificar implementação do servidor e execução (determinística).

Inclui:
- engine/transformation, CSV, validação
- runner CLI, exit codes, file layout, retention
- storage (local + blob), logging JSONL

---

## Deck 3 — `specs/frontend/` (UI Material 3)
**Objetivo:** especificar telas, componentes, estados e clientes de API (sem inventar contrato).

Inclui:
- Material Design 3, tokens e navegação
- páginas e componentes (props/events/validações)
- state machines por rota
- API client contract (normalização, UiError mapping)
- estratégia de testes unitários (sem E2E)

---

## Como trabalhar (playbook)
- Use `PROMPTS.md` como guia.
- Recomendação: **1 chat = 1 etapa**.
- Backend e frontend podem ser gerados por agentes diferentes.
