
# Component: DataTable

## Responsabilidade
Listar dados tabulares com ações por linha.

## Material base
- M3 Data Table

## Props
| Nome | Tipo | Obrigatório |
|----|----|----|
| columns | Column[] | sim |
| rows | any[] | sim |
| loading | boolean | não |
| emptyMessage | string | não |
| rowActions | RowAction[] | não |

### Column
- id: string
- label: string
- type: text | chip | icon | actions
- width?: number

### RowAction
- id: string
- icon: string
- tooltip: string

## Events
- rowClicked(row)
- actionClicked(actionId, row)

## Estados
- loading (skeleton)
- empty
- error (externo)

## Acessibilidade
- keyboard navigation
- aria-sort
