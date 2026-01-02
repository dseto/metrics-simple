Antes de qualquer implementação, leia `TECH_STACK.md`.

# PROMPTS — Spec‑Driven (Shared + Backend + Frontend)

Este arquivo é o **roteiro operacional** para gerar (ou implementar) o software a partir do spec pack usando **VS Code Insiders + GitHub Copilot (Agent Mode)** com o agente **Spec‑Driven Builder**.

> Objetivo: reduzir variações, garantir consistência com os contratos (`shared`) e manter execução determinística.

---

## Como usar (recomendado)

### Uma conversa ou várias?
- Use **um único chat por macro‑fase** para manter contexto:
  - Chat A: **Shared validation + alinhamento**
  - Chat B: **Backend**
  - Chat C: **Frontend**
- Dentro de cada chat, execute os prompts **na ordem**, sem “pular” etapas.
- Se você interromper por muito tempo ou mudar muito o contexto, **abra um novo chat** e recomece pela etapa “Recap” (Prompt 01).

### Regras para o agente (copiar para o início do chat)
Cole isto como primeira mensagem do chat:

> Você é o agente **Spec‑Driven Builder**.  
> Fonte da verdade: `specs/shared/*` → `specs/backend/*` → `specs/frontend/*`.  
> Não invente endpoints/campos/regras. Se houver ambiguidade, pare e pergunte.  
> Gere mudanças pequenas e verificáveis. Após cada etapa, liste: arquivos alterados, comandos para validar e o checklist atendido.

### Convenções de execução
- Sempre gerar um `executionId` (UUID) por execução/rodada de geração e registrar nos logs.
- Sempre manter outputs determinísticos:
  - Mesmo input + DSL + schema → mesmo CSV.
- Não usar Python no backend (somente C#/.NET).

---

## Prompt 00 — Validar o deck Shared (contratos)

**Quando usar:** sempre no começo do Chat A (ou antes de iniciar backend/frontend).

**Input (arquivos):**
- `specs/shared/openapi/config-api.yaml`
- `specs/shared/domain/schemas/*.schema.json`
- `specs/shared/examples/*`
- `specs/shared/spec-manifest.json`

**Prompt (cole no chat):**
> Leia o deck `specs/shared`.  
> 1) Verifique se todos os `$ref` dos JSON Schemas resolvem (paths relativos).  
> 2) Verifique se o OpenAPI referencia schemas existentes (e vice‑versa).  
> 3) Valide os exemplos (`specs/shared/examples/*`) contra os schemas correspondentes.  
> 4) Gere um relatório com: inconsistências encontradas, severidade (HIGH/MED/LOW), correção proposta e arquivos a alterar.  
> 5) Se houver correção necessária, aplique as correções **somente** no deck shared e atualize o `spec-manifest.json` (sha256 dos arquivos alterados).  
> Saída esperada: um resumo + lista de patches por arquivo.

**Checklist de saída:**
- [ ] Nenhum `$ref` quebrado
- [ ] OpenAPI sem referências inválidas
- [ ] Exemplos válidos
- [ ] `spec-manifest.json` atualizado quando necessário

---

## Prompt 01 — Recap (estado do projeto + plano curto)

**Quando usar:** no início de qualquer chat (A/B/C) ou após reabrir conversa.

**Prompt:**
> Faça um recap do spec pack (3 decks) em 15 linhas:  
> - o que é shared/back/frontend  
> - principais fluxos (CRUD, preview, runner)  
> - principais arquivos de referência por deck (paths).  
> Em seguida, proponha um plano de execução em 6–10 passos para a macro‑fase atual (informe se é Shared / Backend / Frontend).

---

# BACKEND — prompts por etapa

> Stack fixa backend: .NET 10, SQLite local, Serilog, NJsonSchema 11.0.2.

## Prompt 10 — Gerar/confirmar esqueleto do backend (projeto)

**Input:**
- `specs/backend/01-overview/*`
- `specs/shared/*`

**Prompt:**
> Gere (ou valide) o esqueleto do backend em .NET 10 com a estrutura:  
> `src/Common`, `src/Engine`, `src/Api`, `src/Runner`, `config/`, `output/`, `.github/`.  
> Regras: namespace raiz `MetricsSimple.*`; sem Python.  
> Inclua `global.json`, `.editorconfig`, `Directory.Build.props` e tasks VS Code.  
> Saída: árvore de diretórios e arquivos criados + comandos `dotnet restore/build`.

**Checklist:**
- [ ] `dotnet restore` e `dotnet build` passam
- [ ] Projetos referenciam `specs/shared` como SSOT (pelo menos via links/documentação)

---

## Prompt 11 — SQLite: esquema + migração inicial

**Input:**
- `specs/backend/06-storage/sqlite-schema.md`
- `specs/shared/domain/schemas/*.schema.json`

**Prompt:**
> Implemente o esquema SQLite conforme `sqlite-schema.md` com tabelas e constraints.  
> Entregue:  
> - script `config/sqlite/init.sql`  
> - bootstrap de criação no startup (API/Runner) se DB não existir  
> - seed opcional (vazio por padrão)  
> Garanta: IDs únicos, versões por processId únicas, timestamps (createdAt/updatedAt).  
> Mostre também como configurar connection string (`config/config.db`).

**Checklist:**
- [ ] DB cria sem erro
- [ ] Constraints principais presentes (PK/UK/FK)
- [ ] Padrão de paths e nomes conforme specs

---

## Prompt 12 — Repositórios (contratos + implementação)

**Input:**
- `specs/backend/06-storage/repository-contracts.md`

**Prompt:**
> Implemente os repositórios SQLite (Connectors, Processes, ProcessVersions).  
> Regras: CRUD completo; erros padronizados; evitar SQL injection; usar parâmetros.  
> Entregue interfaces + implementação + testes unitários de repositório com DB temporário.  
> Saída: arquivos criados/alterados + como rodar os testes.

**Checklist:**
- [ ] CRUD funciona e respeita constraints
- [ ] Testes de repositório cobrem create/get/update/delete + conflitos
- [ ] Sem dependências não especificadas

---

## Prompt 13 — Engine (DSL → JSON) + Schema validation + CSV

**Input:**
- `specs/backend/05-transformation/*`
- `specs/shared/domain/schemas/previewResult.schema.json`

**Prompt:**
> Implemente a Engine conforme specs:  
> - Parser/avaliador DSL suportado (paths, columns, defaults, transforms permitidos)  
> - `preview/transform` gera `outputJson`, valida com JSON Schema e gera `previewCsv` quando aplicável  
> - CSV determinístico: ordem de colunas, escaping, newline, encoding  
> Inclua **golden vectors** (fixtures) com inputs/DSL/schema e CSV esperado.

**Checklist:**
- [ ] Preview retorna erros consistentes com NJsonSchema (path/kind/message)
- [ ] CSV determinístico (mesma saída sempre)
- [ ] Golden vectors adicionados e documentados

---

## Prompt 14 — API (Minimal API) endpoints + ApiError

**Input:**
- `specs/shared/openapi/config-api.yaml`
- `specs/shared/domain/schemas/apiError.schema.json`
- `specs/backend/03-interfaces/*`

**Prompt:**
> Implemente os endpoints exatamente conforme o OpenAPI (`config-api.yaml`):  
> - `/api/connectors` CRUD  
> - `/api/processes` CRUD  
> - `/api/processes/{processId}/versions` CRUD  
> - `/api/preview/transform`  
> Regras:  
> - sempre responder erros no formato `ApiError` (shared)  
> - status codes conforme `specs/shared/domain/types/http-status-map.md`  
> - validação de request body via schemas (ou validação equivalente)  
> Entregue também um `api.http` (REST Client) com chamadas exemplo.

**Checklist:**
- [ ] Rotas batem com OpenAPI
- [ ] Erros padronizados (`ApiError`)
- [ ] Exemplo de chamadas funcionando

---

## Prompt 15 — Runner CLI (execução síncrona)

**Input:**
- `specs/backend/04-execution/*`

**Prompt:**
> Implemente o Runner CLI:  
> - `run --processId <id> [--version <n>]`  
> - `validate --processId <id> [--version <n>]`  
> - `cleanup` (retenção local)  
> Regras:  
> - exit codes: `0 OK`, `!=0 erro` (documentar)  
> - gerar `executionId` por execução e incluir em logs e paths  
> - storage local de CSV: `basePath/processId/yyyy/MM/dd/executionId.csv`  
> - retenção para não lotar disco  
> Saída: exemplos de comando + outputs esperados.

**Checklist:**
- [ ] Exit codes implementados
- [ ] `executionId` implementado e rastreável
- [ ] Paths e retenção conforme regra

---

## Prompt 16 — Logs (Serilog) e integração com Blob (opcional)

**Input:**
- `specs/backend/07-observability/*`

**Prompt:**
> Configure Serilog para:  
> - Console (dev)  
> - Arquivo local rotacionado (opcional)  
> - Sink para Azure Blob Storage (logs) **conforme spec** (se aplicável)  
> - incluir `executionId`, `processId`, `version` como enrichment  
> Garanta que os logs são fáceis de ingerir no Elastic (JSON lines recomendado).  
> Entregue `appsettings.json` completo e exemplos.

**Checklist:**
- [ ] Logs estruturados com correlacionadores
- [ ] Sem Application Insights / Azure Monitor (versão simples)

---

## Prompt 17 — Testes mínimos (unit + integração)

**Input:**
- `specs/backend/08-testing/*` (se existir)
- Golden vectors criados

**Prompt:**
> Adicione testes mínimos:  
> - unitários para Engine (DSL, schema, csv)  
> - integração para API (happy path + erros)  
> - integração para Runner (execução com DB temporário)  
> Inclua instruções `dotnet test` e um `TESTING.md`.

**Checklist:**
- [ ] `dotnet test` passa
- [ ] Cobertura de cenários críticos

---

# FRONTEND — prompts por etapa (Material 3)

> Stack UI: Material Design 3, componentes modernos, e specs de UI do deck frontend.

## Prompt 30 — Validar UI specs + field catalog

**Input:**
- `specs/frontend/11-ui/ui-field-catalog.md`
- `specs/frontend/11-ui/component-specs.md`
- `specs/frontend/11-ui/ui-routes-and-state-machine.md`
- Mockups (imagens) se presentes

**Prompt:**
> Leia o deck frontend e valide consistência:  
> - cada campo no field catalog existe em uma tela  
> - ids/paths são únicos e estáveis  
> - validações e mensagens estão definidas  
> Gere relatório de inconsistências e aplique correções nos arquivos de spec (frontend) se necessário.

---

## Prompt 31 — Gerar skeleton do frontend (com Material 3)

**Prompt:**
> Gere o skeleton do frontend conforme o deck:  
> - setup Material 3  
> - layout principal + navegação  
> - páginas: Connectors, Processes, Versions, Preview  
> - design tokens conforme specs  
> Entregue árvore de arquivos e instruções de build/run.

---

## Prompt 32 — Implementar componentes e formulários (100% pelo field catalog)

**Prompt:**
> Implemente formulários usando **somente** o `ui-field-catalog.md`:  
> - tipos de input  
> - validações  
> - mensagens de erro  
> - bindings (id/path)  
> Garanta que cada tela corresponde ao mockup e component specs.

---

## Prompt 33 — API client (OpenAPI shared)

**Prompt:**
> Implemente o client da API lendo os contratos do deck shared (OpenAPI).  
> Regras:  
> - sem endpoints inventados  
> - tratamento de erros `ApiError`  
> - loading/empty/error states conforme state machine.

---

# FINALIZAÇÃO

## Prompt 90 — Checklist final + “drift check”

**Prompt:**
> Execute um drift check:  
> - código backend implementa rotas do OpenAPI?  
> - modelos de request/response batem com shared schemas?  
> - frontend usa somente campos do field catalog?  
> Gere um relatório final com: gaps restantes, riscos e próximos passos.

---

## Prompt 99 — Empacotar e documentar

**Prompt:**
> Gere um pacote final com:  
> - instruções de build/run/test  
> - exemplos (api.http, seed, fixtures)  
> - mapa de pastas  
> - como evoluir specs sem quebrar contratos  
> Saída: `RELEASE_NOTES.md` e `docs/`.



## Prompt 18 — Implementar geração assistida de DSL (AI design-time)
**Input:**
- `specs/backend/08-ai-assist/*`
- `specs/shared/domain/schemas/dslGenerate*.schema.json`
- `specs/shared/openapi/config-api.yaml`

**Prompt:**
> Implemente o endpoint `POST /api/ai/dsl/generate` conforme as specs.  
> Adicione o provider (`IAiProvider`) com pelo menos `MockProvider` e um provider HTTP opcional.  
> Garanta guardrails: validar DSL, schema e preview interno; rejeitar com `AI_OUTPUT_INVALID` se falhar.  
> Inclua testes unitários e de integração.

**Checklist:**
- [ ] 200 retorna DSL + schema válidos
- [ ] 503 AI_DISABLED quando desabilitado
- [ ] 503 AI_OUTPUT_INVALID quando output não valida


## Prompt 34 — UI AI Assistant (frontend)
**Input:**
- `specs/frontend/11-ui/ui-ai-assistant.md`
- `specs/frontend/11-ui/ui-field-catalog.md`
- `specs/frontend/11-ui/component-specs.md`

**Prompt:**
> Implemente o painel/aba **AI Assistant** no editor de versão.  
> Use o field catalog como fonte da verdade (ids/paths, validações, mensagens).  
> Integre com `POST /api/ai/dsl/generate` e aplique o resultado nos campos do editor (dsl + outputSchema) somente após clique explícito em **Apply**.

**Checklist:**
- [ ] Estados idle/loading/success/error/disabled
- [ ] Tratamento 503 AI_DISABLED com banner
- [ ] Apply não salva automaticamente; apenas preenche