
# Repository Contracts

Data: 2026-01-01

Interfaces mínimas (SQLite).

## Connectors
- Get(id)
- List()
- Create()

## Processes
- Get(id)
- List()
- Create()
- Update()
- Delete() (bloquear se tiver versions)

## Versions
- Get(processId, version)
- Create()
- Update()
- ListByProcess() (opcional)

Regras:
- repositório não “corrige” dados inválidos
- validações de domínio em services
