
# UI Routes & State Machines — Material 3

Data: 2026-01-01

Este documento define rotas, estado por página e transições (máquina de estados) para reduzir ambiguidade na implementação.

---

## Convenções

### Estado comum (PageState)
```ts
type PageState =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'ready'; dirty: boolean }
  | { kind: 'saving'; dirty: boolean }
  | { kind: 'deleting'; dirty: boolean }
  | { kind: 'error'; dirty: boolean; error: UiError }
  | { kind: 'navigating'; dirty: boolean };
```

### Regras globais
- Se `dirty=true` e o usuário tenta navegar para fora:
  - mostrar dialog: **Discard changes?**
  - ações: **Stay** / **Discard**
- Em `saving` e `deleting`:
  - desabilitar inputs
  - mostrar progress no botão principal

### Feedback padrão
- Save sucesso: Snackbar "Saved"
- Delete sucesso: Snackbar "Deleted"
- Erro: ErrorBanner com Retry (quando aplicável)

---

## Rotas

| Route | Page | Fonte de dados | Ação principal |
|------|------|-----------------|----------------|
| /dashboard | Dashboard | GET /api/processes | navegar |
| /processes | ProcessesList | GET /api/processes | Create |
| /processes/:processId | ProcessEditor | GET/PUT/DELETE /api/processes/{id} | Save |
| /processes/:processId/versions/:version | VersionEditor | GET/PUT /api/processes/{id}/versions/{v} | Save |
| /connectors | Connectors | GET/POST /api/connectors | Create |
| /preview | PreviewTransform | POST /api/preview/transform | Run Preview |
| /runner | RunnerHelp | static | copiar comandos |

> Observação: criação de version pode ser feita via dialog no ProcessEditor e depois navegar para VersionEditor.

---

## Máquinas de estados por página

### A) Dashboard
**Estados:** loading → ready | error

**Transitions**
- `onEnter` => loading, chama GET /processes
- `onSuccess` => ready(dirty=false)
- `onFailure` => error(dirty=false)
- `onRetry` (error) => loading

---

### B) ProcessesList
**Estados:** loading → ready | error

**Transitions**
- `onEnter` => loading, GET /processes
- `onFilterChange` => ready (client-side filter; não refetch)
- `onDeleteClick` => dialog confirm
- `onDeleteConfirm` => deleting(dirty=false), DELETE /processes/{id}
- `onDeleteSuccess` => loading (refetch) + snackbar
- `onDeleteFailure` => error(dirty=false)

---

### C) ProcessEditor
**Estados:** loading → ready(dirty?) → saving → ready | error ; delete path

**Transitions**
- `onEnter(processId)`:
  - se processId == 'new': ready(dirty=true) com modelo default
  - senão: loading e GET /processes/{id}
- `onFieldChange` => ready(dirty=true)
- `onSave`:
  - se mode=create => saving, POST /processes
  - se mode=edit => saving, PUT /processes/{id}
- `onSaveSuccess`:
  - ready(dirty=false) + snackbar
  - se create: navegar para /processes/:id
- `onSaveFailure` => error(dirty=true, error)
- `onDeleteClick` => dialog confirm
- `onDeleteConfirm` => deleting(dirty=false), DELETE /processes/{id}
- `onDeleteSuccess` => navigating(dirty=false) → /processes + snackbar
- `onDeleteFailure` => error(dirty=false)

**Guards**
- Não permitir Save se validação client falhar (ver `ui-api-client-contract.md`).

---

### D) VersionEditor
**Estados:** loading → ready(dirty?) → saving → ready | error

**Transitions**
- `onEnter(processId, version)`:
  - se version == 'new': ready(dirty=true) com defaults
  - senão: loading + GET /versions/{version}
- `onFieldChange` => ready(dirty=true)
- `onSave`:
  - create => POST /processes/{id}/versions
  - edit => PUT /processes/{id}/versions/{version}
- `onSaveSuccess` => ready(dirty=false) + snackbar (e navegar se create)
- `onSaveFailure` => error(dirty=true)
- `onOpenPreview` => navigating (se dirty, dialog) e navegar para /preview com state/prefill

---

### E) Connectors
**Estados:** loading → ready | error ; create dialog has own mini-state

**Transitions**
- `onEnter` => loading + GET /connectors
- `onCreateOpen` => open dialog (idle)
- `onCreateSubmit` => saving (dialog) + POST /connectors
- `onCreateSuccess` => close dialog + snackbar + loading (refetch)
- `onCreateFailure` => error (dialog inline)

---

### F) PreviewTransform
**Estados:** ready(dirty?) → running → ready ; error

**Transitions**
- `onEnter` => ready(dirty=false) (prefill optional)
- `onFieldChange` => ready(dirty=true)
- `onRunPreview`:
  - guard: JSON válido (schema e input) e DSL não vazio
  - running: POST /preview/transform
- `onRunSuccess` => ready(dirty=false) com result
- `onRunFailure` => error(dirty=true, error)

---

## Default Models (criação)

### Process defaults
- status: Draft
- outputDestinations: [LocalFileSystem] com basePath vazio (obrigar preenchimento)

### Version defaults
- enabled: true
- sourceRequest: method GET, path vazio
- dsl.profile: jsonata
- dsl.text vazio
- outputSchema: {} (mas validação exige schema coerente antes de Save)
