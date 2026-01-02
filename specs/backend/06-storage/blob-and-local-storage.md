
# CSV Storage (Local + Azure Blob)

Data: 2026-01-01

## Local
Path obrigatório:
- basePath/processId/yyyy/MM/dd/executionId.csv

Precedência basePath:
1) Process destination LocalFileSystem.basePath
2) CLI --localBasePath (override)

Retenção:
- cleanup remove arquivos com data < hoje - retentionDays

## Blob (opcional)
- container obrigatório
- pathPrefix opcional

Path sugerido:
- pathPrefix/processId/yyyy/MM/dd/executionId.csv

Segredo:
- connectionStringRef aponta para appsettings.json (não guardar segredo no SQLite)
