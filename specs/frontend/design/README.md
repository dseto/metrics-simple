
# Design System — Tokens (Source of Truth)

Data: 2026-01-01

Este deck fixa o design com **tokens concretos** (cores, tipografia e layout) para reduzir variações na implementação.

Arquivos principais:
- `tokens/brand-colors.json` — paleta Zurich (nomeada)
- `tokens/material-theme.light.json` — roles Material 3 (light)
- `tokens/material-theme.dark.json` — roles Material 3 (dark)
- `tokens/typography.json` — tipografia e escala
- `tokens/layout.json` — spacing/radius/elevation/breakpoints

Regra: **não usar cores hardcoded** no código. Sempre referenciar tokens.
