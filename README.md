# Delta Spec Deck — Plan Engine (IR v1) paralelo ao Jsonata (legacy)

**Data**: 2026-01-06  
**Versão**: 1.0  
**Objetivo**: Adicionar um novo motor de transformação **PlanEngine (IR v1)** como opção incremental ao motor atual (legacy Jsonata/LLM), no **mesmo endpoint** `/api/v1/ai/dsl/generate`, com roteamento por parâmetro `engine` e configuração/feature flag.

## Por que este delta existe
O motor legacy (LLM → Jsonata) é frágil para:
- dialeto Jsonata (alucinação de sintaxe, shape incorreto)
- APIs com JSON variado (arrayPath/campos variam)
- confiabilidade (502 em casos simples)

O PlanEngine muda o papel da LLM:
- LLM gera um **plano JSON validável** (IR) — não uma DSL textual
- Backend executa o plano **deterministicamente** (sem depender da LLM acertar sintaxe)
- Continua possível compilar/retornar `dsl.text` por compatibilidade

## Escopo do PlanEngine v1
Operações suportadas (suficientes para cobrir IT13 e 80% dos casos comuns):
- `select` (projeção + rename)
- `filter` (condições simples)
- `compute` (expressões aritméticas simples, ex.: `price * quantity`)
- `mapValue` (tradução por dicionário, ex.: pending→Pendente)
- `groupBy` + `aggregate` (sum/count/avg/min/max)
- `sort` (asc/desc por campo)
- `limit` (opcional)

## Não-objetivos (v1)
- joins entre arrays distintos
- expressões arbitrárias/execução de código
- suporte total a todos formatos de data/hora
- UI/Front-end

## Conteúdo
- `specs/backend/05-transformation/` — especificações implementáveis
- `templates/plan-schema-v1.json` — JSON Schema do plano (IR)
- `prompts/backend/` — prompts de implementação para GitHub Copilot
- `DELTA.md` e `RELEASE_NOTES.md` — resumo de mudanças e release notes
