# Page — Connectors

Data: 2026-01-05

## Route
- `/connectors`

## Purpose
CRUD de Connectors para chamadas a APIs externas:
- baseUrl
- autenticação (NONE/BEARER/API_KEY/BASIC)
- defaults de request (método, headers, query, body, contentType)
- delete connector (com regra 409 em uso)

## API dependencies
- GET `/api/v1/connectors`
- POST `/api/v1/connectors`
- GET `/api/v1/connectors/{connectorId}`
- PUT `/api/v1/connectors/{connectorId}`
- DELETE `/api/v1/connectors/{connectorId}`

## List / Table
Colunas sugeridas:
- Id
- Name
- Base URL
- Auth Type
- Indicadores:
  - Token configurado (hasApiToken)
  - API Key configurada (hasApiKey)
  - Basic password configurada (hasBasicPassword)
- Timeout
- Enabled
- Actions: Edit, Delete

## Create / Edit dialog

### Fields (non-secret)
- id (create only)
- name
- baseUrl
- timeoutSeconds
- enabled
- authType: NONE | BEARER | API_KEY | BASIC
- API_KEY config:
  - apiKeyLocation: HEADER|QUERY
  - apiKeyName: string
- BASIC config:
  - basicUsername: string

### Secrets (write-only)
- BEARER: apiToken + apiTokenSpecified (via UI: “Atualizar token” / “Limpar token”)
- API_KEY: apiKeyValue + apiKeySpecified (via UI: “Atualizar API key” / “Limpar API key”)
- BASIC: basicPassword + basicPasswordSpecified (via UI: “Atualizar senha” / “Limpar senha”)

Regras:
- Segredos nunca são exibidos após salvar.
- Em edição: se usuário não tocar no campo, a UI não envia `*Specified` (mantém).
- “Limpar …”: enviar `*Specified=true` e valor `null`.

### requestDefaults
- method (GET|POST)
- headers (map)
- queryParams (map)
- body (textarea JSON ou texto)
- contentType (ex.: application/json)

## Delete behavior
- Ao clicar Delete:
  - chamar DELETE `/api/v1/connectors/{id}`
  - 204: remover da lista, toast sucesso
  - 409: mostrar mensagem “Conector em uso por processos; remova referências antes.”
