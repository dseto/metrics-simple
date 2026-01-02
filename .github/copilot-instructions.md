# Copilot Instructions — Metrics Simple (Spec-Driven)

## Fonte da verdade
- **`specs/` é a fonte de verdade** para contratos, comportamento e convenções.
- A implementação deve estar aderente, principalmente a:
  - `specs/00-vision/spec-index.md` (mapa do spec pack)
  - `specs/03-interfaces/openapi-config-api.yaml` (contratos HTTP)
  - `specs/02-domain/schemas/*.schema.json` (modelos/dados)
  - `specs/04-execution/*` (Runner CLI + pipeline + exit codes + layout/retention)
  - `specs/05-transformation/*` (DSL + golden tests)
  - `specs/06-storage/*` (Local/Blob)
  - `specs/07-observability/*` (Serilog JSONL + correlation)
  - `specs/09-testing/*` (somente unit tests)

Se houver divergência, **ajuste o código** para ficar compatível com os specs. Só proponha alterar specs se for inevitável.

---

## Objetivo do produto (versão simples)
Solução “single-server” composta por:
1) **Studio (UI)** hospedado no **IIS** (sem Monaco Editor)
2) **API de Configuração (REST)** hospedada no **IIS**
3) **SQLite local** como Config Store
4) **Runner (Console/CLI) síncrono** que executa o pipeline e gera CSV
5) **Segredos em `.json` local (texto puro)** — risco aceito (não implementar ACL/cripto/DPAPI)
6) **CSV**: salvar em **filesystem local** e/ou **Azure Blob Storage**
7) **Logs (Serilog)**: JSON Lines gravados em **Azure Blob Storage** (ingestão no Elastic por processo externo)
8) **Testes**: manter **apenas testes unitários** (sem E2E)

---

## Regras obrigatórias
### Runner (CLI)
- Seguir `specs/04-execution/runner-cli-spec.md`
- Implementar exit codes conforme `specs/04-execution/exit-codes.md`
- Implementar pipeline conforme `specs/04-execution/pipeline-spec.md`
- **Gerar `executionId`** no início e propagar em logs e nomes de arquivos (ver `specs/07-observability/correlation-spec.md`)
- Layout local do CSV conforme `specs/04-execution/file-layout.md`
- Retenção/limpeza conforme `specs/04-execution/retention-policy.md` (comando `cleanup`)

### Engine (Transformação/Validação/CSV)
- Seguir `specs/05-transformation/dsl-spec.md`
- Validar output com **JSON Schema** antes de gerar CSV (falha => Exit 40)
- Golden unit tests devem ser gerados a partir de `specs/05-transformation/unit-golden-tests.yaml`

### Storage
- Local e Blob: seguir `specs/06-storage/*`
- Logs em Blob: seguir `specs/07-observability/*` + `specs/06-storage/azure-blob-spec.md`

### Secrets
- Seguir `specs/08-security/local-secrets-policy.md`
- Não implementar criptografia, DPAPI, BitLocker ou ACL forte
- Não logar secrets

### API
- Aderir ao OpenAPI em `specs/03-interfaces/openapi-config-api.yaml`
- Implementar `/preview/transform` conforme spec

### Testes
- Somente unit tests: `specs/09-testing/*`
- Contratos “lite”: validar OpenAPI/JSON Schemas via testes unitários

---

## Build & Test (contrato)
- Seguir `specs/00-vision/build-and-test.md` para comandos oficiais e dependências.

---

## Definição de pronto (DoD)
- `dotnet build` passa
- `dotnet test` passa
- OpenAPI e JSON Schemas válidos (testes unitários)
- Runner respeita exit codes e gera artefatos conforme specs
