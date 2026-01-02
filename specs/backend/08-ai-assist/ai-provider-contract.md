# AI Provider Contract

Data: 2026-01-02

Objetivo: encapsular a integração com a plataforma de LLM (corporativa ou local), sem acoplar o backend à implementação.

## Interface (conceitual)
- `GenerateDslAsync(DslGenerateRequest request, CancellationToken ct) -> DslGenerateResult`

Os DTOs canônicos estão em `specs/shared/domain/schemas/`.

## Requisitos do provider
- Tempo limite configurável (padrão 30s)
- Retentativas controladas (0..1) somente em erros transitórios
- Deve retornar:
  - `dsl.profile` (copiar do request, ou retornar explicitamente)
  - `dsl.text` (string)
  - `outputSchema` (objeto JSON Schema)
  - `rationale` (string curta)
  - `warnings` (lista)
  - `modelInfo` opcional

> Observação: se o provider devolver `outputSchema` como string JSON, o backend deve parsear e validar antes de devolver ao client.

## Configuração
Configurações em `appsettings.json` (seção `AI`):
- `Enabled: true|false`
- `Provider: "HttpOpenAICompatible" | "MockProvider"`
- `EndpointUrl` (se HTTP)
- `Model`
- `PromptVersion`
- `TimeoutSeconds`

## Modo offline
Quando `enabled=false`, endpoint retorna 503 `AI_DISABLED`.
