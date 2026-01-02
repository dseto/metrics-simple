# Page — Preview Transform

Data: 2026-01-01

## Route
- `/preview`

## Purpose
- Tela dedicada (opcional) para testar transformação sem salvar versão.
## Layout (responsive)
- 3 col (>= lg): Input | DSL | Output/CSV
- < lg: tabs.
## Components
- MsPreviewWorkbench
  - MsJsonEditorLite (Input)
  - MsJsonEditorLite (DSL)
  - MsJsonEditorLite (Schema)
  - MsPreviewPanel

## States
- `loading`: skeleton
- `ready`: render normal
- `empty`: empty-state (quando lista vazia)
- `error`: error banner (quando falha global)
