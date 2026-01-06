# Prompt (GitHub Copilot) — Atualizar IT13 para Multi-Engine (legacy vs plan_v1) e JSON variado

## Precedência
- Este prompt tem prioridade sobre qualquer agente base.
- NÃO executar junto com outros prompts.
- Escopo: testes de integração (IT13) + ajustes mínimos de fixtures.

## Objetivo
Atualizar `IT13_LLMAssistedDslFlowTests` para:
- Manter o comportamento atual do legacy (não quebrar)
- Adicionar cobertura para `engine=plan_v1` (e opcional `engine=auto`)
- Garantir que os testes não dependam de variáveis frágeis do schema inferido (que é permissivo)
- Cobrir JSON variado (items/results/root-array)

## Regras
- Não remover cenários existentes; apenas estender.
- Se o ambiente não tiver chave LLM, os testes plan_v1 devem conseguir passar via templates determinísticos (template->plano) para casos simples.
- Reduzir flakiness: asserts sobre campos essenciais e preview, não sobre a forma exata do schema (a não ser parseabilidade + type básico).

## Casos
1) SimpleExtraction (PT-BR):
  - Deve passar com `engine=plan_v1` usando template->plano sem LLM
  - Validar que preview tem colunas para id/nome/cidade (ou mapeamento equivalente ao sample)
2) Aggregation (EN):
  - Deve passar com `engine=plan_v1` (template T5)
  - Validar que retorna 2 linhas (categories) e Total Revenue correto
3) WeatherForecast:
  - Deve passar com `engine=plan_v1` (template T2)
  - Validar ordering por date asc e tradução de condition conforme prompt se explicitado
4) ComplexTransformation:
  - Pode continuar rodando no legacy (ou plan_v1 se LLM disponível)
  - Marcar como `[Trait("RequiresLLM","true")]` se necessário, ou condicionar execução conforme env var

## Fixtures de input
Adicionar variações de JSON:
- root array: `[{"id":...}]`
- wrapper items: `{"items":[...]}`
- wrapper results: `{"results":[...]}`
E garantir que RecordPathDiscovery escolha corretamente.

## Critérios de aceite
- IT10/IT11/IT12 continuam passando
- IT13 passa >= 3/4 quando `engine=plan_v1` em ambiente sem LLM (usando templates)
- Sem regressão no legacy
