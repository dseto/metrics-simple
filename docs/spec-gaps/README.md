# Spec Gaps e Descobertas de Implementação

**Objetivo**: Rastrear descobertas, limitações e decisões de implementação que não foram claramente documentadas no spec deck original e que precisam ser integradas para futuras evoluções.

## Formato de documentação

Cada spec gap deve ser documentado em arquivo separado seguindo o padrão:

```
YYYYMMDD_NN_TITULO_SPEC_GAP.md
```

### Estrutura obrigatória:

```markdown
# [Título do Spec Gap]

**Data de Descoberta**: YYYY-MM-DD
**Arquivo Spec Relevante**: `specs/backend/XX-topic/file.md` (ou N/A se não existe)
**Prioridade**: [CRÍTICO | ALTO | MÉDIO | BAIXO]
**Status**: [DESCOBERTO | PROPOSTO | INTEGRADO | REJEITADO]
**Área**: [Backend | Frontend | Infra | Shared]

## Contexto

[Explicação do que foi descoberto durante implementação]

## Gap Identificado

[Por que não estava claro na spec]

## Recomendação para Spec Deck

[O que deveria estar documentado]

## Exemplo de Código/Padrão

[Code snippet ou diagrama exemplificando]

## Status de Integração

- [ ] Atualizar spec relevante
- [ ] Notificar spec owner
- [ ] Revisar outras áreas afetadas

## Notas

[Anotações adicionais]
```

## Índice de Spec Gaps

| Arquivo | Título | Spec Relevante | Status | Prioridade |
|---------|--------|---|--------|-----------|
| [20260105_01_SQLITE_SCHEMA_MIGRATIONS.md](20260105_01_SQLITE_SCHEMA_MIGRATIONS.md) | SQLite Schema Migrations não Documentadas na Spec | `specs/backend/06-storage/sqlite-schema.md` | DESCOBERTO | ALTO |

---

## Processo para o agente `spec-driven-builder`

### Ao descobrir um spec gap:

1. **Identifique durante implementação** se algo está ambíguo, não documentado ou contradiz a spec
2. **CRIE documento em `docs/spec-gaps/`** antes de inventar solução
3. **Use padrão**: `YYYYMMDD_NN_TITULO.md`
4. **Documente**:
   - Contexto: o que estava fazendo
   - Gap: por que spec foi insuficiente
   - Recomendação: como melhorar a spec
   - Código: exemplo de solução implementada
5. **ATUALIZE índice** em `README.md`
6. **Não invente**: se spec tem `...`, registre gap ANTES de implementar

### Prioridades

| Nível | Descrição |
|-------|-----------|
| **CRÍTICO** | Spec faltando completamente; afeta contrato/API |
| **ALTO** | Comportamento não especificado claramente; causa ambiguidade |
| **MÉDIO** | Detalhes de implementação; edge cases não mencionados |
| **BAIXO** | Nice-to-have; documentação de padrões descobertos |

### Após documentar o gap:

- **PROPOSTO**: Aguardando review de spec owner
- **INTEGRADO**: Mudanças mergeadas na spec
- **REJEITADO**: Spec está correta, implementação apenas diferente

### Revisar regularmente

- **After each feature**: Verificar se houve gaps
- **Before release**: Sync gaps CRÍTICO/ALTO com spec team
- **Quarterly**: Integrar aprendizados acumulados na spec deck

---

## Relacionamento com Tech Debt

**Diferença importante**:

| Tipo | Foco | Local | Ação |
|------|------|-------|------|
| **Tech Debt** | Código/implementação que precisa refatoração | `docs/tech-debt/` | Fix direto ou backlog |
| **Spec Gap** | Spec deck incompleto/ambíguo | `docs/spec-gaps/` | Propor mudança na spec |

Exemplo:
- ❌ **NOT a gap**: "Refatorar SQLite reader pattern" → vai em tech-debt/
- ✅ **IS a gap**: "Spec não documenta como fazer migrations de schema" → vai em spec-gaps/

---

Generated: 2026-01-05
