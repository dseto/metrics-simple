---
name: spec-driven-builder
description: Implementa a solução Metrics Simple de forma spec-driven, usando `specs/` como fonte de verdade. Trabalha em etapas, altera múltiplos arquivos, executa build/test e corrige iterativamente.
tools:
  ['vscode', 'execute', 'read', 'edit', 'search', 'web', 'agent', 'copilot-container-tools/*', 'ms-python.python/getPythonEnvironmentInfo', 'ms-python.python/getPythonExecutableCommand', 'ms-python.python/installPythonPackage', 'ms-python.python/configurePythonEnvironment', 'todo']
model: Claude Haiku 4.5 (copilot)
---

# Spec-Driven Builder — Playbook

## Regras de ouro
1) Leia `specs/00-vision/spec-index.md` e use como mapa.
2) Nunca contradiga `openapi-config-api.yaml` e os JSON Schemas.
3) Se existir ambiguidade, siga `specs/04-execution/pipeline-spec.md` e `specs/02-domain/source-request.md`.
4) Não implementar hardening de secrets (ACL/cripto/DPAPI). Risco aceito.
5) Sempre rodar `dotnet build` e `dotnet test` ao final de cada etapa.

## Ordem de execução (etapas)
### Etapa 0 — Setup inicial
- Garantir estrutura do repo conforme `specs/00-vision/repo-structure.md`.
- Confirmar `build-and-test.md` e preparar scripts de dev se necessário.

### Etapa 1 — Engine + Golden Unit Tests
- Implementar Engine conforme `specs/05-transformation/dsl-spec.md`.
- Gerar testes unitários a partir de `specs/05-transformation/unit-golden-tests.yaml`.
- `dotnet test`

### Etapa 2 — Persistência (SQLite) + Domínio
- Implementar modelos/repositórios conforme `specs/02-domain/schemas/*.schema.json`.
- Implementar schema SQLite conforme `specs/02-domain/sqlite-schema.md`.
- `dotnet test`

### Etapa 3 — Config API (IIS)
- Implementar endpoints conforme `specs/03-interfaces/openapi-config-api.yaml`.
- Implementar `/preview/transform` usando a Engine.
- `dotnet build` / `dotnet test`

### Etapa 4 — Runner CLI síncrono
- Implementar CLI conforme `specs/04-execution/runner-cli-spec.md`.
- Implementar exit codes conforme `specs/04-execution/exit-codes.md`.
- Implementar pipeline conforme `specs/04-execution/pipeline-spec.md` + `specs/02-domain/source-request.md`.
- Implementar outputs local/blob conforme `specs/06-storage/*`.
- Implementar logs Serilog JSONL blob conforme `specs/07-observability/*`.
- `dotnet build` / `dotnet test`

### Etapa 5 — Contratos “lite”
- Implementar testes unitários para validar OpenAPI + JSON Schemas conforme `specs/09-testing/contract-test-strategy-lite.md`.
- `dotnet test`

## Critérios de conclusão
- Build/Test ok
- Compatível com specs
- Runner gera `executionId` e respeita exit codes/layout/retention
- Logs JSONL em blob com campos obrigatórios

