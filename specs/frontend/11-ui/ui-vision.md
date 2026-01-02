
# UI Vision — Metrics Simple (Material Design 3)

Data: 2026-01-01

## Objetivo
Fornecer uma UI administrativa moderna (estilo **Material Design 3**) para:
- Gerenciar **Processes**, **ProcessVersions** e **Connectors**
- Executar **Preview Transform** (design-time) com retorno de JSON e CSV preview
- Exibir informações operacionais básicas (sem monitoramento avançado)

## Escopo
### Incluído
- Navegação principal (Drawer)
- Dashboard de processos
- CRUD Process, Version e Connector
- Preview Transform
- Página Help (Runner CLI)

### Fora de escopo (versão simples)
- Editor avançado com Monaco
- Execução do Runner dentro do browser
- Observabilidade avançada (Application Insights, Azure Monitor)
- RBAC / multi-tenant
- E2E tests

## Princípios
- **Spec-driven**: campos/validações derivam de:
  - `specs/03-interfaces/openapi-config-api.yaml`
  - `specs/02-domain/schemas/*.schema.json`
- **Determinismo**: preview usa o backend (via API) para garantir consistência.
- **Modernidade**: Material 3, superfícies tonais, ícones Material Symbols.

## Tecnologia (recomendação)
- Front-end: Angular 17+ com Material 3 (ou React + Material)
- Hospedagem: IIS
