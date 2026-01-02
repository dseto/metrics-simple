# Plano de Execução: Implementação do Backend "Metrics Simple"

Este plano detalha as etapas para transformar o *skeleton* atual em um backend funcional e robusto, seguindo a metodologia **Spec-Driven Development (SDD)** e as instruções de governança fornecidas.

## 1. Fase de Fundação e Infraestrutura
O objetivo é estabelecer a base de dados e a gestão de segredos conforme as especificações.

| Atividade | Descrição | Spec de Referência |
| :--- | :--- | :--- |
| **Setup SQLite** | Criar o banco `config.db` e as tabelas `Process`, `ProcessVersion` e `Connector` usando Dapper ou EF Core (conforme preferência do skeleton). | `specs/02-domain/sqlite-schema.md` |
| **Secrets Provider** | Implementar o leitor de segredos a partir do `secrets.local.json`. | `specs/08-security/local-secrets-policy.md` |
| **Modelagem de DTOs** | Gerar classes C# a partir dos JSON Schemas fornecidos. | `specs/02-domain/schemas/*.json` |

## 2. Implementação da Engine (Coração do Projeto)
A Engine é responsável pela transformação de dados e validação de contratos.

1.  **Integração Jsonata:** Adicionar biblioteca para suporte a Jsonata (ex: `Jsonata.Net.Native`).
2.  **Validador de Schema:** Implementar validação de saída usando `Newtonsoft.Json.Schema` ou `JsonSchema.Net`.
3.  **Gerador de CSV:** Implementar conversão de JSON Array para CSV.
4.  **Golden Tests:** Implementar o `GoldenTestsSkeleton.cs` para ler o `unit-golden-tests.yaml` e validar a Engine automaticamente.

## 3. Desenvolvimento da API de Configuração
Implementação do serviço web para gestão dos processos.

*   **Controllers:** Implementar endpoints seguindo o `openapi-config-api.yaml`.
*   **Preview Service:** Implementar a lógica de `/preview/transform` que executa a Engine em memória sem persistência.
*   **Validação de Contrato:** Garantir que as respostas da API correspondam exatamente ao OpenAPI spec.

## 4. Implementação do Runner CLI e Pipeline
O Runner é o componente que executa o trabalho pesado de forma síncrona.

*   **Pipeline Orchestrator:** Implementar a sequência de 8 passos definida em `specs/04-execution/pipeline-spec.md`.
*   **HTTP Client:** Implementar o fetch de dados externos usando as configurações do `Connector` e `SourceRequest`.
*   **Exit Codes:** Garantir que cada falha retorne o código correto (ex: 40 para falha de schema, 50 para erro de storage).
*   **Cleanup Command:** Implementar a lógica de retenção de arquivos locais.

## 5. Storage e Observabilidade
Finalização das integrações externas e monitoramento.

*   **Azure Blob Provider:** Implementar o upload de CSVs e Logs para o Azure Blob Storage.
*   **Serilog Enrichment:** Configurar o Serilog para incluir `executionId`, `processId` e gerar logs em formato JSONL.
*   **Correlation:** Garantir que o `executionId` seja o prefixo de todos os artefatos gerados.

## 6. Estratégia de Testes Robustos
Para garantir a qualidade, seguiremos uma pirâmide de testes adaptada ao SDD:

| Tipo de Teste | Foco | Ferramenta |
| :--- | :--- | :--- |
| **Unitários (Engine)** | Lógica de transformação e DSL. | xUnit + Golden Files |
| **Contrato (API)** | Validação de Schemas de entrada/saída. | xUnit + JsonSchema |
| **Funcionais (Runner)** | Execução do pipeline completo com mocks de API. | xUnit + WireMock.Net |
| **CLI (E2E Lite)** | Validação de Exit Codes e geração de arquivos. | Scripts de teste (PowerShell/Bash) |

## 7. Cronograma Sugerido (Sprints)
*   **Sprint 1:** Fundação + Engine + Golden Tests.
*   **Sprint 2:** API de Configuração + Preview.
*   **Sprint 3:** Runner CLI + Pipeline + Storage.
*   **Sprint 4:** Refinamento de Observabilidade + Bateria Final de Testes.

---
Este plano garante que cada linha de código escrita tenha uma justificativa direta nas especificações, minimizando desvios e garantindo a entrega de um sistema confiável.
