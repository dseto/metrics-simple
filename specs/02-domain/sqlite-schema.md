# SQLite Schema (mínimo)
- Process(id PK, ... outputDestinationsJson)
- ProcessVersion(PK processId+version, sourceRequestJson, dsl, outputSchemaJson)
- Connector(id PK, baseUrl, authRef, ...)
