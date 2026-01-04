# Page — Connectors

    Data: 2026-01-01

    ## Route
    - `/connectors`

    ## Purpose
    - CRUD de Connectors (baseUrl, authRef, timeout, apiToken).
    - Observação: o token não é exibido após salvar; apenas um indicador "Token configurado".

## Referência visual
- `visual/mockups/A_UI_design_mockup_in_Material_Design_3_(Material_.png`
## Layout (responsive)
- Top bar com 'New connector'.
- Tabela com ações edit/delete.
- < md: lista compacta.
## Components
- MsConnectorTable / MsConnectorListCompact
- MsConnectorEditorDialog
- MsErrorBanner

## Data binding (container -> components)
- `connectorsState.items -> MsConnectorTable.rows`
- `connectorsState.loading -> MsConnectorTable.loading`
- `connectorsState.error -> MsErrorBanner.error`
## Actions
- New/Edit abre dialog; Save chama API; snackbar
- Delete -> confirm dialog

## A11y
- Dialog com focus trap; first focus no campo Name.
