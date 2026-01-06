# Prompt (GitHub Copilot) — Refactor routing para Multi-Engine (legacy + plan_v1) no MESMO endpoint

## Precedência
- Este prompt tem prioridade sobre qualquer agente base que esteja ativo.
- NÃO misture este prompt com outros prompts de tarefa ao mesmo tempo.
- Escopo fechado: backend apenas.

## Objetivo
Manter o motor atual **intacto** (legacy Jsonata/LLM) e adicionar suporte a um **novo motor** `plan_v1` (IR/executor determinístico), selecionável no **mesmo endpoint** `/api/v1/ai/dsl/generate` via campo opcional `engine`.

## Requisitos de compatibilidade (não quebrar)
- Nenhuma mudança no comportamento do `engine=legacy`.
- Se `engine` não vier no request, use o default via configuração (inicialmente `legacy`).
- Todos os logs/correlationId existentes devem continuar funcionando.

## Mudança incremental no request
Adicionar campo opcional no DTO (ou model) do endpoint:
- `engine`: `"legacy" | "plan_v1" | "auto"`
- `includePlan`: boolean opcional (default false) — quando true, incluir o `plan` no response (apenas plan_v1).

Importante:
- NÃO remover campos existentes (goal, sampleInput, dslProfile etc.).
- Se já existir um `dslProfile`, ele continua sendo aceito; o plan_v1 deve ignorar `dslProfile` ou validar que é compatível.

## Arquitetura alvo (Strategy)
Criar interface de engine:
- `IAiTransformationEngine` com método principal algo como:
  - `Task<AiDslGenerationResult> GenerateAsync(AiDslGenerationRequest request, CancellationToken ct)`
Ou equivalente ao seu padrão atual.

Criar implementações:
1) `LegacyAiDslEngine` — encapsula o fluxo existente (prompt system, parser, retry, bad patterns, templates legacy se existirem).
2) `PlanV1AiEngine` — por enquanto retorna `NotImplemented` (stub) com status controlado (ou cai para legacy se `auto`).

Criar roteador:
- `AiEngineRouter` (ou similar) responsável por selecionar a engine por:
  - request.engine (se vier)
  - config default (se não vier)
  - modo `auto`: decide com base em heurística simples (ver abaixo).

## Heurística do modo auto (MVP)
- Se goal indicar operações comuns (extract/select, rename, filter, sort, group/sum/avg/count) e sampleInput tiver um array candidato claro -> escolher `plan_v1`.
- Caso contrário -> legacy.
(Neste prompt, implemente apenas a estrutura; a heurística pode ser simples e refinada depois.)

## Contrato de resposta
- Resposta deve manter os campos existentes do endpoint.
- Para `plan_v1`:
  - Pode preencher `dsl.text` com vazio ou um placeholder (ex.: `"<plan_v1>"`) para não quebrar front.
  - Se `includePlan=true`, incluir campo adicional `plan` com o plano gerado/executado.
- Para `legacy`:
  - Resposta igual ao que já era.

## Logging e métricas
- Logar qual engine foi escolhida:
  - `EngineSelected=legacy|plan_v1|auto->...`
- Incluir correlationId/requestId como já existe.

## Tarefas de implementação (passo a passo)
1) Localize o endpoint `/api/v1/ai/dsl/generate` e identifique o request/response models atuais.
2) Adicione campo `engine` e `includePlan` no request model, mantendo backward compatibility.
3) Introduza `IAiTransformationEngine`, `LegacyAiDslEngine`, `PlanV1AiEngine`, `AiEngineRouter`.
4) Refatore o endpoint para chamar o router e delegar para a engine selecionada.
5) Garanta que `engine=legacy` reproduz exatamente o comportamento anterior (mesmos status codes e payloads).
6) Adicione testes unitários mínimos do router (seleção legacy vs plan_v1 vs auto).
7) Não implemente a lógica completa do plan_v1 aqui — apenas stub com TODO e retorno claro (ou fallback para legacy quando auto).

## Critérios de aceite
- Build OK
- IT10/IT11/IT12 continuam passando
- Endpoint compila e responde com `engine=legacy` como antes
- `engine=plan_v1` retorna resposta stub consistente (ex.: 501/400 com mensagem "plan_v1 not implemented") **sem afetar legacy**
