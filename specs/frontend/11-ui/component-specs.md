
# Component Specs (Implementável) — Material Design 3
## A11y (implementável)
- Requisitos gerais: `../a11y/a11y-requirements.md`
- Cada componente abaixo inclui uma subseção **Accessibility** quando aplicável.


Data: 2026-01-01

Este documento define **contratos de componentes** (props/events/estados/validações) para permitir geração de UI de forma estável.
Ele complementa:
- `ui-field-catalog.md` (catálogo de campos por tela)
- `specs/11-ui/components.md` (inventário)
- `specs/11-ui/pages/*` (páginas)
- `specs/11-ui/data-contract-mapping.md` (API)

## Convenções
### Naming
- Componentes: `Ms*` (Metrics Simple), ex.: `MsPageHeader`, `MsProcessTable`
- Eventos: `on<Action>` (ex.: `onSave`, `onDelete`)
- Props boolean: `is*`, `has*`, `can*`

### Estado padrão
Todo componente “data-driven” deve suportar:
- `loading: boolean`
- `error: UiError | null`
- `empty: boolean` (quando aplicável)
- `disabled: boolean` (quando aplicável)

### UiError
```ts
type UiError = {
  title: string;           // curto, ex.: "Falha ao salvar"
  message: string;         // explicação legível
  code?: string;           // opcional (HTTP_500, NETWORK_TIMEOUT, etc)
  details?: string;        // texto para copiar (stack/response)
  requestId?: string;      // se API retornar
  canRetry?: boolean;      // default true
};
```

### JSON Editors
Como não usamos Monaco:
- Textarea monospace + botões: **Format**, **Validate**, **Copy**
- O parse/validate ocorre no client antes de chamar API.

### KV Editor (headers/query)
- UI: tabela com linhas editáveis (key/value)
- Normalização:
  - remover linhas com key vazia
  - keys são `trim()`
  - não permitir duplicatas (client-side)

---

## 1) Shell / Layout

### 1.1 MsAppShell
**Responsabilidade:** layout global (drawer + top bar + container).

**Props**
- `title: string`
- `navItems: NavItem[]`
- `activeRoute: string`
- `userBadgeText?: string` (opcional)

**Types**
```ts
type NavItem = {
  id: 'dashboard'|'processes'|'connectors'|'preview'|'runner';
  label: string;
  icon: string;        // Material Symbols name
  route: string;
};
```

**Events**
- `onNavigate(route: string)`

**UX**
- Drawer permanente (>= 1024px), modal (< 1024px)

---

### 1.2 MsPageHeader
**Props**
- `title: string`
- `subtitle?: string`
- `breadcrumbs?: BreadcrumbItem[]`
- `primaryAction?: ActionButton`
- `secondaryActions?: ActionButton[]`
- `dirty?: boolean` (mostra “• Unsaved changes”)

**Types**
```ts
type BreadcrumbItem = { label: string; route?: string };
type ActionButton = {
  id: string;
  label: string;
  icon?: string;
  variant: 'filled'|'tonal'|'outlined'|'text';
  disabled?: boolean;
};
```

**Events**
- `onAction(id: string)`

---

## 2) Data / Lists

### 2.1 MsStatusChip
**Props**
- `status: 'Draft'|'Active'|'Disabled'`
- `size?: 'sm'|'md'` (default md)

**Rendering**
- Usa Material Chip com cor tonal consistente (não hardcode de cor: usar theme roles)

---

### 2.2 MsProcessTable
**Responsabilidade:** listar processes com ações.

**Props**
- `rows: ProcessRow[]`
- `loading: boolean`
- `error: UiError | null`
- `filter: { query: string; status?: 'Draft'|'Active'|'Disabled'|'All' }`
- `canDelete?: boolean` (default true)

**Types**
```ts
type ProcessRow = {
  id: string;
  name: string;
  status: 'Draft'|'Active'|'Disabled';
  connectorId: string;
  destinationsSummary: string[]; // ex.: ["Local", "Blob"]
};
```

**Events**
- `onFilterChange(filter)`
- `onOpen(processId: string)`
- `onDelete(processId: string)`

**Empty state**
- quando rows vazio e !loading e !error: mostrar CTA Create

---

### 2.3 MsConnectorTable
**Props**
- `rows: ConnectorRow[]`
- `loading: boolean`
- `error: UiError | null`

**Types**
```ts
type ConnectorRow = {
  id: string;
  name: string;
  baseUrl: string;
  authRef: string;
  timeoutSeconds: number;
};
```

**Events**
- `onCreate()`
- `onOpen?(id)` (opcional)

---

## 3) Forms

### 3.1 MsFormFooter
**Props**
- `primary: ActionButton` (Save)
- `secondary?: ActionButton[]` (Cancel/Delete)
- `sticky?: boolean` (default true)

**Events**
- `onAction(id)`

---

### 3.2 MsProcessForm
**Responsabilidade:** editar Process.

**Props**
- `value: ProcessDto`
- `connectors: { id: string; name: string }[]`
- `loading: boolean`
- `error: UiError | null`
- `mode: 'create'|'edit'`
- `disabled?: boolean`

**Types (derivado do OpenAPI)**
```ts
type OutputDestination =
  | { type: 'LocalFileSystem'; local: { basePath: string } }
  | { type: 'AzureBlobStorage'; blob: { connectionStringRef: string; container: string; pathPrefix?: string } };

type ProcessDto = {
  id: string;
  name: string;
  status: 'Draft'|'Active'|'Disabled';
  connectorId: string;
  outputDestinations: OutputDestination[];
};
```

**Events**
- `onChange(next: ProcessDto, dirty: boolean)`
- `onSave(value: ProcessDto)`
- `onDelete?(processId: string)` (apenas edit)

**Validações client**
- id obrigatório (create)
- name obrigatório
- connectorId obrigatório
- outputDestinations min 1
- Local: basePath obrigatório
- Blob: connectionStringRef + container obrigatórios

---

### 3.3 MsProcessVersionForm
**Props**
- `value: ProcessVersionDto`
- `mode: 'create'|'edit'`
- `loading: boolean`
- `error: UiError | null`

**Types**
```ts
type SourceRequestDto = {
  method: 'GET'|'POST'|'PUT'|'DELETE';
  path: string;
  headers?: Record<string,string>;
  queryParams?: Record<string,string>;
};

type DslDto = {
  profile: 'jsonata'|'jmespath'|'custom';
  text: string;
};

type ProcessVersionDto = {
  processId: string;
  version: number;
  enabled: boolean;
  sourceRequest: SourceRequestDto;
  dsl: DslDto;
  outputSchema: any;   // JSON
  sampleInput?: any;   // JSON
};
```

**Events**
- `onChange(next: ProcessVersionDto, dirty: boolean)`
- `onSave(value: ProcessVersionDto)`
- `onOpenPreview(prefill: PreviewPrefill)`

**Validações client**
- version obrigatório (create)
- sourceRequest.method obrigatório
- sourceRequest.path obrigatório
- dsl.profile obrigatório
- dsl.text obrigatório
- outputSchema: JSON válido (parse)
- sampleInput: se preenchido, JSON válido

---

### 3.4 MsKeyValueEditor
**Props**
- `title: string`
- `value: { key: string; value: string }[]`
- `placeholderKey?: string`
- `placeholderValue?: string`
- `disabled?: boolean`

**Events**
- `onChange(next: {key,value}[])`

**Regras**
- não permitir key duplicada (case-insensitive) — mostrar erro inline
- remover entradas com key vazia ao salvar

---

### 3.5 MsJsonEditorLite
**Props**
- `title: string`
- `valueText: string` (texto bruto)
- `mode: 'json'|'text'` (default json)
- `readOnly?: boolean`
- `height?: number` (px)

**Events**
- `onChange(valueText: string)`
- `onValidJson?(valueObj: any)` (quando parse ok)

**Ações internas**
- `Format` (prettify)
- `Validate` (parse)
- `Copy`

---

## 4) Preview

### 4.1 MsPreviewPanel
**Props**
- `dsl: DslDto`
- `outputSchemaText: string` (json)
- `sampleInputText: string` (json)
- `loading: boolean`
- `result?: PreviewResult`
- `error: UiError | null`

**Types**
```ts
type PreviewResult = {
  isValid: boolean;
  errors: { path: string; message: string; kind?: string | null }[];
  output: any | null;
  previewCsv?: string | null;
};
type PreviewPrefill = {
  dslProfile: 'jsonata'|'jmespath'|'custom';
  dslText: string;
  outputSchemaText: string;
  sampleInputText: string;
};
```

**Events**
- `onRun(request: { dsl; outputSchemaObj; sampleInputObj })`
- `onCopy(kind: 'json'|'csv')`

**Validações**
- Se JSON inválido -> não chama API, mostra erro inline

---

## 5) Feedback

### 5.1 MsErrorBanner
**Props**
- `error: UiError`
- `visible: boolean`

**Events**
- `onRetry?()`
- `onCopyDetails()`
- `onDismiss()`

---

### 5.2 MsSnackbar
**Props**
- `message: string`
- `actionLabel?: string`
- `durationMs?: number` (default 4000)

**Events**
- `onAction?()`
- `onDismiss()`

---

## 6) Page Contracts (implementável)

Cada página deve ser implementada como “container” que:
- carrega dados (API)
- compõe componentes acima
- faz mapeamentos DTO ↔ UI
- controla loading/error/dirty

### 6.1 DashboardContainer
**Data**
- GET /api/processes

**Derived**
- totals: total/active/disabled
- recent: top 10 por ordem alfabética (ou por updatedAt quando existir)

### 6.2 ProcessesContainer
- GET /api/processes
- Delete: DELETE /api/processes/{id}

### 6.3 ProcessEditorContainer
- create: POST /api/processes
- edit: GET+PUT /api/processes/{id}
- versions list: (opcional) se API não expuser list, pode ser “known versions” via chamadas diretas quando houver

### 6.4 VersionEditorContainer
- create: POST /api/processes/{id}/versions
- edit: GET+PUT /api/processes/{id}/versions/{version}

### 6.5 PreviewContainer
- POST /api/preview/transform

### 6.6 ConnectorsContainer
- list: GET /api/connectors
- create: POST /api/connectors

---

## 7) UI Contract Tests (unit)
Recomendado criar testes unitários (não E2E) para:
- validação de JSON editor (parse/format)
- validação de KV editor (dedupe)
- mapeamento DTO ↔ form state
- chamadas API mockadas (Preview)


### AiAssistantPanel
- Inputs: goalText, sampleInputText, dslProfile, hints, existingDsl, existingOutputSchema
- Actions: Generate (calls API), Apply (emit suggested dsl+schema)
- States: idle/loading/success/error/disabled
- Uses Material 3: filled text fields, tonal button, progress indicator, banners.
