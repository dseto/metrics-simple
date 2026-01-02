# Shared constants

## ID patterns
- `id`: `^[a-z0-9][a-z0-9\-]{2,63}$`

## Process.status
- `Draft`
- `Active`
- `Disabled`

## OutputDestination.type
- `LocalFileSystem`
- `AzureBlobStorage`

## ProcessVersion.dsl.profile
- `jsonata` (mínimo obrigatório)
- `jmespath` (opcional)
- `custom` (reservado)

## Error codes (canonical, high-level)
- `VALIDATION_FAILED`
- `NOT_FOUND`
- `CONFLICT`
- `INTERNAL_ERROR`
