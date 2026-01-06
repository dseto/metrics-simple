# Prompt (GitHub Copilot) — Implementar Plan Engine Core (IR v1 + executor determinístico)

## Precedência
- Este prompt tem prioridade sobre qualquer agente base.
- NÃO executar junto com outros prompts de tarefa.
- Escopo: backend apenas.

## Objetivo
Implementar o motor `plan_v1` de ponta a ponta **sem LLM ainda**:
- Definir IR (plano) v1
- Validar IR via JSON Schema (server-side)
- Descobrir `recordset`/`arrayPath` em JSON variado
- Resolver campos (aliases pt/en + matching simples)
- Normalizar shape (garantir `array<object>`)
- Executar operações determinísticas: select/rename, filter, compute, sort, group+aggregate
- Gerar preview (JSON/CSV conforme já existe) e outputSchema inferido de forma permissiva

## Regras de confiabilidade (não negociáveis)
- Nunca quebrar por `undefined`/null: se preview não for JSON, retornar erro claro (WrongShape/PreviewNotJson).
- Sempre normalizar output final para `array<object>` (ou erro claro).
- Inferência de schema deve ser **permissiva**:
  - Sem `required` (ou required vazio)
  - `additionalProperties: true`
  - Merge de tipos ao longo dos itens do array

## IR v1 (model)
Criar modelos (C#) + JSON Schema (arquivo em templates/) com algo como:

```json
{
  "planVersion": "1.0",
  "source": { "recordPath": "/items" },
  "ops": [
    { "op": "select", "fields": [{ "from": "/name", "as": "Name" }] },
    { "op": "filter", "where": { "path": "/status", "eq": "active" } },
    { "op": "compute", "as": "revenue", "expr": "price * quantity" },
    { "op": "groupBy", "keys": ["/category"], "aggregates": [{ "fn": "sum", "expr": "revenue", "as": "Total Revenue" }] },
    { "op": "sort", "by": "/date", "dir": "asc" }
  ]
}
```

Notas:
- Paths no plano devem ser **JSON Pointer-like** (`/a/b/0/c`) ou uma forma simples e consistente.
- Para MVP, suporte apenas paths simples (sem wildcard); o recordPath aponta para o array base.

## Descoberta de recordPath (RecordPathDiscovery)
Implementar componente determinístico que, dado `sampleInput`, retorna:
- lista de candidatos (paths que são arrays)
- escolha do melhor candidato (score)
- ou erro "NoRecordsetFound"

Score sugerido:
- arrays com itens object valem mais
- arrays maiores valem mais
- nomes comuns (`items`, `results`, `data`, `sales`, `products`, `forecast`) adicionam pontos
- overlap com palavras do goal (se disponível no request) adiciona pontos

## FieldResolver (aliases)
Implementar resolver:
- Se o plano usa `/nome` mas o record tem `name`, mapear via aliases
- Aliases mínimos pt/en:
  - nome->name
  - cidade->city
  - idade->age
  - data->date
  - categoria->category
  - preco/preço->price
  - quantidade->quantity
- Resolver deve checar existência do campo no sample record e ajustar paths quando possível
- Se ambíguo (mais de uma opção), escolher a mais direta e registrar warning

## Executor de ops (determinístico)
Implementar executor que trabalha sobre `IReadOnlyList<JsonElement>` (rows) ou equivalente:
- select: cria novo objeto com chaves `as` e extrai valores de `from`
- filter: suporta operadores básicos (eq, ne, contains, gt/gte/lt/lte) para strings/números
- compute: suporta expressões simples:
  - `a * b`, `a + b`, `a - b`, `a / b`
  - onde `a`/`b` podem ser paths ou constantes numéricas
- sort: asc/desc por path (string/número/date ISO)
- groupBy+aggregate:
  - keys: lista de paths
  - aggregates: fn = sum, count, avg, min, max
  - expr: path ou expressão simples (como compute)
  - output: rows agregadas (array<object>)

## Integração com endpoint (plan_v1)
- `PlanV1AiEngine.GenerateAsync` deve:
  1) obter recordPath (se não vier no request, descobrir)
  2) obter `rows` do recordset
  3) executar ops
  4) normalizar shape
  5) gerar preview/CSV se já existir infraestrutura; caso não, gerar JSON preview e converter para CSV no mesmo padrão do engine atual
  6) inferir outputSchema permissivo
  7) retornar no mesmo response contract, com `dsl.text = "<plan_v1>"` e `plan` opcional quando `includePlan=true`

## Critérios de aceite
- Build OK
- `engine=plan_v1` funciona com plano fornecido (mesmo que gerado manualmente em teste)
- Sem LLM neste prompt
- Inclui testes unitários básicos do RecordPathDiscovery, ShapeNormalizer e GroupBy
