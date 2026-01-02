
# States and Feedback (Prescritivo)

Data: 2026-01-01

## Objetivo
Definir regras determinísticas de **loading/empty/error/success** e como apresentar feedback.

> Referência de erros técnicos: `ui-api-client-contract.md` (`UiError`).

---

## Loading
### List pages (Dashboard, Processes, Connectors)
- Mostrar `MsSkeletonList` por **no mínimo 300ms** (evita flicker).
- Desabilitar ações primárias enquanto `loading=true`.
- Se `loading` > 10s: exibir `MsInlineInfo` ("Still working...") + ação "Retry".

### Editors (Process Editor / Version Editor)
- Se carregando dados iniciais: `MsSkeletonForm`.
- Se salvando: bloquear botões, mostrar progress no botão ("Saving...").

---

## Empty state
- Deve usar `MsEmptyState` com:
  - título curto
  - descrição em 1–2 linhas
  - CTA primário (ex.: "Create process")

---

## Success feedback
- Criação/atualização: `MsSnackbar` (3s) com ação "View" quando aplicável.
- Execução Runner disparada via UI (se existir): snackbar + link para logs.

---

## Error UX (matriz determinística)

### Princípios
- **Erro global** (lista/página inteira): `MsErrorBanner`
- **Erro de ação** (save/delete): `MsSnackbar` + "Retry"
- **Erro de campo** (validação): mensagem inline + `aria-describedby`

### Matriz UiError → Render
| Caso | Condição | Render | Ação |
|---|---|---|---|
| Network/timeout | `UiError.code` in (`NETWORK_ERROR`,`TIMEOUT`) | `MsErrorBanner` (lista) / `MsSnackbar` (ação) | "Retry" |
| Not found | `httpStatus=404` | `MsErrorBanner` (página) | "Back to list" |
| Conflict | `httpStatus=409` | `MsSnackbar` (ação) + texto específico | "Resolve" (link para editor) |
| Validation semantic | `httpStatus=422` | `MsErrorBanner` (página) ou inline se mapear campo | "Fix" |
| Bad request | `httpStatus=400` | Inline para campos quando possível; senão banner | - |
| Unexpected | default | `MsErrorBanner` + "Copy details" | "Report" (copia JSON) |

### Texto (copy) — regras
- Não exibir stacktrace.
- Exibir `UiError.title` como headline.
- `UiError.message` como body.
- `UiError.code` só em seção "Details" colapsável.

---

## Confirmações (Dialog)
- Delete sempre exige confirmação:
  - `MsConfirmDialog` com foco inicial em "Cancel"
  - ESC fecha
  - Enter confirma somente se o foco estiver no botão "Delete"

---

## Unsaved changes
- Estado "dirty" exibido no header do editor.
- Ao navegar para fora com dirty:
  - `MsConfirmDialog` ("Discard changes?")
  - Botões: Cancel / Discard
