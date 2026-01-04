# SQLite Schema (Config DB)

Data: 2026-01-02

Banco local (SQLite) para armazenar Connectors, Processes e ProcessVersions.

> Observação: objetos JSON (ex.: `outputSchema`) são persistidos como TEXT (JSON string).

## Tabelas

### connectors
| column | type | notes |
|---|---|---|
| id | TEXT PK | |
| name | TEXT | |
| baseUrl | TEXT | |
| authRef | TEXT | referência a segredo/config (não guardar segredo aqui) |
| timeoutSeconds | INTEGER | |
| enabled | INTEGER | 0/1 |
| createdAt | TEXT | ISO |
| updatedAt | TEXT | ISO |

### processes
| column | type | notes |
|---|---|---|
| id | TEXT PK | |
| name | TEXT | |
| description | TEXT | nullable |
| status | TEXT | Draft/Active/Disabled |
| connectorId | TEXT FK | -> connectors.id |
| tagsJson | TEXT | JSON array (nullable) |
| createdAt | TEXT | ISO |
| updatedAt | TEXT | ISO |

### process_output_destinations
| column | type | notes |
|---|---|---|
| processId | TEXT FK | -> processes.id |
| idx | INTEGER | 0..n-1 |
| type | TEXT | LocalFileSystem / AzureBlobStorage |
| configJson | TEXT | JSON (ex.: local/basePath ou blob/container/...) |

PK: (processId, idx)

### process_versions
| column | type | notes |
|---|---|---|
| processId | TEXT FK | |
| version | INTEGER | 1..n |
| enabled | INTEGER | |
| method | TEXT | GET/POST/PUT/DELETE |
| path | TEXT | |
| headersJson | TEXT | JSON map (nullable) |
| queryParamsJson | TEXT | JSON map (nullable) |
| dslProfile | TEXT | jsonata/jmespath/custom |
| dslText | TEXT | |
| outputSchemaJson | TEXT | JSON Schema (TEXT) |
| sampleInputJson | TEXT | JSON sample (nullable) |
| createdAt | TEXT | ISO |
| updatedAt | TEXT | ISO |

PK: (processId, version)

### connector_tokens
| column | type | notes |
|---|---|---|
| connectorId | TEXT PK | -> connectors.id (ON DELETE CASCADE) |
| encVersion | INTEGER | 1 |
| encAlg | TEXT | AES-256-GCM |
| encNonce | TEXT | base64 |
| encCiphertext | TEXT | base64 (ciphertext + tag) |
| createdAt | TEXT | ISO |
| updatedAt | TEXT | ISO |

Nota: tokens **não** são armazenados em plaintext. Criptografia em repouso com chave fora do DB (ex.: `METRICS_SECRET_KEY`).

