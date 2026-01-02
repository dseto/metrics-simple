# Golden Tests (Transform + CSV)

Data: 2026-01-01

Casos mínimos:
- quoting (vírgula/aspas/newline)
- nulls
- schema inválido
- output inválido (validation fail)
- determinismo (mesmo output => mesmo csv)

## Suite canônica
- YAML: `../05-transformation/unit-golden-tests.yaml`
- Formato: `../05-transformation/golden-test-format.md`
- Fixtures: `../05-transformation/fixtures/*`

