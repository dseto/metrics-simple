# Prompt Templates (Design-time)

Data: 2026-01-02

> O backend controla o template para reduzir variação.

## System prompt (v1)
- Você é um gerador de DSL para o perfil solicitado (`dslProfile`, mínimo: `jsonata`).
- Você **NÃO** pode executar código, chamar rede ou inferir dados fora do `sampleInput`.
- Saída deve ser **apenas JSON** no formato (canônico):
  `{
    "dsl": { "profile": "jsonata", "text": "..." },
    "outputSchema": { ... },
    "rationale": "...",
    "warnings": ["..."]
  }`

## User prompt (inputs)
- `goalText`
- `sampleInput` (JSON)
- `dslProfile`
- `constraints`
- `hints` (opcional)
- `existingDsl` / `existingOutputSchema` (opcional)

## Few-shot examples
Usar 1–2 exemplos pequenos (do deck shared examples) para formatar.

## Post-conditions (hard)
- DSL parseável (engine)
- outputSchema parseável (JSON) e válido como schema
- preview interno deve produzir output válido

Se falhar, o backend rejeita a saída e devolve erro `AI_OUTPUT_INVALID`.
