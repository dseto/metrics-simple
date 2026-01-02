# UI — AI Assistant (Gerar DSL/Schema)

Data: 2026-01-02

Objetivo: permitir que o usuário descreva o CSV em linguagem natural e gere uma sugestão de **DSL** e **Output Schema**.

## Ponto de entrada
Tela **Process Version Editor** (ou modal) com aba/painel: **AI Assistant**.

## Campos (UI)
- `sampleInputText` (textarea): JSON de exemplo colado pelo usuário.
- `goalText` (textarea): “quero CSV com ...”
- `dslProfile` (select): `jsonata` (default) / `jmespath`
- `hints` (opcional)

> Antes de chamar a API, a UI faz `JSON.parse(sampleInputText)` e envia como `sampleInput` (objeto).

## Fluxo
1) Usuário cola `sampleInputText` (exemplo da API de origem)  
2) Usuário escreve `goalText`  
3) Seleciona `dslProfile` (default `jsonata`)  
4) Clica **Generate**  
5) UI chama `POST /api/ai/dsl/generate`  
6) UI exibe:
   - DSL sugerida (read-only com botão "Copy")
   - Schema sugerido (read-only)
   - warnings + rationale
7) Usuário clica **Apply** → UI preenche campos oficiais do Version Editor (`dsl` + `outputSchema`)  
8) Usuário salva versão via endpoints normais de versions.

## Estados (state machine)
- idle
- generating (loading)
- generated (success)
- failed (error)
- disabled (AI desabilitada)

## Tratamento de erro
- 503 `AI_DISABLED`: banner “IA desabilitada nesta instalação”.
- 503 `AI_PROVIDER_UNAVAILABLE`: “Serviço de IA indisponível”.
- 400 validação: apontar campos inválidos.
