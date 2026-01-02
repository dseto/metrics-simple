
# Component: JsonEditor

## Responsabilidade
Editar e visualizar JSON com validação básica.

## Material base
- Textarea monospace
- Buttons (Format, Copy)

## Props
| Nome | Tipo | Obrigatório |
|----|----|----|
| value | string | sim |
| readOnly | boolean | não |
| height | number | não |
| error | string | não |

## Events
- changed(value)
- formatRequested()

## Validações
- JSON.parse local

## Estados
- normal
- invalid
