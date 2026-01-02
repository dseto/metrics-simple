
# Design Tokens — Material 3 (Determinístico)

Data: 2026-01-01

Este documento define **regras** para uso de tokens; os **valores concretos** ficam em `../design/tokens/`.

## Fonte de verdade (valores)
- Brand palette: `../design/tokens/brand-colors.json`
- Material roles (light): `../design/tokens/material-theme.light.json`
- Material roles (dark): `../design/tokens/material-theme.dark.json`
- Typography: `../design/tokens/typography.json`
- Layout: `../design/tokens/layout.json`

## Regras
1) **Proibido** usar `#RRGGBB` no código (exceto no gerador de tokens).
2) Usar somente a escala de spacing/radius definida.
3) Tipografia deve seguir a escala; não inventar tamanhos.
4) Dark mode usa exclusivamente o arquivo dark.
