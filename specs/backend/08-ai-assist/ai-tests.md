# AI Tests (Design-time)

## Unit tests
- `MockProvider` retorna payload válido e inválido.
- Validador rejeita:
  - DSL inválida
  - JSON schema inválido
  - preview falha

## Integration tests (API)
- `POST /api/ai/dsl/generate` 200 quando habilitado + provider ok
- 503 `AI_DISABLED` quando desabilitado
- 503 `AI_OUTPUT_INVALID` quando provider retorna output ruim

## Golden vectors
- Reutilizar `specs/shared/examples/dslGenerate*.sample.json` como casos base.
