
# Component: FormField

## Responsabilidade
Campo de formulário com validação e feedback.

## Material base
- TextField / Select / Textarea (M3)

## Props
| Nome | Tipo | Obrigatório |
|----|----|----|
| label | string | sim |
| value | any | sim |
| type | text | select | textarea | json |
| options | Option[] | condicional |
| required | boolean | não |
| disabled | boolean | não |
| error | string | não |
| helperText | string | não |

### Option
- value: string
- label: string

## Events
- changed(value)
- blurred()

## Validações
- required
- JSON parse (quando type=json)

## Estados
- normal
- invalid
- disabled
