# Backend Specs — Delta Pack: Segurança + AuthN/AuthZ (LocalJwt em SQLite, Okta/Entra-ready)

**Data:** 2026-01-03

Este delta pack adiciona uma camada mínima de segurança e autenticação/autorização ao backend,
mantendo o design preparado para migrar no futuro para **Okta** ou **Entra ID** com baixo impacto.

## Estrutura (mesma do spec deck original)
- `00-vision/` visão e índice do delta
- `02-domain/` modelos e invariantes de auth
- `03-interfaces/` endpoints, contrato de erro e requisitos de segurança da API
- `04-execution/` ordem de middlewares e pipeline de auth
- `06-storage/` schema SQLite + migration
- `07-observability/` audit logging por request
- `09-testing/` testes e critérios de aceite

> Observação: este pack descreve **alterações (delta)** sobre o deck base.
Ele não substitui o deck inteiro; ele especifica o que deve ser acrescentado/alterado.

## Arquivo de entrada
- `00-vision/security-auth-delta.md` (comece por aqui)
