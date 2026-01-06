# Débitos Técnicos e Melhorias Pendentes

**Objetivo**: Manter um radar centralizado de débitos técnicos, melhorias pendentes e refatorações que precisam ser feitas, mas que não são bloqueadores imediatos.

## Formato de documentação

Cada débito técnico deve ser documentado em um arquivo separado seguindo o padrão:

```
YYYYMMDD_NN_TITULO_DEBITO.md
```

### Estrutura obrigatória de cada documento:

```markdown
# [Título do Débito]

**Data de Criação**: YYYY-MM-DD
**Severity**: [CRÍTICO | ALTO | MÉDIO | BAIXO]
**Status**: [PENDENTE | EM_PROGRESSO | BLOQUEADO | DONE]
**Área**: [Backend | Frontend | Infra | Tests | DevOps | Outro]

## Descrição

[Explicação clara do débito técnico]

## Por que é um débito?

[Por que não foi implementado da forma ideal]

## Impacto

- [Item 1]
- [Item 2]
- [Item 3]

## Como resolver

[Passos ou direção para resolução]

## Dependências

- [Se houver outras tarefas bloqueando]

## Estimativa

[Effort estimate: 1h | 4h | 1d | 1w | TBD]

## Notas e Progresso

[Anotações, atualizações de progresso]
```

## Índice de Débitos

| Arquivo | Título | Severity | Status | Área |
|---------|--------|----------|--------|------|
| [20260105_01_SQLITE_READER_DISPOSAL_PATTERN.md](20260105_01_SQLITE_READER_DISPOSAL_PATTERN.md) | SQLite Reader Disposal Pattern | ALTO | DONE | Backend |
| [20260105_02_SQLITE_SCHEMA_VERSIONING_SYSTEM.md](20260105_02_SQLITE_SCHEMA_VERSIONING_SYSTEM.md) | SQLite Schema Versioning System | MÉDIO | PENDENTE | Backend / DevOps |

---

## Guia de Uso

### Para o agente `spec-driven-builder`:

1. **Ao identificar um débito técnico durante implementação**:
   - Crie arquivo em `docs/tech-debt/` com padrão de data
   - Use severity apropriada (ver níveis abaixo)
   - Atualize a tabela de índice

2. **Ao resolver um débito**:
   - Mude status para `DONE`
   - Adicione `commit hash` na seção de notas
   - Mantenha no índice como histórico

3. **Para priorizar revisão**:
   - Ordenar por Severity (CRÍTICO → BAIXO)
   - Ordenar por Status (BLOQUEADO → PENDENTE)

### Níveis de Severity

| Nível | Descrição |
|-------|-----------|
| **CRÍTICO** | Afeta disponibilidade, segurança ou dados; bloqueador de release |
| **ALTO** | Afeta performance, UX ou causa bugs com frequência |
| **MÉDIO** | Refatoração necessária; manutenibilidade afetada |
| **BAIXO** | Melhorias cosméticas; nice-to-have |

### Revisar regularmente

- **Weekly**: Verificar débitos CRÍTICO/ALTO
- **Monthly**: Refatorar MÉDIO/BAIXO quando houver tempo livre
- **Release planning**: Considerar débitos CRÍTICO/ALTO para próxima sprint

---

Generated: 2026-01-05
