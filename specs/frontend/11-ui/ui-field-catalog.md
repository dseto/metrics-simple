# UI Field Catalog (Determinístico)

Data: 2026-01-01

Este documento lista **cada campo por tela** (path, tipo, validação, fonte, mensagens de erro e ids estáveis),
para reduzir variações na implementação do frontend.

Ele complementa:
- `pages/*.md` (composição e binding)
- `component-specs.md` (contratos de componentes)
- `states-and-feedback.md` (UX de estados/erros)
- `ui-api-client-contract.md` (normalização e erros)

---

## Convenções

### 1) `fieldId` e `data-testid`
- Todo campo deve ter:
  - `fieldId` (string estável)
  - `data-testid` igual ao `fieldId`
- Padrão: `<page>.<section>.<field>`
  - Ex.: `processEditor.form.name`

### 2) `path`
- `path` refere-se ao DTO enviado para API (quando aplicável).
- Para campos somente-UI (ex.: busca), o path começa com `ui.`

### 3) Validação
- **Client-side** deve validar os itens marcados como “Obrigatório” antes de chamar API.
- Para JSON editors:
  - validar parse JSON (erro “JSON inválido”)
- Para KV editor:
  - não permitir chaves duplicadas (case-insensitive)

### 4) Mensagens padrão
Use exatamente estes textos (evita variação de copy):
- **Obrigatório:** `Campo obrigatório.`
- **JSON inválido:** `JSON inválido. Verifique a sintaxe.`
- **URL inválida:** `URL inválida.`
- **Número inválido:** `Valor inválido.`
- **Mínimo 1 item:** `Adicione pelo menos um item.`
- **Timeout mínimo:** `O timeout deve ser maior ou igual a 1.`

### 5) A11y (mínimo)
- `aria-label` obrigatório para JsonEditor/KVEditor.
- Erros devem ser expostos por `aria-describedby` com id: `<fieldId>.error`.

---

## Tela: Processes (Lista)
Route: `/processes`

| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `processes.toolbar.search` | `ui.processes.query` | string | `MatInput` + `MsSearchField` | não | trim; max 200 | `processesState.query` | — |

**Ações (não-campo)**
- `processes.toolbar.new` (CTA): navega para `/processes/new`

---

## Tela: Process Editor
Route: `/processes/new` (create) e `/processes/:id` (edit)

### Seção: Identificação
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `processEditor.form.id` | `Process.id` | string | `MatInput` | sim (create) | trim; regex: `^[a-z0-9-]+$`; min 3; max 64 | `processEditorState.model.id` | ID inválido. (apenas a-z, 0-9, -) |
| `processEditor.form.name` | `Process.name` | string | `MatInput` | sim | trim; min 3; max 128 | `processEditorState.model.name` | Mín. 3, Máx. 128 caracteres. |
| `processEditor.form.status` | `Process.status` | enum (`Draft|Active|Disabled`) | `MatSelect` | sim | default: `Draft` (create) | `processEditorState.model.status` | Campo obrigatório. |
| `processEditor.form.connectorId` | `Process.connectorId` | string (lookup) | `MatSelect` (options=connectors) | sim | deve escolher 1 option | `processEditorState.model.connectorId` | Campo obrigatório. |

### Seção: Destinos de saída (`outputDestinations[]`)
Regra: o Process deve ter **≥ 1** destino.

| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `processEditor.destinations.add` | `ui.outputDestinations.add` | action | `MatButton` | — | — | UI | — |
| `processEditor.destinations[i].type` | `Process.outputDestinations[i].type` | enum (`LocalFileSystem|AzureBlobStorage`) | `MatSelect` | sim | — | `model.outputDestinations[i]` | Campo obrigatório. |

#### Destino: LocalFileSystem
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `processEditor.destinations[i].local.basePath` | `Process.outputDestinations[i].local.basePath` | string | `MatInput` | sim (se type=LocalFileSystem) | trim; não permitir vazio | `model.outputDestinations[i].local.basePath` | Campo obrigatório. |

#### Destino: AzureBlobStorage
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `processEditor.destinations[i].blob.connectionStringRef` | `Process.outputDestinations[i].blob.connectionStringRef` | string | `MatInput` | sim (se type=AzureBlobStorage) | trim; não permitir vazio | `model.outputDestinations[i].blob.connectionStringRef` | Campo obrigatório. |
| `processEditor.destinations[i].blob.container` | `Process.outputDestinations[i].blob.container` | string | `MatInput` | sim | trim; não permitir vazio | `model.outputDestinations[i].blob.container` | Campo obrigatório. |
| `processEditor.destinations[i].blob.pathPrefix` | `Process.outputDestinations[i].blob.pathPrefix` | string | `MatInput` | não | trim; se preenchido: não iniciar com `/`; não conter `..` | `model.outputDestinations[i].blob.pathPrefix` | Valor inválido. |

#### Erro de “mínimo 1 destino”
- **fieldId:** `processEditor.destinations`  
- **Regra:** se `outputDestinations.length == 0` → erro
- **Mensagem:** `Adicione pelo menos um item.`  
- **Render:** inline abaixo do painel + `aria-describedby="processEditor.destinations.error"`

### Seção: Ações
- `processEditor.action.save` (button) — desabilitar quando inválido/saving
- `processEditor.action.delete` (button) — apenas edit, exige confirmação

---

## Tela: Version Editor
Route: `/processes/:id/versions/new` e `/processes/:id/versions/:version` (version numérica)

### Seção: Identificação / Controle
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `versionEditor.form.version` | `ProcessVersion.version` | number | `MatInput` (type=number) | sim (create) | inteiro; min 1 | `versionEditorState.model.version` | Informe um número inteiro ≥ 1. |
| `versionEditor.form.enabled` | `ProcessVersion.enabled` | boolean | `MatSlideToggle` | sim | default: true | `model.enabled` | — |

### Seção: Source Request
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `versionEditor.source.method` | `ProcessVersion.sourceRequest.method` | enum (`GET|POST|PUT|DELETE`) | `MatSelect` | sim | default: `GET` | `model.sourceRequest.method` | Campo obrigatório. |
| `versionEditor.source.path` | `ProcessVersion.sourceRequest.path` | string | `MatInput` | sim | trim; não permitir vazio | `model.sourceRequest.path` | Campo obrigatório. |
| `versionEditor.source.headers` | `ProcessVersion.sourceRequest.headers` | kv map | `MsKeyValueEditor` | não | keys únicas (case-insensitive) | `model.sourceRequest.headers` | Valor inválido. |
| `versionEditor.source.queryParams` | `ProcessVersion.sourceRequest.queryParams` | kv map | `MsKeyValueEditor` | não | keys únicas (case-insensitive) | `model.sourceRequest.queryParams` | Valor inválido. |

### Seção: Transform (DSL)
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `versionEditor.dsl.profile` | `ProcessVersion.dsl.profile` | enum (`jsonata|jmespath|custom`) | `MatSelect` | sim | default: `jsonata` | `model.dsl.profile` | Campo obrigatório. |
| `versionEditor.dsl.text` | `ProcessVersion.dsl.text` | text | `MsJsonEditorLite` (plain text mode) | sim | trim; min 1; max 20000 | `model.dsl.text` | Mín. 1, Máx. 20000 caracteres. |

### Seção: Output Schema (JSON)
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `versionEditor.outputSchema` | `ProcessVersion.outputSchema` | json | `MsJsonEditorLite` | sim | parse JSON obrigatório; min 1 propriedade | `model.outputSchema` | JSON inválido. Verifique a sintaxe. |

### Seção: Sample Input (JSON)
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `versionEditor.sampleInput` | `ProcessVersion.sampleInput` | json | `MsJsonEditorLite` | não | se preenchido: parse JSON | `model.sampleInput` | JSON inválido. Verifique a sintaxe. |

### Seção: Preview
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `versionEditor.preview.run` | `ui.preview.run` | action | `MatButton` | — | exige outputSchema válido; se sampleInput vazio, usar default `{{}}` | `previewState` | — |
| `versionEditor.preview.activeTab` | `ui.preview.activeTab` | enum (`Output|CSV|Errors`) | `MatTabs` | — | — | `previewState.activeTab` | — |

**Notas**
- `aria-label` obrigatório nos 3 editores:
  - `versionEditor.dsl.text`
  - `versionEditor.outputSchema`
  - `versionEditor.sampleInput`

---

## Tela: Connectors
Route: `/connectors`

### Dialog: Create/Edit Connector
| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `connectors.dialog.id` | `Connector.id` | string | `MatInput` | sim (create) | trim; regex: `^[a-z0-9-]+$`; min 3; max 64 | `connectorDraft.id` | ID inválido. (apenas a-z, 0-9, -) |
| `connectors.dialog.name` | `Connector.name` | string | `MatInput` | sim | trim; min 3; max 128 | `connectorDraft.name` | Mín. 3, Máx. 128 caracteres. |
| `connectors.dialog.baseUrl` | `Connector.baseUrl` | url | `MatInput` | sim | trim; validar URL (http/https) | `connectorDraft.baseUrl` | URL inválida. |
| `connectors.dialog.authRef` | `Connector.authRef` | string | `MatInput` | sim | trim; não permitir vazio | `connectorDraft.authRef` | Campo obrigatório. |
| `connectors.dialog.timeoutSeconds` | `Connector.timeoutSeconds` | number | `MatInput` (type=number) | sim | inteiro; min 1 | `connectorDraft.timeoutSeconds` | O timeout deve ser maior ou igual a 1. |
| `connectors.dialog.apiToken` | `Connector.apiToken` | string | `MatInput` (type=password) | não | trim; min 1; max 4096 (se preenchido) | `connectorDraft.apiToken` | Mín. 1, Máx. 4096 caracteres. |

---

## Tela: Preview Transform (Workbench) — opcional
Route: `/preview`

| fieldId | path | Tipo UI | Componente | Obrigatório | Validações | Fonte | Mensagem de erro |
|---|---|---|---|---:|---|---|---|
| `preview.inputJson` | `ui.preview.inputJson` | json | `MsJsonEditorLite` | sim | parse JSON obrigatório | `previewContainer.inputJson` | JSON inválido. Verifique a sintaxe. |
| `preview.dsl.profile` | `ui.preview.dsl.profile` | enum | `MatSelect` | sim | default `jsonata` | `previewContainer.dsl.profile` | Campo obrigatório. |
| `preview.dsl.text` | `ui.preview.dsl.text` | text | `MsJsonEditorLite` | sim | trim; min 1; max 20000 | `previewContainer.dsl.text` | Mín. 1, Máx. 20000 caracteres. |
| `preview.outputSchema` | `ui.preview.outputSchema` | json | `MsJsonEditorLite` | sim | parse JSON obrigatório; min 1 propriedade | `previewContainer.outputSchema` | JSON inválido. Verifique a sintaxe. |
| `preview.run` | `ui.preview.run` | action | `MatButton` | — | exige validação OK | `previewContainer` | — |

---

## Referências
- Form validations: `component-specs.md` (MsProcessForm / MsProcessVersionForm)
- Error UX: `states-and-feedback.md`
- A11y: `../a11y/a11y-requirements.md`


## AI Assistant (Process Version Editor)
## AI Assistant (Process Version Editor)

- id/path: `aiAssistant.goalText`
  - tipo: textarea
  - validação: required, minLen=10, maxLen=4000
  - fonte: user input
  - mensagem de erro: "Descreva o CSV desejado (mín. 10 caracteres)."

- id/path: `aiAssistant.sampleInputText`
  - tipo: textarea (code)
  - validação: required, minLen=2, maxLen=500000, mustBeValidJson
  - fonte: user paste
  - mensagem de erro: "Informe um JSON válido (até 500k)."

- id/path: `aiAssistant.dslProfile`
  - tipo: select (`jsonata|jmespath`)
  - validação: required (default `jsonata`)
  - fonte: user selection
  - mensagem de erro: "Campo obrigatório."

- id/path: `aiAssistant.hints.columns`
  - tipo: text
  - validação: optional, maxLen=200
  - fonte: user input
  - mensagem de erro: "Máx. 200 caracteres."

- id/path: `aiAssistant.generate`
  - tipo: button
  - validação: enabled when `goalText` + `sampleInputText` valid
  - fonte: action
  - mensagem de erro: n/a

- id/path: `aiAssistant.result.dsl`
  - tipo: readonly code block
  - fonte: API response

- id/path: `aiAssistant.result.outputSchema`
  - tipo: readonly JSON viewer
  - fonte: API response

> Integração: antes de chamar a API, a UI faz `JSON.parse(sampleInputText)` e envia como `sampleInput` (objeto).