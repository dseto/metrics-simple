
# UI Contract Tests (lite)

Objetivo: travar que a UI segue contratos (sem E2E).

## Estratégia
- Testar normalização e validações de DTOs
- Testar mapeamento HTTP->UiError
- Testar que clients chamam rotas corretas (com mocks)

## Casos mínimos
- ProcessDto normalize/validate
- ProcessVersionDto normalize/validate
- Preview: bloqueia chamada se JSON inválido
- Error mapping: 400/409/500/network
