# Page — Process Editor

    Data: 2026-01-01

    ## Route
    - `/processes/:id (edit) e /processes/new (create)`

    ## Purpose
    - Criar/editar Process (metadata, destinos, status).

## Referência visual
- `visual/mockups/A_UI_mockup_features_three_main_screens_of_an_inte.png`
## Layout (responsive)
- Layout 2-col (>= lg):
-   - esquerda (8/12): form principal
-   - direita (4/12): painel 'Destinations' + 'Versions'
- < lg: vira 1 coluna com seções.
## Components composition
- MsAppShell
  - MsPageHeader
    - title (Create/Edit)
    - actions: Save, Delete (edit only)
    - dirty indicator
  - MsProcessForm
  - MsDestinationsPanel
  - MsVersionsPanel
  - MsErrorBanner (page-level)

## Data binding (container -> components)
- `processEditorState.model -> MsProcessForm.model`
- `processEditorState.validationErrors -> MsProcessForm.errors`
- `processEditorState.destinations -> MsDestinationsPanel.items`
- `processEditorState.versions -> MsVersionsPanel.items`
- `processEditorState.saving -> SaveButton.loading/disabled`
## Validation
- Client-side: required fields + basic format (antes de chamar API).
- Server-side errors (400/422/409): mapear para fields quando possível; senão banner.

## Actions
- Save:
  - validate -> call POST/PUT
  - on success snackbar
  - on error: follow states-and-feedback matrix
- Delete:
  - confirm dialog
  - on success navigate back to list

## A11y notes
- Botão Save tem aria-label 'Save process'.
- Erros de campo devem usar aria-describedby apontando para helper/error text.
- Delete dialog: foco inicial em Cancel.
