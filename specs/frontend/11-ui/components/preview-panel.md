
# Component: PreviewPanel

## Responsabilidade
Exibir resultado do Preview Transform.

## Material base
- Tabs
- Code blocks

## Props
| Nome | Tipo | Obrigatório |
|----|----|----|
| jsonOutput | string | não |
| csvOutput | string | não |
| errors | string[] | não |
| loading | boolean | não |

## Estados
- idle
- loading
- success
- error

## Ações
- Copy JSON
- Copy CSV
