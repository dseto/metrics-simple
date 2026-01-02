# Scope — v1.2

## What this spec deck defines
This spec deck is the single source of truth for implementing MetricsSimple v1.x.

### Runtime execution (sync only)
- Runner is invoked via CLI and executes synchronously.
- No queues, no background jobs, no schedulers.

### Design-time features
- UI Studio for configuration and preview.
- LLM-assisted DSL generation (suggestions only).

### Testing (mandatory)
- Contract tests (OpenAPI + schemas + DTOs)
- Golden tests (Jsonata real + schema + CSV RFC4180)
- Integration tests (E2E) using WebApplicationFactory + mock HTTP for FetchSource + SQLite + runner execution

## What is NOT allowed in v1.x
- Azure Functions
- Message queues (Service Bus, Storage Queues, RabbitMQ, etc.)
- Any async job orchestration

A stack obrigatória está definida em `TECH_STACK.md`.
