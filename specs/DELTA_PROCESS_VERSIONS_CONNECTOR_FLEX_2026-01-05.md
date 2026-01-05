# DELTA — Process Versions + Connector Flex + Remoção AuthRef

Data: 2026-01-05  
Versão alvo: 1.2.0

## Motivação (achados de testes)
1. O frontend não exibe versões e chama `GET /api/v1/processes/{processId}/versions` recebendo **405**: endpoint não implementado no backend.
2. O frontend tem botão **Delete Connector** chamando `DELETE /api/v1/connectors/{connectorId}` e falha porque o endpoint não existe/ não está implementado.
3. `authRef` não faz mais sentido: foi substituído por **Secrets persistidos** (API Token e outros), sem depender de variáveis de ambiente por connector.
4. Connectors precisam suportar: método (GET/POST), body, contentType, headers/query e múltiplos tipos de autenticação (não apenas URL+token).

---

## Alterações de contrato

### Connector (breaking)
- **Removido**: `authRef`
- **Adicionado**: `authType: NONE | BEARER | API_KEY | BASIC`
- **BEARER**: usa `apiToken` (writeOnly) + semântica `apiTokenSpecified`
- **API_KEY**: usa `apiKeyLocation` + `apiKeyName` + `apiKeyValue` (writeOnly) + `apiKeySpecified`
- **BASIC**: usa `basicUsername` + `basicPassword` (writeOnly) + `basicPasswordSpecified`
- **requestDefaults**: `method`, `headers`, `queryParams`, `body`, `contentType`

Semântica de update (PUT):
- Secrets só alteram quando `*Specified=true`.
- `null` remove; `string` substitui; omitido mantém.
- Secrets **nunca** são retornados na API (somente flags `has*`).

### ProcessVersion.sourceRequest
- Adicionado suporte a `body` e `contentType`.

---

## Alterações de API (Backend)

### Obrigatório implementar (evitar 405)
- `GET /processes/{processId}/versions`
  - retorna array de versões
  - ordenação por `version asc`
  - 404 se o processo não existir

### Obrigatório implementar (evitar endpoint ausente)
- `DELETE /connectors/{connectorId}`
  - 204 em sucesso
  - 404 se não existir
  - **409** se connector estiver em uso por algum processo

---

## Composição de request (Runner)
- Merge `connector.requestDefaults` + `processVersion.sourceRequest`:
  - headers/query: defaults primeiro, version sobrescreve
  - body/contentType: version vence; senão defaults
- Injeção de auth ocorre após merge e **não pode sobrescrever** valores explicitamente definidos.

---

## Storage (SQLite)
- Remover `connectors.authRef`
- Adicionar colunas para `authType` + configs e defaults (JSON)
- Persistir secrets em tabela `connector_secrets` (AES-256-GCM) usando `METRICS_SECRET_KEY`
- `connector_tokens` passa a ser **LEGACY** (migração para `connector_secrets` quando existir)
- Adicionar `process_versions.bodyText` e `process_versions.contentType`

---

## Testes exigidos

### Backend — Unit/Repo
- CRUD e migração de `connector_secrets`
- delete connector remove secrets associados
- delete connector em uso => 409 (service/API)

### Backend — Integration
- list versions
- delete connector 204/409
- runner BEARER + (API_KEY ou BASIC)
- POST com body/contentType

### Frontend — E2E (Gherkin)
- list versions carrega e mostra
- delete connector trata 409
- create/edit connector sem authRef e com auth types + defaults
