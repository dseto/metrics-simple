
# Component: KeyValueEditor

## Responsabilidade
Editar listas de pares chave/valor (headers, query params).

## Material base
- Table + TextField

## Props
| Nome | Tipo | Obrigatório |
|----|----|----|
| items | {key:string,value:string}[] | sim |
| readOnly | boolean | não |

## Events
- changed(items)

## Regras
- Keys não vazias
- Keys únicas
