## 1.2.0 (2026-01-05)
- Backend/API: obrigatório `GET /processes/{processId}/versions` para suportar a UI (corrige falha 405).
- Backend/API: obrigatório `DELETE /connectors/{connectorId}` + `409` quando connector estiver em uso.
- Breaking: removido `REMOVIDO_authRef` (sem dependência de variáveis de ambiente por connector).
- Connector: `authType (NONE|BEARER|API_KEY|BASIC)` e `requestDefaults (method/body/contentType/headers/queryParams)`.
- ProcessVersion: `sourceRequest` agora suporta `body` e `contentType`.
- Testes: ampliar suíte (repo + integration + gherkin) para cobrir list versions, delete connector, auth injection e body/contentType.

# Release Notes

## 1.1.0 (2026-01-02)
- Convergência total de contratos entre decks (Shared ↔ Backend ↔ Frontend):
  - `Connector` agora inclui `REMOVIDO_authRef` (e remove detalhes de request que pertencem à versão).
  - `Process` inclui `status` e `outputDestinations` com tipos `LocalFileSystem` / `AzureBlobStorage`.
  - `ProcessVersion.version` definida como **inteiro** (seleção numérica no runner e rotas).
  - `dsl` padronizado como objeto `{profile, text}`.
  - `outputSchema` padronizado como **objeto JSON** (JSON Schema).
- Correção do OpenAPI: endpoint `/api/ai/dsl/generate` movido para dentro de `paths`.
- Error contract unificado:
  - `ApiError` / `AiError` agora usam `correlationId` e `details` estruturado.
- Remoção de referências acidentais (`filecite`) e limpeza de docs para ficar **100% implementável**.

## 1.0.0 (2026-01-01)
- Primeira versão do spec deck com stack, UI specs e backend pipeline.
