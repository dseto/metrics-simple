# Database Initialization Strategy

**Data:** 2026-01-03  
**Contexto:** Configuração do SQLite para desenvolvimento e produção

---

## 📊 Como Funciona Atualmente

### 1. **Inicialização Automática** ✅

No `src/Api/Program.cs` (linha 75-76):

```csharp
// Initialize database
var dbProvider = new DatabaseProvider();
dbProvider.InitializeDatabase(dbPath);
```

A função `InitializeDatabase()` em `src/Engine/DatabaseProvider.cs`:

1. **Cria diretório** se não existir:
   ```csharp
   var directory = Path.GetDirectoryName(dbPath);
   if (!string.IsNullOrEmpty(directory))
   {
       Directory.CreateDirectory(directory);  // ← Cria src/Api/config/ se necessário
   }
   ```

2. **Cria arquivo BD** automaticamente (SQLite):
   ```csharp
   var connectionString = $"Data Source={dbPath}";
   using var connection = new SqliteConnection(connectionString);
   connection.Open();  // ← Cria config.db se não existir
   ```

3. **Cria schema** (tabelas) com `CREATE TABLE IF NOT EXISTS`:
   ```sql
   CREATE TABLE IF NOT EXISTS Process (...)
   CREATE TABLE IF NOT EXISTS ProcessVersion (...)
   CREATE TABLE IF NOT EXISTS Connector (...)
   CREATE TABLE IF NOT EXISTS auth_users (...)
   CREATE TABLE IF NOT EXISTS auth_user_roles (...)
   -- E índices
   ```

### ✅ Resultado
Na primeira execução da API:
- `src/Api/config/` é criada automaticamente
- `config.db` é criado automaticamente
- **Todas as tabelas são criadas**
- Database está pronto para uso

---

## 🚀 Deploy em Produção

### Cenário 1: Docker (Recomendado)

No `compose.yaml`:
```yaml
services:
  csharp-api:
    image: metrics-simple-csharp-api:latest
    volumes:
      - db-volume:/app/data/db          # ← BD persiste entre restarts
      - config-volume:/app/config       # ← Config persiste
    environment:
      - METRICS_SQLITE_PATH=/app/data/db/production.db
```

**O que acontece:**
1. Container inicia
2. API startup executa `InitializeDatabase()`
3. BD é criada em `/app/data/db/production.db`
4. Volume Docker garante que BD persiste

✅ **Não precisa de script de migração!**

### Cenário 2: Deploy em Server (Linux/Windows)

**Estrutura de pastas:**
```
/app/metrics-simple/
├── bin/
│   └── api.dll
├── data/
│   └── db/                    # ← Criar essa pasta com permissões
│       └── production.db      # ← Será criada automaticamente
└── config/
    └── appsettings.json       # ← Com METRICS_SQLITE_PATH ou Database:Path
```

**Configuração (`appsettings.json`):**
```json
{
  "Database": {
    "Path": "/app/data/db/production.db"  // ← Absoluto ou relativo
  }
}
```

**Environment variable (alternativa, toma precedência):**
```bash
export METRICS_SQLITE_PATH=/app/data/db/production.db
dotnet Api.dll
```

✅ **API cria BD automaticamente na primeira execução!**

### Cenário 3: Azure App Service

```csharp
// Program.cs - appsettings.json
var dbPath = Environment.GetEnvironmentVariable("METRICS_SQLITE_PATH") 
    ?? builder.Configuration["Database:Path"] 
    ?? "./config/config.db";  // ← Default local
```

**Configuração recomendada:**
1. App Service → Configuration → Application settings
2. Adicionar: `METRICS_SQLITE_PATH` = `/home/site/wwwroot/data/production.db`
3. Ou usar Azure Blob Storage para BD (mudaria implementação)

✅ **BD criada automaticamente no primeiro request!**

---

## ⚠️ Considerações Importantes

### 1. **Permissões de Arquivo**

```bash
# Linux - Garantir que o app pode escrever na pasta
chmod 755 /app/data/db/
# Ou melhor: app user
chown appuser:appuser /app/data/db/
```

### 2. **Backup de Produção**

Como `.db` agora está em `.gitignore`, precisa de **backup automático**:

```bash
#!/bin/bash
# backup-db.sh (executar via cron)
BACKUP_DIR="/backups/metrics-simple"
DB_PATH="/app/data/db/production.db"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p $BACKUP_DIR
cp $DB_PATH "$BACKUP_DIR/production_$DATE.db"

# Manter últimos 30 dias
find $BACKUP_DIR -name "production_*.db" -mtime +30 -delete
```

### 3. **Verificação de Saúde**

Na startup, verificar se BD está acessível:

```csharp
// Program.cs - após InitializeDatabase
try 
{
    var testConn = dbProvider.GetConnection(dbPath);
    testConn.Open();
    testConn.Close();
    logger.LogInformation("Database OK: {dbPath}", dbPath);
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Database initialization failed!");
    throw;
}
```

---

## 📋 Checklist de Deploy

- [ ] Pasta `data/db/` existe e tem permissões de escrita
- [ ] `METRICS_SQLITE_PATH` ou `Database:Path` configurado
- [ ] Teste local: `dotnet run` e verificar `config.db` criado
- [ ] Teste em Docker: `docker compose up` e validar BD
- [ ] Backup automático configurado (cron job)
- [ ] Health check inclui verificação de BD
- [ ] Logs mostram `Database OK` na startup
- [ ] Volume Docker para persistência (se containerizado)

---

## 🔄 Atualização do .gitignore

**Agora com BD files ignorado:**

```ignore
# Database files (generated at runtime, never commit)
*.db
*.sqlite
*.sqlite3
*.db-journal
src/Api/config/*.db
src/Api/config/*.backup
```

**Por que?**
- `*.db` são binários grandes
- Contêm dados que variam entre ambientes
- Não devem ser versionados
- São criados automaticamente na execução

**Verificar:**
```bash
git status
# Não deve mostrar config.db ou *.db
```

---

## 📊 Resumo

| Ambiente | Como BD é Criada | Caminho | Persistência |
|----------|-----------------|--------|--------------|
| **Dev Local** | Automático na startup | `./config/config.db` | Arquivo local |
| **Docker** | Automático na startup | `/app/data/db/production.db` | Volume Docker |
| **Linux Server** | Automático na startup | `/app/data/db/production.db` | Arquivo no servidor |
| **Azure App Service** | Automático na startup | `/home/site/wwwroot/data/production.db` | App Service file system |

**Processo comum a todos:**
1. API inicia
2. `InitializeDatabase()` é chamado
3. Se BD não existe: **criada automaticamente**
4. Se tabelas não existem: **criadas automaticamente**
5. API pronta para aceitar requests

---

## 🎯 Conclusão

✅ **Não precisa de script de migração**  
✅ **BD é criada automaticamente na primeira execução**  
✅ **Pronto para produção com `.gitignore` correto**  
✅ **Escalável para Docker e cloud**

**Único detalhe:** Garantir que o caminho da BD tem permissões de escrita no ambiente de deploy.
