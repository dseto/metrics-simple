### connectors
| column | type | notes |
|---|---|---|
| id | TEXT PK | |
| name | TEXT | |
| baseUrl | TEXT | |
| authType | TEXT | NONE/BEARER/API_KEY/BASIC |
| authConfigJson | TEXT | JSON não-secreto (API_KEY: name/location; BASIC: username) |
| requestDefaultsJson | TEXT | JSON (method/headers/query/body/contentType) |
| timeoutSeconds | INTEGER | |
| enabled | INTEGER | 0/1 |
| createdAt | TEXT | ISO |
| updatedAt | TEXT | ISO |

### connector_secrets
Tabela N:1 (connectorId, secretKind) para armazenar **secrets criptografados** (writeOnly na API).

| column | type | notes |
|---|---|---|
| connectorId | TEXT FK | -> connectors.id |
| secretKind | TEXT | BEARER_TOKEN / API_KEY_VALUE / BASIC_PASSWORD |
| encVersion | INTEGER | |
| encAlg | TEXT | Ex.: AES-256-GCM |
| encNonce | TEXT | base64 |
| encCiphertext | TEXT | base64 |
| createdAt | TEXT | ISO |
| updatedAt | TEXT | ISO |

PK: (connectorId, secretKind)

### connector_tokens (LEGACY)
Tabela 1:1 (connectorId) para armazenar **token bearer criptografado** (compatibilidade).  
A partir da versão 1.2.0 o storage padrão é `connector_secrets`.

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
| bodyText | TEXT | Body (string ou JSON serializado) (nullable) |
| contentType | TEXT | Content-Type override (nullable) |
| dslProfile | TEXT | jsonata/jmespath/custom |
| dslText | TEXT | |
| outputSchemaJson | TEXT | JSON Schema (TEXT) |
| sampleInputJson | TEXT | JSON sample (nullable) |
| createdAt | TEXT | ISO |
| updatedAt | TEXT | ISO |

PK: (processId, version)
