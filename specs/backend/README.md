# Backend Specs

Data: 2026-01-01

## Propósito
Define **comportamento e implementação server-side** (API, Engine e Runner).

## Referências obrigatórias
- OpenAPI e Schemas ficam em `../shared/`.
- Se precisar citar contrato, aponte para:
  - `../shared/openapi/config-api.yaml`
  - `../shared/domain/schemas/*.schema.json`

## Conteúdo típico
- Execução: `04-execution/`
- Transformação: `05-transformation/`
- Storage: `06-storage/`
- Observabilidade: `07-observability/`


## Hardened behavior coverage
Este deck agora inclui specs explícitas para:
- regras de domínio
- comportamento de API (erros/status/idempotência)
- pipeline de execução do runner + contrato de CLI
- regras determinísticas de CSV e validação de outputSchema
- schema SQLite e contratos de repositório
- observabilidade via logs JSONL
- golden/contract/repository tests

- integration tests E2E obrigatórios (WebApplicationFactory + mock HTTP + runner)

