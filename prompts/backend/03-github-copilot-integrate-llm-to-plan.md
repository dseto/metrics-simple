# Prompt (GitHub Copilot) — Integrar LLM para gerar PLANO (IR v1) em plan_v1 (Structured Output real)

## Precedência
- Este prompt tem prioridade sobre qualquer agente base.
- NÃO executar junto com outros prompts de tarefa.
- Escopo: backend apenas.

## Objetivo
Quando `engine=plan_v1` (ou auto seleciona plan_v1), a LLM deve gerar um **Transformation Plan (IR v1)** válido (JSON) em vez de gerar Jsonata.

## Regras fundamentais
- A LLM NÃO deve gerar DSL textual.
- A LLM deve retornar **somente JSON** aderente ao JSON Schema do plano.
- O backend valida o plano (JSON Schema), valida paths contra o sample e executa determinístico.
- Se a LLM falhar (timeout/parse/schema invalid), usar fallback determinístico:
  - templates -> plano (T1/T2/T5) usando RecordPathDiscovery + FieldResolver
  - e somente se nada aplicar, retornar erro claro (400 com mensagem de ambiguidade), evitando 502 sempre que possível.

## Prompt do sistema (curto + few-shot)
Criar/atualizar prompt system do plan_v1 para:
- explicar o IR v1 e ops suportadas
- enfatizar:
  - recordPath deve apontar para o array principal (ou omitir e deixar backend descobrir)
  - ops devem produzir array de objetos no final
  - não inventar operadores, não inventar paths inexistentes
- incluir 3 exemplos few-shot:
  1) extraction PT-BR com rename
  2) aggregation EN (group by + sum revenue)
  3) weather sort + translate

## Structured outputs
Implementar enforcement do plano com JSON Schema (server-side):
- Se o provider suportar `json_schema`, enviar o schema do IR v1.
- Independente do provider, sempre validar no backend usando um validador de JSON Schema.
- Se structured outputs do provider forem frágeis, tratar a resposta como texto e parsear robusto, MAS só aceitar se passar no schema.

## Pipeline plan_v1 com LLM
1) Construir prompt com:
  - goal (texto do usuário)
  - sampleInput (compactado/truncado com cuidado)
  - opcional: lista de candidatos de recordPath (top 3) gerados pelo RecordPathDiscovery
2) Chamar LLM para retornar o plano JSON
3) Parse robusto (remover markdown, extrair JSON)
4) Validar JSON Schema do plano
5) Resolver recordPath se omitido ou inválido
6) Resolver aliases de campos
7) Executar o plano
8) Retornar preview + outputSchema (permissivo)
9) Se falhar em qualquer etapa:
  - se for erro recuperável (schema invalid, path invalid, wrong shape), tentar:
    a) correção determinística (resolver path/shape)
    b) template->plano
    c) 1 retry LLM com hints curtos (no máximo 1)
  - evitar loop de retries longos

## Observabilidade
- Logar: modelo, latência, engine, se plano veio de LLM ou de template, e categoria de erro (PlanSchemaInvalid, RecordPathNotFound, PathInvalid, WrongShape, etc.)
- Guardar um preview do plano (truncado) para debug.

## Critérios de aceite
- IT13 deve começar a passar ao menos os casos simples via template->plano sem depender do modelo
- `engine=plan_v1` nunca deve retornar 502 por problemas de parsing da LLM; preferir 400 com mensagem de ambiguidade ou fallback template
