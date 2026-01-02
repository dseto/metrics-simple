# Release Notes

## 1.1.0 (2026-01-02)
- Convergência total de contratos entre decks (Shared ↔ Backend ↔ Frontend):
  - `Connector` agora inclui `authRef` (e remove detalhes de request que pertencem à versão).
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
