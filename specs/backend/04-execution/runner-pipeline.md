
# Runner Pipeline (Synchronous)

Data: 2026-01-01

Execução síncrona acionada por CLI (sem filas).

## Entradas
- Config local: SQLite + appsettings.json
- CLI: processId, version (opcional), overrides de destino

## Saídas
- CSV local e/ou Blob
- Logs Serilog JSONL (Blob)

## Pipeline
1) InitExecution: gerar executionId; log ExecutionStarted
2) LoadProcessConfig: carregar Process/Connector/Version; validar status=Active
3) FetchSource: chamar endpoint origem (timeout); aplicar headers/query
4) Transform: aplicar DSL e gerar JSON
5) ValidateOutputSchema: validar JSON vs outputSchema
6) GenerateCsv: CSV determinístico
7) StoreCsv: salvar local + opcional blob
8) FinalizeExecution: log ExecutionCompleted; exit code 0

## Precedência de headers/query
(baixa -> alta)
1) Connector defaults
2) sourceRequest (version)
3) CLI overrides
Em conflito: sobrescreve. Keys: case-insensitive.

## Falhas
- Sempre log ExecutionFailed com step e errorCode
- Retornar exit code != 0 conforme `cli-contract.md`
