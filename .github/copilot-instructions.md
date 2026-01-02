# Copilot Instructions (Governança) — Spec‑Driven

## Princípios
1. **Specs são a fonte da verdade**. Código deve seguir:
   - `specs/shared/*` (contratos)
   - `specs/backend/*` (backend)
   - `specs/frontend/*` (frontend)
2. **Não inventar** endpoints, campos, validações ou regras fora das specs.
3. **Mudança de contrato** exige update no deck `shared` e rastreabilidade.

## Stack fixa (não negociar)
- .NET 10, C# (backend)
- SQLite (local)
- Serilog (logs)
- NJsonSchema (schema validation)
- Material Design 3 (frontend)

## Qualidade mínima (obrigatório)
- Build deve passar (`dotnet build`)
- Testes devem passar (`dotnet test`)
  - Contract tests
  - Golden tests
  - **Integration tests (E2E) obrigatórios**: WebApplicationFactory + mock HTTP (FetchSource) + SQLite + runner
- Sem warnings críticos; `nullable` habilitado
- Erros devem seguir `ApiError` (shared)

## Fluxo de trabalho
- Antes de codar: ler `specs/spec-index.md`
- Implementar em pequenas mudanças com commits frequentes
- Após cada etapa: rodar build/test e corrigir iterativamente
