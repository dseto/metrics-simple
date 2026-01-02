
# UI Components — Implementable Spec (Material Design 3)

Data: 2026-01-01

Este diretório define **componentes de UI em nível implementável**.
Cada componente possui:
- Responsabilidade clara
- Props (inputs)
- Events (outputs)
- Validações
- Estados
- Mapeamento para Material Design 3

Esses componentes são **framework-agnostic**, mas mapeiam diretamente para:
- Angular Material 3
- Material Web Components
- React + MUI (Material 3)

---

## Convenções
- Props usam `camelCase`
- Events usam verbo no passado (`saved`, `deleted`)
- Componentes são **controlados** (estado vem de props sempre que possível)
