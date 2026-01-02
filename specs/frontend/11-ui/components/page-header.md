
# Component: PageHeader

## Responsabilidade
Exibir título da página, subtítulo e ações principais.

## Material base
- Top App Bar (M3)
- Surface container

## Props
| Nome | Tipo | Obrigatório | Descrição |
|----|----|----|----|
| title | string | sim | Título da página |
| subtitle | string | não | Texto auxiliar |
| actions | Action[] | não | Botões principais |

### Action
- id: string
- label: string
- icon?: string (Material Symbol)
- variant: filled | tonal | outlined | text
- disabled?: boolean

## Events
- actionClicked(actionId)

## Estados
- default
- loading (desabilita ações)

## Acessibilidade
- role="banner"
- título como `<h1>`
