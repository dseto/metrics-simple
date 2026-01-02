# Resumo Técnico - Metrics Simple Backend

## Visão Geral

O backend **Metrics Simple** é uma aplicação .NET 8 que implementa um pipeline de transformação de dados baseado em **Spec-Driven Development (SDD)**. A aplicação permite configurar, validar e executar transformações de dados usando DSL (Domain Specific Language) baseada em Jsonata.

## Arquitetura em Camadas

```
┌─────────────────────────────────────────────────────────┐
│                   Presentation Layer                     │
│  ┌──────────────────────┐  ┌──────────────────────────┐ │
│  │   API REST (ASP.NET) │  │   CLI (System.CommandLine)│ │
│  └──────────────────────┘  └──────────────────────────┘ │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                  Business Logic Layer                    │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Engine Service (Transform, Validate, Generate)  │   │
│  │  Pipeline Orchestrator (8-step execution)        │   │
│  └──────────────────────────────────────────────────┘   │
└────────────────────┬────────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────────┐
│                  Data Access Layer                       │
│  ┌──────────────────────────────────────────────────┐   │
│  │  Process Repository      (SQLite)                │   │
│  │  ProcessVersion Repository (SQLite)              │   │
│  │  Connector Repository    (SQLite)                │   │
│  │  Storage Provider        (Local/Blob)            │   │
│  │  Secrets Provider        (JSON)                  │   │
│  └──────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────┘
```

## Componentes Principais

### 1. Engine Service
**Responsabilidade:** Orquestrar a transformação, validação e geração de CSV

**Classes:**
- `EngineService`: Orquestrador principal
- `JsonataTransformer`: Implementa transformação Jsonata
- `SchemaValidator`: Valida saída contra JSON Schema
- `CsvGenerator`: Converte JSON Array em CSV

**Interfaces:**
- `IDslTransformer`: Contrato para transformadores
- `ISchemaValidator`: Contrato para validadores
- `ICsvGenerator`: Contrato para geradores CSV

### 2. API REST
**Responsabilidade:** Expor endpoints para gerenciamento de configuração

**Endpoints:**
- `GET/POST /api/processes` - Listar/criar processos
- `GET/PUT/DELETE /api/processes/{id}` - CRUD de processo
- `POST/GET/PUT /api/processes/{processId}/versions/{version}` - Gerenciar versões
- `GET/POST /api/connectors` - Gerenciar conectores
- `POST /api/preview/transform` - Preview de transformação

**Repositórios:**
- `ProcessRepository`: CRUD para Process
- `ProcessVersionRepository`: Gerenciamento de versões
- `ConnectorRepository`: Gerenciamento de conectores

### 3. Runner CLI
**Responsabilidade:** Executar pipeline de transformação via linha de comando

**Comandos:**
- `run`: Executa pipeline completo (8 passos)
- `validate`: Valida DSL e schema
- `cleanup`: Retenção/limpeza de arquivos

**Exit Codes:**
- `0`: Sucesso
- `10`: Config inválida
- `20`: Falha API externa
- `30`: Falha transformação
- `40`: Schema inválido
- `50`: Falha escrita CSV
- `60`: Falha logs
- `99`: Erro inesperado

### 4. Pipeline Orchestrator
**Responsabilidade:** Orquestrar os 8 passos do pipeline de execução

**Passos:**
1. Carregar configuração do SQLite
2. Carregar secrets
3. Buscar dados da API externa
4. Executar transformação DSL
5. Validar schema de saída
6. Gerar CSV
7. Salvar CSV (Local/Blob)
8. Salvar logs

## Fluxos de Dados

### Fluxo de Design (API)
```
User → API → ProcessRepository → SQLite
         ↓
    Engine Service
         ↓
    PreviewTransformResponse
```

### Fluxo de Execução (Runner)
```
CLI → PipelineOrchestrator
    ↓
1. Load Config (SQLite)
2. Load Secrets (JSON)
3. Fetch Data (HTTP)
4. Transform (Jsonata)
5. Validate (JSON Schema)
6. Generate CSV
7. Save CSV (Local/Blob)
8. Save Logs
    ↓
Exit Code
```

## Modelo de Dados

### Process
```csharp
record ProcessDto(
    string Id,
    string Name,
    string Status,
    string ConnectorId,
    List<OutputDestinationDto> OutputDestinations
);
```

### ProcessVersion
```csharp
record ProcessVersionDto(
    string ProcessId,
    string Version,
    bool Enabled,
    SourceRequestDto SourceRequest,
    DslDto Dsl,
    object OutputSchema,
    object? SampleInput = null
);
```

### Connector
```csharp
record ConnectorDto(
    string Id,
    string Name,
    string BaseUrl,
    string AuthRef,
    int TimeoutSeconds
);
```

## Tecnologias Utilizadas

| Aspecto | Tecnologia |
|--------|-----------|
| Runtime | .NET 8.0 |
| Web Framework | ASP.NET Core Minimal APIs |
| Database | SQLite 8.0.11 |
| Transformação | Jsonata (NJsonSchema 11.0.2) |
| Validação | NJsonSchema 11.0.2 |
| CLI | System.CommandLine 2.0.0-beta4 |
| Logging | Serilog 4.1.0 |
| Storage | Azure.Storage.Blobs 12.21.2 |
| Testes | xUnit 2.9.2 |
| API Docs | Swashbuckle.AspNetCore 6.5.0 |

## Padrões de Design

### 1. Repository Pattern
Abstração de acesso a dados através de interfaces:
- `IProcessRepository`
- `IProcessVersionRepository`
- `IConnectorRepository`

### 2. Dependency Injection
Injeção de dependências via DI container:
- ASP.NET Core DI para API
- Injeção manual para Runner

### 3. Strategy Pattern
Diferentes estratégias de transformação:
- `IDslTransformer` (implementação: Jsonata)
- `ISchemaValidator` (implementação: NJsonSchema)
- `ICsvGenerator` (implementação: CsvGenerator)

### 4. Pipeline Pattern
Orquestração de múltiplos passos:
- `PipelineOrchestrator` com 8 passos sequenciais

## Testes

### Golden Tests (Engine)
Testes que validam a transformação contra casos de uso reais:
- `TestHostsCpuTransform`: Transformação com validação de schema
- `TestSimpleArrayTransform`: Transformação simples
- Validação de CSV output

### Contract Tests (API)
Testes que validam a estrutura de DTOs conforme OpenAPI spec:
- Validação de propriedades obrigatórias
- Validação de tipos de dados
- Validação de relacionamentos

## Observabilidade

### Logging Estruturado
- Serilog com output em JSONL
- Enriquecimento com `executionId` para correlação
- Logs em arquivo e console

### Contexto de Execução
Cada execução é rastreada com:
- `executionId`: UUID único
- `processId`: ID do processo
- `version`: Versão do processo
- `timestamp`: Timestamp da execução

## Segurança

### Gerenciamento de Secrets
- Arquivo `secrets.local.json` para desenvolvimento
- Suporte a variáveis de ambiente para produção
- Secrets provider com abstração de acesso

### Validação de Entrada
- Validação de schema JSON
- Validação de DSL Jsonata
- Tratamento de erros robusto

## Performance

### Otimizações
- SQLite em-memory para testes
- Lazy loading de configurações
- Streaming de CSV para grandes datasets
- Connection pooling para HTTP

### Escalabilidade
- Suporte a Azure Blob Storage para saída
- Logging assíncrono
- Processamento paralelo de múltiplas execuções

## Configuração

### appsettings.json
```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "./logs/api-.jsonl" } }
    ]
  },
  "Database": { "Path": "./config/config.db" },
  "Secrets": { "Path": "./config/secrets.local.json" }
}
```

### secrets.local.json
```json
{
  "connectors": {
    "connector-id": {
      "token": "secret-token"
    }
  }
}
```

## Próximos Passos

1. **Autenticação**: Implementar JWT/OAuth
2. **Autorização**: RBAC (Role-Based Access Control)
3. **Caching**: Redis para cache distribuído
4. **Monitoramento**: Application Insights
5. **CI/CD**: GitHub Actions / Azure DevOps
6. **Containerização**: Docker / Kubernetes
7. **Documentação**: Swagger/OpenAPI completo

---

*Última atualização: 01 de Janeiro de 2026*
