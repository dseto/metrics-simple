
# A11y Requirements (Spec-Driven)

Data: 2026-01-01

## Objetivo
Consolidar requisitos de acessibilidade (WCAG 2.1 AA) e transformar em regras implementáveis.

## Requisitos mínimos (must)
1) **Teclado 100%**: todas ações alcançáveis por Tab/Shift+Tab, Enter/Space.
2) **Foco visível** sempre.
3) **Labels**:
   - inputs com label explícito
   - `aria-describedby` apontando para mensagens de erro
4) **Dialog**:
   - focus trap
   - ESC fecha (quando permitido)
   - retorno de foco para o trigger
5) **Tabela**:
   - headers com `scope="col"`
   - sorting com `aria-sort`
6) **Snackbar**:
   - não deve ser o único canal para erro crítico (usar banner).
7) **Contraste**:
   - usar apenas tokens (garantir contraste via design system)

## Testes unitários (mínimo viável)
- Verificar presença de aria-label/aria-describedby em componentes críticos
- Verificar ordem de tab em dialogs (focus trap básico)
