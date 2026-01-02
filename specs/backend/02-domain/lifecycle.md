
# Lifecycle (Backend)

Data: 2026-01-01

## Process status
Estados:
- Draft
- Active
- Disabled

Transições permitidas:
- Draft -> Active
- Active -> Disabled
- Disabled -> Active

Regras:
- `Active` significa “pode ser usado pelo Runner”.
- `Disabled` significa “não deve ser executado”.

## ProcessVersion enabled
- `enabled=true`: versão elegível para execução.
- `enabled=false`: versão não deve ser usada.

Política de seleção (Runner):
- Se o CLI receber `--version`, usar aquela version (se existir e enabled).
- Se não receber `--version`, usar a maior version enabled (regra em `../04-execution/cli-contract.md`).
