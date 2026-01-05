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

## 🚫 Restrições de Configuração (PROIBIDO para o agente)

**O agente NÃO PODE alterar:**
1. **Modelo LLM** - Somente o usuário pode alterar o modelo configurado em:
   - `src/Api/Program.cs` (campo `Model`)
   - `src/Api/AI/AiModels.cs`
   - Qualquer configuração de `AI:Model` em appsettings
2. **API Keys** - CRÍTICO:
   - **NUNCA hardcodear API keys no código fonte**
   - **SEMPRE carregar de variáveis de ambiente (.env)**
   - Nunca expor, logar ou modificar chaves de API reais
   - Em testes, usar tokens fake/mock (não chaves reais)
   - Exemplos de API keys que NÃO PODEM aparecer no código:
     - `sk-or-v1-*` (OpenRouter)
     - `sk-*` (OpenAI)
     - Qualquer token com formato de API key real
3. **Endpoints de LLM** - Somente usuário pode alterar `EndpointUrl`

**Se o agente identificar problemas com o modelo LLM:**
- Documentar o problema (padrão de erro, frequência)
- Sugerir alternativas ao usuário
- **NÃO alterar o modelo diretamente**

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

## 📋 Convenção de Arquivos em /docs

**SEMPRE usar formato de prefixo cronológico para novos arquivos em `docs/`:**

```
Format: YYYYMMDD_NN_NOME_DO_ARQUIVO.md
Exemplo: 20260103_09_DATABASE_INITIALIZATION.md
```

**Regras:**
1. **YYYYMMDD** = data de criação (ISO 8601)
2. **NN** = número sequencial do dia (01, 02, 03, ...)
   - Se múltiplos arquivos no mesmo dia, incrementar sequencialmente
   - Primeiro arquivo do dia = _01_, segundo = _02_, etc.
3. **NOME_DO_ARQUIVO** = descrição clara em UPPER_SNAKE_CASE

**Exemplos:**
- ✅ `20260102_DOCKER_CONFIGURATION.md` (primeiro arquivo de 2026-01-02)
- ✅ `20260103_01_USER_MANAGEMENT_EXAMPLES.md` (primeiro de 2026-01-03)
- ✅ `20260103_09_DATABASE_INITIALIZATION.md` (nono de 2026-01-03)
- ❌ `DOCKER_CONFIGURATION.md` (sem prefixo - ERRADO)
- ❌ `Docker-Config.md` (sem YYYYMMDD - ERRADO)

**Benefícios:**
- 📁 Pasta docs fica automaticamente **ordenada cronologicamente**
- 🔍 Fácil encontrar documentos recentes (olhar fim da lista)
- 📊 Histórico visual de evolução do projeto
- 🔗 Relacionamento entre docs fica claro (mesma data = mesmo contexto)

**Implementação ao criar novo arquivo:**

```
Sempre fazer assim:
1. Determinar data: TODAY_DATE (use current date)
2. Contar arquivos da mesma data em docs/
3. Incrementar NN: _01, _02, _03, etc.
4. Criar arquivo com padrão: YYYYMMDD_NN_NOME.md
5. Fazer commit documentando o padrão
```

**Atualizações de arquivos antigos:**
- Se atualizar arquivo antigo: **NÃO renomear**
- Usar data original + manter NN
- Exemplo: atualizar `20260102_DOCKER_CONFIGURATION.md` mantém nome igual
