# Scope — v1.0

## What this spec deck defines
This spec deck is the single source of truth for implementing MetricsSimple v1.0.

### Runtime execution (sync only)
- Runner is invoked via CLI and executes synchronously.
- No queues, no background jobs, no schedulers.

### Design-time features
- UI Studio for configuration and preview.
- LLM-assisted DSL generation (suggestions only).

## What is NOT allowed in v1.0
- Azure Functions
- Message queues (Service Bus, Storage Queues, RabbitMQ, etc.)
- Any async job orchestration


A stack obrigatória está definida em `TECH_STACK.md`.
