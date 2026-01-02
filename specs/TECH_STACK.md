
# Technology Stack — MetricsSimple v1.1

Este documento define a **stack tecnológica obrigatória** do projeto.
Qualquer desvio é considerado **fora de escopo**.

---

## Backend

- Linguagem: **C#**
- Runtime: **.NET 8.x**
- API: **ASP.NET Core Minimal API**
- Runner: **Console Application (CLI)**
- Execução: **SÍNCRONA**
- Persistência: **SQLite local**
- Validação: **NJsonSchema**
- Logs: **Serilog (JSON estruturado)**
- IA (design-time):
  - Interface `IAiProvider`
  - Apenas geração assistida de DSL
  - Nunca usada em runtime

🚫 Proibido:
- Python
- Node.js no backend
- Azure Functions
- Filas / Workers
- Execução assíncrona
- Application Insights / Azure Monitor

---

## Frontend

- UI: **Material Design 3**
- Tipo: **SPA**
- Integração: REST (OpenAPI shared)
- Formulários: **100% guiados pelo ui-field-catalog**

🚫 Proibido:
- Frameworks fora dos guidelines do Material 3
- Campos fora do field catalog

---

## Infraestrutura (v1.x)

- Execução local / VM / IIS
- Armazenamento:
  - Arquivo local
  - Azure Blob Storage (opcional)
- Observabilidade:
  - Logs estruturados
  - Sem APM

---

## Princípios não negociáveis
- Determinismo
- Reprodutibilidade
- Auditabilidade
- Spec-Driven Development
