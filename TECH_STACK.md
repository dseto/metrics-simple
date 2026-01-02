# Technology Stack — MetricsSimple v1.2

Este documento define a **stack tecnológica obrigatória** do projeto.
Qualquer desvio é considerado **fora de escopo**.

---

## Backend

- Linguagem: **C#**
- Runtime: **.NET 10**
- API: **ASP.NET Core Minimal API**
- Runner: **Console Application (CLI)**
- Execução: **SÍNCRONA**
- Persistência: **SQLite local**
- Transform DSL: **Jsonata** (via biblioteca externa estável)
- JSON: **System.Text.Json**
- Schema validation: **NJsonSchema**
- Logs: **Serilog** (JSONL)

---

## Testes

### Unit / Contract / Golden
- Framework: **xUnit**
- Golden tests: fixtures + comparação semântica de JSON + CSV byte-a-byte
- Contract tests: parse OpenAPI + validação de schemas + alinhamento de DTOs

### Integration (obrigatório)
- Host in-memory da API: **Microsoft.AspNetCore.Mvc.Testing** (WebApplicationFactory)
- Mock HTTP para fontes externas (FetchSource):
  - Preferido: **WireMock.Net** (in-process) — não requer Docker
  - Opcional: **testcontainers-dotnet** para rodar WireMock/Azurite em container (quando Docker Desktop/CI suportar)

> A spec exige que **integration tests rodem sem dependência de internet** e validem o caminho real de fetch HTTP (mockado) → transformação → validação → CSV.

---

## Princípios não negociáveis
- Determinismo
- Reprodutibilidade
- Auditabilidade
- Spec-Driven Development
