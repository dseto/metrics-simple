# Runner CLI Contract

Data: 2026-01-02

## Comandos
- `run`
- `validate`
- `cleanup`

### run
```
runner.exe run --processId <id> [--version <n>] [--localBasePath <path>] [--blob on|off]
```

### validate
Valida:
- existência Process/Connector/Version
- `Process.status == Active` e `ProcessVersion.enabled == true`
- `outputSchema` válido (JSON Schema)
- destinos (`outputDestinations`) válidos

### cleanup
Remove arquivos antigos conforme retenção.
- `--retentionDays <n>` (default sugerido: 30)

## Exit codes
- 0 OK
- 10 VALIDATION_ERROR
- 20 NOT_FOUND
- 30 DISABLED
- 40 SOURCE_ERROR (ex.: falha ao buscar fonte externa, timeout, HTTP 5xx, ou **falha ao decriptografar connector apiToken**)
- 50 TRANSFORM_ERROR
- 60 STORAGE_ERROR
- 70 UNEXPECTED_ERROR

## Seleção de version
- Se `--version`: usar exatamente aquela versão (inteiro).
- Senão: selecionar a **maior versão numérica** (`max(version)`) com `enabled=true`.

## Configuração para execução e testes

Para suportar execução determinística e **integration tests**, o Runner deve respeitar:

- `METRICS_SQLITE_PATH`: caminho do arquivo SQLite.
- `METRICS_SECRET_KEY`: chave **base64 (32 bytes)** usada para criptografar/decriptografar `connector.apiToken` (AES-256-GCM). **Fail-fast** se ausente/ inválida.

Essas chaves são normativas para a suíte `Integration.Tests`.

---

## Delta 1.2.0 — Secrets e Connector Flex

- `REMOVIDO_authRef` foi removido.
- Secrets são persistidos no SQLite, criptografados com `METRICS_SECRET_KEY`.
- Storage recomendado: tabela `connector_secrets` (multi-secret).
- A tabela `connector_tokens` permanece como LEGACY para compatibilidade/migração.

Runner:
- Deve suportar `authType`: NONE, BEARER, API_KEY, BASIC.
- Deve suportar merge de `connector.requestDefaults` + `processVersion.sourceRequest`.
