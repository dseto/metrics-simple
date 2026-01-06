# SQLite Schema Versioning System

**Data de Criação**: 2026-01-05
**Severity**: MÉDIO
**Status**: PENDENTE
**Área**: Backend | DevOps

## Descrição

Implementar um sistema automático de versioning de schema que detecta mudanças na estrutura do banco de dados (novas colunas, índices, etc) e executa `ALTER TABLE` automaticamente durante a inicialização da aplicação.

## Por que é um débito?

Atualmente, o schema é inicializado via `CREATE TABLE IF NOT EXISTS` no `DatabaseProvider.InitializeDatabase()`. Esta abordagem tem limitações:

1. **Não adiciona colunas**: Se adicionarmos uma coluna à tabela existente, a query `CREATE TABLE IF NOT EXISTS` não faz nada
2. **Não remove colunas**: Não há mecanismo para cleanup de colunas obsoletas
3. **Sem índices incrementais**: Não é possível adicionar índices dinamicamente
4. **DevOps pain**: Requer manual volume deletion em Docker (`Remove-Item src/Api/config/config.db`)
5. **Production risk**: Em produção, não há forma de rodar migrations sem downtime

**Workaround atual**: Quando schema muda, desenvolvedores precisam apagar manualmente o arquivo SQLite local e containers precisam ser recriados.

## Impacto

- **Hoje**: Mudanças de schema exigem manual DevOps (apagar DB, reiniciar containers)
- **Risco**: Fácil esquecer de deletar DB antigo → incompatibilidade silenciosa
- **Escalabilidade**: Não funciona em ambiente cloud (managed databases)

## Como resolver

### Solução recomendada: Schema Versioning

1. **Criar tabela `__SchemaVersion__`**:
   ```sql
   CREATE TABLE IF NOT EXISTS __SchemaVersion__ (
     id INTEGER PRIMARY KEY,
     version INTEGER NOT NULL,
     appliedAt TEXT NOT NULL,
     description TEXT
   )
   ```

2. **Implementar `SchemaManager.cs`**:
   ```csharp
   public class SchemaManager
   {
       private static readonly List<SchemaMigration> Migrations = new()
       {
           new SchemaMigration(1, "Initial schema", InitialSchema),
           new SchemaMigration(2, "Add Connector.authType", AddConnectorAuthType),
           new SchemaMigration(3, "Add indices", AddIndices),
       };
       
       public void ApplyPendingMigrations(SqliteConnection conn)
       {
           var currentVersion = GetCurrentVersion(conn);
           var pending = Migrations.Where(m => m.Version > currentVersion);
           
           foreach (var migration in pending)
           {
               ExecuteMigration(conn, migration);
           }
       }
   }
   ```

3. **Registrar no `Program.cs`**:
   ```csharp
   var schemaManager = new SchemaManager();
   schemaManager.ApplyPendingMigrations(connection);
   ```

4. **Benefícios**:
   - ✅ Adiciona/remove colunas automaticamente
   - ✅ Garante schema sempre atualizado
   - ✅ Histórico de mudanças
   - ✅ Funciona em cloud/produção
   - ✅ Zero downtime (se bem desenhado)

### Alternativa: Entity Framework Core Migrations

Se já existir EF Core no projeto, usar `dotnet ef migrations` é mais padronizado.

## Dependências

- Nenhuma bloqueadora — pode ser feito incremental
- Não afeta código existente (apenas adiciona sistema de versioning)

## Estimativa

- Sistema básico: 4h
- Testes: 2h
- Documentação: 1h
- **Total**: 1 dia

## Notas e Progresso

**2026-01-05**: 
- Identificado durante análise de erro 500 (stale schema in Docker)
- Problema: `CREATE TABLE IF NOT EXISTS` não faz ALTER TABLE
- Workaround documentado: apagar manualmente `config.db`
- **Status**: Aguardando implementação em próximo sprint

**Próximos passos**:
1. Definir estrutura de migrations (SQL puro vs C# helpers)
2. Implementar `SchemaManager`
3. Testar com múltiplas mudanças de schema
4. Atualizar documentação de deployment
5. Remover necessidade manual de apagar DB
