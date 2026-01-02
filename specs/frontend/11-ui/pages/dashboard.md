# Page — Dashboard

    Data: 2026-01-01

    ## Route
    - `/`

    ## Purpose
    - Visão rápida: processos ativos, últimos runs, atalhos para criação/edição.

## Referência visual
- `visual/mockups/A_UI_design_mockup_in_Material_Design_3_(Material_.png`
## Layout (responsive)
- Header fixo (MsAppShell) + content container com max-width (ver tokens breakpoints).
- Grid 12 col (>= md):
-   - Col 1–8: card 'Recent Runs' (MsRecentRunsList)
-   - Col 9–12: card 'Quick Actions' (MsQuickActions)
- Em telas < md: cards empilhados (1 coluna).
## Components composition (top-down)
- MsAppShell
  - MsTopAppBar
  - MsSideNav
  - PageContent
    - MsSectionHeader (title='Dashboard')
    - MsRecentRunsList
    - MsQuickActions

## Data binding (container -> components)
- `dashboardState.recentRuns[] -> MsRecentRunsList.items`
- `dashboardState.loading -> MsRecentRunsList.loading`
- `dashboardState.error -> MsErrorBanner`
## States
- `loading`: skeleton
- `ready`: render normal
- `empty`: empty-state (quando lista vazia)
- `error`: error banner (quando falha global)
## Actions
- Click 'Create process' -> navigate `/processes/new`
- Click run item -> navigate `/runs/{executionId}` (se existir)

## A11y notes
- Recent runs list: cada item é `<button>`/`<a>` com label contendo processId + status.
- SideNav: foco visível e aria-current na rota ativa.
