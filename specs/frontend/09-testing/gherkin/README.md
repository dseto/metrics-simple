# Gherkin (E2E) - Frontend

Este diretório contém cenários E2E em Gherkin atualizados para refletir as mudanças de segurança (JWT, roles e redirects por 401) e o contrato canônico de rotas.

## Regras

- Core versionado: todos os endpoints de negócio usam `/api/v1/*`.
- Exceções sem versionamento (aceitas): `/api/auth/*`, `/api/admin/auth/*`, `/api/health`.

## Arquivos

- `01-auth.feature`: login, acesso sem sessão e sessão expirada.
- `02-authorization.feature`: bloqueio por role e acesso Admin.
- `03-connectors.feature`: CRUD básico de conectores.
- `04-processes.feature`: CRUD básico de processos.
- `05-process-versions.feature`: create/update de versões de processo.
- `06-preview-and-ai.feature`: preview e geração de DSL.
