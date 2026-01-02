
# UI Unit Test Strategy (sem E2E)

Objetivo: aumentar estabilidade da geração da UI sem testes end-to-end.

## Escopo
- Componentes puros: validação e transformação de estado
- Containers: lógica de chamada de API com mocks
- Utilitários: JSON parse/format; KV normalize

## Ferramentas (dependente do framework)
- Angular: Jest/Vitest + Testing Library Angular
- React: Vitest/Jest + Testing Library React

## Casos obrigatórios
- MsJsonEditorLite: parse ok/erro; format
- MsKeyValueEditor: dedupe; trim; remove empty
- MsPreviewPanel: bloqueia chamada se JSON inválido; renderiza erros
- MsProcessForm: validações mínimas
- MsProcessVersionForm: validações + prefill preview
