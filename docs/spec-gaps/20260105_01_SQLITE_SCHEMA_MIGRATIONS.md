# SQLite Schema Migrations não Documentadas na Spec

**Data de Descoberta**: 2026-01-05
**Arquivo Spec Relevante**: `specs/backend/06-storage/sqlite-schema.md`
**Prioridade**: ALTO
**Status**: DESCOBERTO
**Área**: Backend | Infra

## Contexto

Durante implementação e debugging do erro 500 em `/api/v1/connectors`, descobrimos que:

1. Schema SQLite foi alterado (v1.1 → v1.2) para adicionar coluna `authType` na tabela `Connector`
2. Aplicação usa `CREATE TABLE IF NOT EXISTS` no `DatabaseProvider.InitializeDatabase()`
3. Em Docker, com volume local persistido, a coluna nova NÃO era adicionada automaticamente
4. Resultado: aplicação tentava acessar `authType` mas coluna não existia → erro SQL

## Gap Identificado

A spec `sqlite-schema.md` não documenta:

1. **Como fazer migrations de schema**: Apenas descreve schema final, não processo de evolução
2. **Estratégia de versionamento**: Não há tabela de versão ou mecanismo de controle
3. **Comportamento de `CREATE TABLE IF NOT EXISTS`**: Não altera tabelas existentes
4. **Handling em Docker**: Não especifica como DB persiste em volume local
5. **Procedure quando schema muda**: Desenvolvedores/DevOps ficam sem guia

## Recomendação para Spec Deck

Adicionar seção em `specs/backend/06-storage/sqlite-schema.md`:

### Section: Schema Versioning & Migrations

```markdown
## Schema Versioning

### Current State (v1.2.0)
[incluir schema SQL completo com versão]

### Migration Strategy

The application uses `CREATE TABLE IF NOT EXISTS` for initial schema creation.
However, schema **evolves** — adding columns, indices, or constraints.

**Process for schema changes**:

1. **Create new migration**: Update schema SQL version
2. **Implement automatic migration**: DatabaseProvider should detect version and apply ALTER TABLE
3. **Document in this file**: Every schema version should be tracked
4. **Testing**: Integration tests must validate schema compatibility

### Example: Adding a Column

If you add `Connector.newestField: TEXT`:

1. Update `DatabaseProvider.cs` schema to include new column
2. Implement `SchemaManager` to execute:
   ```sql
   ALTER TABLE Connector ADD COLUMN newestField TEXT;
   ```
3. Register migration in version tracking
4. Update this document with new schema version
5. Test: old DB should auto-migrate, new instances should initialize correctly

### Docker Behavior

Local volume mounts (`./src/Api/config:/app/config`) persist across container restarts.
When schema changes:
- Development: Delete old `config.db` and restart containers
- Production: Use automated migration system (SchemaManager or similar)

### Schema Versions

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-01-01 | Initial schema |
| 1.1.0 | 2026-01-03 | Added Connector.secretsJson |
| 1.2.0 | 2026-01-05 | Added Connector.authType |
```

## Exemplo de Código/Padrão

**Padrão que deveria estar na spec**:

```csharp
// DatabaseProvider.cs - Should be automated per spec
public class SchemaManager
{
    private const int CurrentSchemaVersion = 120; // 1.2.0
    
    public void ApplyMigrations(SqliteConnection connection)
    {
        EnsureVersionTable(connection);
        var currentVersion = GetCurrentVersion(connection);
        
        if (currentVersion < 110)
            MigrateToV110(connection);
        if (currentVersion < 120)
            MigrateToV120(connection);
            
        UpdateVersion(connection, CurrentSchemaVersion);
    }
    
    private void MigrateToV120(SqliteConnection conn)
    {
        // ALTER TABLE Connector ADD COLUMN authType TEXT NOT NULL DEFAULT 'NONE'
        ExecuteSql(conn, "ALTER TABLE Connector ADD COLUMN authType TEXT NOT NULL DEFAULT 'NONE'");
    }
}
```

## Status de Integração

- [ ] Atualizar `specs/backend/06-storage/sqlite-schema.md` com seção Schema Versioning
- [ ] Criar `specs/backend/06-storage/schema-migrations.md` (arquivo dedicado)
- [ ] Notificar spec owner (backend team)
- [ ] Revisar se outras specs (CLI, runner) precisam mencionar versionamento

## Notas

**2026-01-05 - Initial Discovery**:
- Erro descoberto durante Docker deployment
- Root cause: Stale DB schema (missing `authType` column)
- Workaround: Manual deletion de `config.db`
- **Raiz real**: Spec não documenta strategy para schema evolution

**Impacto de não ter isso na spec**:
- Cada desenvolvedor improvisa solução diferente
- DevOps não sabe quando/como apagar dados
- Production não tem migration path
- Novos features que mudam schema ficam bloqueadas

**Próximo release deve ter**:
- ✅ SchemaManager automático
- ✅ Versionamento claro em spec
- ✅ Testing de migrations
- ✅ Documentação de DevOps/Docker
