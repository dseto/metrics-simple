
# Frontend Specs (UI)

Data: 2026-01-01

## Propósito
Define **UI Material Design 3** e comportamento do cliente.

## Referências obrigatórias
- Contratos canônicos em `../shared/`.
- UI deve implementar conforme `11-ui/` e consumir a API conforme OpenAPI shared.

## Conteúdo típico
- UI: `11-ui/`
- Testes UI: `09-testing/`


## Hardened frontend stability artifacts
- Design tokens concretos em `design/tokens/` (cores/typografia/layout)
- Token mapping determinístico (`design/token-mapping.md`)
- Pages specs detalhadas (`11-ui/pages/*.md`) com layout + data binding
- Error UX prescritivo (`11-ui/states-and-feedback.md`)
- A11y implementável (`a11y/a11y-requirements.md` + seções em component specs)
- Referências visuais (`visual/mockups/*`)

- Field catalog por tela: `11-ui/ui-field-catalog.md`
