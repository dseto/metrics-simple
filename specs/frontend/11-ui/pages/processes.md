# Page — Processes

    Data: 2026-01-01

    ## Route
    - `/processes`

    ## Purpose
    - Listagem, busca e ações rápidas (create/edit/delete).

## Referência visual
- `visual/mockups/A_set_of_six_high-fidelity_UI_wireframe_mockups_di.png`
## Layout (responsive)
- Top actions bar com Search + 'New process'.
- Tabela ocupa toda largura com sticky header.
- Responsivo: em < md, tabela vira lista (cards) via MsProcessListCompact.
## Components composition
- MsAppShell
  - MsSectionHeader(title='Processes')
  - MsToolbarRow
    - MsSearchField
    - MsPrimaryButton('New process')
  - MsProcessTable (>= md) / MsProcessListCompact (< md)
  - MsErrorBanner (conditional)

## Data binding (container -> components)
- `processesState.query -> MsSearchField.value`
- `processesState.items -> MsProcessTable.rows`
- `processesState.loading -> MsProcessTable.loading`
- `processesState.error -> MsErrorBanner.error`
## States
- `loading`: skeleton
- `ready`: render normal
- `empty`: empty-state (quando lista vazia)
- `error`: error banner (quando falha global)
## Actions
- New process -> `/processes/new`
- Row click -> `/processes/{id}`
- Delete -> confirm dialog -> call API -> snackbar success/error

## Error UX
- Falha de listagem (GET): usar MsErrorBanner + Retry
- Falha em delete: MsSnackbar + Retry
