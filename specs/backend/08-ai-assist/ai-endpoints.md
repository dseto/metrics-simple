# AI Endpoints (Design-time)

Data: 2026-01-02

## Endpoint
`POST /api/ai/dsl/generate`

### Request/Response
- Request: `specs/shared/domain/schemas/dslGenerateRequest.schema.json`
- Response: `specs/shared/domain/schemas/dslGenerateResult.schema.json`
- Error (503): `specs/shared/domain/schemas/aiError.schema.json`

### Comportamento (mínimo)
1. Verificar se AI está habilitada (`config/ai.json`).
2. Validar request (tamanho e estrutura).
3. Chamar provider de LLM via `IAiProvider.GenerateDslAsync(...)`.
4. Validar resposta do provider:
   - DSL parse ok (Engine)
   - Schema parse ok (JSON Schema)
5. Rodar um preview interno (reusar Engine/Preview):
   - input: `sampleInput`
   - dsl: gerada
   - schema: gerado
6. Se preview falhar, retornar `AI_OUTPUT_INVALID` (503) com detalhes.

### Observabilidade
Logar (Serilog) com:
- `correlationId` (header `X-Correlation-Id` quando presente)
- provider/model/promptVersion
- tamanho de input/output, latência, status (success/fail)

> Importante: nunca logar segredos. `sampleInput` pode conter dados sensíveis: logar apenas tamanho/hash.
