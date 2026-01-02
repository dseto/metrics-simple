# Page — Version Editor

    Data: 2026-01-01

    ## Route
    - `/processes/:id/versions/:version (edit) e /processes/:id/versions/new (create)`

    ## Purpose
    - Criar/editar ProcessVersion (source request, DSL, output schema, sample input).

## Referência visual
- `visual/mockups/A_set_of_six_high-fidelity_UI_wireframe_mockups_di.png`
## Layout (responsive)
- Layout 2-col (>= lg):
-   - esquerda: form (source request + DSL + schema)
-   - direita: preview panel (input/output/CSV) e ações de preview
- < lg: preview abaixo do form.
## Components composition
- MsAppShell
  - MsPageHeader (Save, Back)
  - MsVersionEditorForm
    - SourceRequestSection
    - DslSection (MsJsonEditorLite para DSL text)
    - OutputSchemaSection (MsJsonEditorLite)
    - SampleInputSection (MsJsonEditorLite)
  - MsPreviewPanel
  - MsErrorBanner

## Data binding (container -> components)
- `versionEditorState.model -> MsVersionEditorForm.model`
- `versionEditorState.preview.request -> MsPreviewPanel.request`
- `versionEditorState.preview.response -> MsPreviewPanel.response`
- `versionEditorState.preview.loading -> MsPreviewPanel.loading`
## Preview behavior
- Botão Preview chama `/preview/transform` com sampleInput + DSL + outputSchema.
- Mostrar:
  - JSON output (pretty)
  - CSV preview
  - errors[] quando inválido

## Error UX
- Erro de preview: banner dentro do MsPreviewPanel + Retry
- Erro de save: snackbar + retry

## A11y notes
- Editors: `aria-label` por seção (DSL editor, Schema editor, Sample input).
- Preview tabs: usar role=tablist e keyboard arrows.
