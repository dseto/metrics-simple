# Release Notes

## 1.2.0 (2026-01-02)
- **Obrigatório**: Integration Tests (E2E) do backend com:
  - WebApplicationFactory (API in-memory)
  - Mock HTTP real para FetchSource (WireMock.Net preferido)
  - Runner CLI executado como processo real
  - SQLite real via `METRICS_SQLITE_PATH`
  - Secrets via `METRICS_SECRET__<authRef>`
- Novo spec: `specs/backend/09-testing/integration-tests.md`
- Playbook do agent atualizado para incluir etapa de Integration Tests.
- Stack atualizada para **.NET 10** no deck e nas instruções de governança.

## 1.1.3 (2026-01-02)
- Engine: normalização obrigatória do output do DSL para **rows array** antes de validar schema/gerar CSV (object -> [object], null -> []) para lidar com edge cases do JSONata (ex.: `$map` com 1 item).
- CSV determinístico reforçado:
  - newline **fixo** `\n` (independente do SO)
  - quoting também quando contém `\r`
  - ordem de colunas: **preferir ordem do outputSchema** (items.properties), fallback alfabético.
- Schema validation: recomendações e contrato para `outputSchema` **self-contained** (sem `$ref` externo) + caching de schema parseado.
- Prompt adicional para implementar as melhorias (EngineService + CsvGenerator + SchemaValidator) sem alterar contratos shared.


## 1.1.2 (2026-01-02)
- Documentação formal de `$ref` e granularidade de schemas: `specs/shared/domain/SCHEMA_GUIDE.md`.
- Guia de evolução/refatoração de specs e checklist anti-drift: `docs/EVOLUTION.md`.
- Suite canônica de Golden Tests adicionada (YAML + fixtures) para transformação + CSV determinístico:
  - `specs/backend/05-transformation/unit-golden-tests.yaml`
  - `specs/backend/05-transformation/fixtures/*`
  - `specs/backend/05-transformation/golden-test-format.md`
- Ferramentas opcionais de validação estrutural por manifest: `tools/spec-validate.(ps1|sh)`.


## 1.1.1 (2026-01-02)
- Agent `spec-driven-builder` revisado para ser mais determinístico:
  - ordem de precedência (Shared > Backend > Frontend)
  - etapas com validações obrigatórias (stop-the-line)
  - critérios de DoD globais e por etapa

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
