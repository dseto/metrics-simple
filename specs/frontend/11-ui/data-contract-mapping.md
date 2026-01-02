
# UI ↔ API Contract Mapping

Base:
- `specs/03-interfaces/openapi-config-api.yaml`
- `specs/02-domain/schemas/*.schema.json`

## Process
- GET/POST /api/processes
- GET/PUT/DELETE /api/processes/{id}

Campos UI: id, name, status, connectorId, outputDestinations.

## ProcessVersion
- POST /api/processes/{id}/versions
- GET/PUT /api/processes/{id}/versions/{version}

Campos: enabled, sourceRequest, dsl, outputSchema, sampleInput.

## Connector
- GET/POST /api/connectors

Campos: id, name, baseUrl, authRef, timeoutSeconds.

## Preview
- POST /api/preview/transform
