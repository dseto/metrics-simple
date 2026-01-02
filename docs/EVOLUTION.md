# Evolution Guide (Specs & Repo Sync)

Data: 2026-01-02

Este documento define **como evoluir o spec deck** sem quebrar testes/automação e sem criar drift entre specs e implementação.

## Quando você mover/renomear arquivos em `specs/`
Sempre execute este checklist **no mesmo PR**:

1. Atualize **todas** as referências:
   - `.csproj` (itens `None/Content` que copiam specs para `bin/`)
   - docs e links internos (`spec-index.md`, READMEs, etc.)
2. Atualize os manifests:
   - `specs/shared/spec-manifest.json`
   - `spec-deck-manifest.json`
3. Rode validação local:
   - `dotnet build`
   - `dotnet test`
4. (Recomendado) Rode um validador estrutural:
   - verifica se todos os paths do manifest existem
   - verifica se checksums batem
   - verifica se links internos não apontam para paths inexistentes

## Padrão de commits para refactor estrutural
Use uma mensagem clara e única, por exemplo:
- `refactor(specs): move shared schemas to specs/shared/domain/schemas`

E inclua no PR:
- antes/depois do layout
- motivo da mudança
- impacto (breaking ou não)

## Regra de ouro
**Não aceite PRs** que alteram a estrutura de `specs/` sem:
- atualizar `.csproj` e/ou scripts que copiam specs
- atualizar manifests
- build/test verde
