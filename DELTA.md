# DELTA — Plan Engine (IR v1)

## Mudanças principais
1. **Novo parâmetro opcional** `engine` no request do endpoint `/api/v1/ai/dsl/generate`
   - `legacy` (default inicial): mantém comportamento atual
   - `plan_v1`: usa PlanEngine (IR v1)
   - `auto`: tenta `plan_v1` para casos cobertos e faz fallback para `legacy` quando necessário

2. **Novo PlanEngine (IR v1)**
   - LLM gera um plano JSON validável por JSON Schema
   - Backend executa determinístico com:
     - RecordPathDiscovery (descoberta do array principal)
     - FieldResolver (aliases pt/en e busca por chaves)
     - ShapeNormalizer (garante array<object> de saída)
     - OutputSchemaInferer permissivo

3. **Compatibilidade**
   - Mesmo endpoint e mesma resposta base.
   - Campo `dsl.text` continua existindo; em `plan_v1` pode conter DSL compilada (opcional) ou string de debug.
   - Campo opcional `plan` pode ser retornado **apenas** quando `includePlan=true` (evita quebrar clientes).

4. **Testes**
   - IT13 deve ser parametrizado para rodar com `engine=plan_v1` (alvo) e opcionalmente `engine=legacy` (regressão).

## Arquivos adicionados
- specs/backend/05-transformation/plan-engine-v1.md
- specs/backend/05-transformation/engine-routing-and-compat.md
- specs/backend/05-transformation/record-path-discovery.md
- specs/backend/05-transformation/field-resolver-and-aliases.md
- specs/backend/05-transformation/shape-normalization.md
- specs/backend/05-transformation/output-schema-inference-permissive.md
- templates/plan-schema-v1.json
- templates/plan-ops-reference.md
- prompts/backend/github-copilot-implement-plan-engine-v1.md
- prompts/backend/github-copilot-wire-engine-routing.md
- prompts/backend/github-copilot-update-it13-for-plan-engine.md
