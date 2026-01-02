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
- 40 SOURCE_ERROR
- 50 TRANSFORM_ERROR
- 60 STORAGE_ERROR
- 70 UNEXPECTED_ERROR

## Seleção de version
- Se `--version`: usar exatamente aquela versão (inteiro).
- Senão: selecionar a **maior versão numérica** (`max(version)`) com `enabled=true`.

## Configuração para execução e testes

Para suportar execução determinística e **integration tests**, o Runner deve respeitar:

- `METRICS_SQLITE_PATH`: caminho do arquivo SQLite.
- `METRICS_SECRET__<authRef>`: segredo para resolver `connector.authRef`.

Essas chaves são normativas para a suíte `Integration.Tests`.
