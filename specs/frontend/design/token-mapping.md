
# Token Mapping — Material Design 3

Data: 2026-01-01

## Objetivo
Definir **como** os tokens concretos serão aplicados no frontend com **Material 3** (Angular Material / MDC).

## Fonte de verdade
- Cores: `tokens/material-theme.light.json` e `tokens/material-theme.dark.json`
- Tipografia: `tokens/typography.json`
- Layout: `tokens/layout.json`

## Aplicação recomendada (determinística)
### 1) CSS variables (recomendado)
- Gerar (build step) um arquivo `theme.css` com variáveis do tipo:
  - `--md-sys-color-primary`
  - `--md-sys-color-on-primary`
  - ...
- O mapeamento é 1:1 com `material3Roles`.

### 2) Angular Material theme (alternativa)
- Criar tema com `@use '@angular/material' as mat;`
- Mapear roles para paletas.
- Observação: o modelo de theming muda por versão; **CSS variables** é mais estável para spec-driven.

## Tipografia
- Aplicar `font-family` em `body` e manter escala conforme `tokens/typography.json`.
- Não inventar tamanhos fora da escala.

## Spacing e radius
- Usar somente os valores de `tokens/layout.json`.
