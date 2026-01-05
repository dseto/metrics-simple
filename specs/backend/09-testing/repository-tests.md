# Repository Tests (SQLite)

Data: 2026-01-05

Casos mínimos obrigatórios para garantir o storage e as regras de domínio.

## Migrations
- Aplicar `001_auth_users.sql`, `002_connector_tokens.sql` e `003_connector_flex_and_process_body.sql` em banco vazio.
- Validar que as tabelas existem:
  - connectors (sem coluna authRef)
  - connector_secrets
  - (opcional) connector_tokens (LEGACY)
  - process_versions com bodyText e contentType

## Connector Secrets (connector_secrets)
- Inserir secretKind=BEARER_TOKEN e recuperar apenas flags (nunca retornar plaintext).
- Atualizar secret (upsert) e validar updatedAt.
- Remover secret (delete) e validar que has* fica false.
- Cascade: ao remover connector, secrets associados são removidos.

## Connector Delete rule (domain)
- Quando um connector está referenciado por qualquer process/version, o serviço deve bloquear DELETE e retornar erro 409 na API.
