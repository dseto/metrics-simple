# UI API Client Contract — DTOs, Normalização e Regras

Data: 2026-01-02

Objetivo: especificar o client HTTP da UI (tipos, normalização, erros) para reduzir variação na implementação.

---

## Base URL e headers
- Base URL: `/api`
- Header padrão: `Content-Type: application/json`
- Header opcional: `X-Correlation-Id` (propagar para troubleshooting)

---

## Tipos (DTOs)
> Derivados do OpenAPI em `specs/shared/openapi/config-api.yaml`.

### ProcessDto
```ts
type OutputDestination =
  | { type: 'LocalFileSystem'; local: { basePath: string } }
  | { type: 'AzureBlobStorage'; blob: { connectionStringRef: string; container: string; pathPrefix?: string } };

type ProcessDto = {
  id: string;
  name: string;
  description?: string | null;
  status: 'Draft'|'Active'|'Disabled';
  connectorId: string;
  tags?: string[] | null;
  outputDestinations: OutputDestination[];
};
```

### ProcessVersionDto
```ts
type SourceRequestDto = {
  method: 'GET'|'POST'|'PUT'|'DELETE';
  path: string;
  headers?: Record<string,string> | null;
  queryParams?: Record<string,string> | null;
};

type DslDto = {
  profile: 'jsonata'|'jmespath'|'custom';
  text: string;
};

type ProcessVersionDto = {
  processId: string;
  version: number;     // 1..n
  enabled: boolean;
  sourceRequest: SourceRequestDto;
  dsl: DslDto;
  outputSchema: any;   // JSON object (JSON Schema)
  sampleInput?: any;   // JSON sample (optional)
};
```

### ConnectorDto
```ts
type ConnectorDto = {
  id: string;
  name: string;
  baseUrl: string;
  authRef: string;
  timeoutSeconds: number;
  enabled?: boolean;
};
```

### PreviewTransform
```ts
type PreviewTransformRequest = {
  dsl: DslDto;
  outputSchema: any;
  sampleInput: any;
};
type ValidationErrorItem = { path: string; message: string; kind?: string | null };

type PreviewTransformResponse = {
  isValid: boolean;
  errors: ValidationErrorItem[];
  output: any | null;
  previewCsv?: string | null;
};
```

### ApiError
```ts
type ApiErrorDetail = { path: string; message: string };
type ApiError = {
  code: string;
  message: string;
  details?: ApiErrorDetail[] | null;
  correlationId: string;
  executionId?: string | null;
};
```

---

## Endpoints (client)

### ProcessesClient
- `list()` -> GET `/processes`
- `create(dto)` -> POST `/processes`
- `get(id)` -> GET `/processes/{id}`
- `update(id, dto)` -> PUT `/processes/{id}`
- `delete(id)` -> DELETE `/processes/{id}`

### ProcessVersionsClient
- `create(processId, dto)` -> POST `/processes/{id}/versions`
- `get(processId, version)` -> GET `/processes/{id}/versions/{version}`
- `update(processId, version, dto)` -> PUT `/processes/{id}/versions/{version}`

### ConnectorsClient
- `list()` -> GET `/connectors`
- `create(dto)` -> POST `/connectors`

### PreviewClient
- `transform(req)` -> POST `/preview/transform`

### AiClient (design-time)
- `generate(req)` -> POST `/ai/dsl/generate`

---

## Normalização antes de enviar (client-side)

### Strings
Aplicar `trim()` em:
- `id`, `name`, `connectorId`, `baseUrl`, `authRef`, `path`

Rejeitar string vazia após trim para campos obrigatórios.

### sampleInput
- A UI pode manter `sampleInputText` como texto (textarea).
- Antes de chamar `preview/transform` ou `ai/dsl/generate`: `JSON.parse(sampleInputText)` e enviar como objeto.

### OutputDestinations
- Bloquear Save se:
  - `LocalFileSystem.local.basePath` vazio
  - `AzureBlobStorage.blob.connectionStringRef` vazio
  - `AzureBlobStorage.blob.container` vazio
