using System.IO;
using Microsoft.Data.Sqlite;

namespace Metrics.Engine;

public interface IDatabaseProvider
{
    void InitializeDatabase(string dbPath);
    SqliteConnection GetConnection(string dbPath);
}

public sealed class DatabaseProvider : IDatabaseProvider
{
    public void InitializeDatabase(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = $"Data Source={dbPath}";
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var command = connection.CreateCommand();

        // Create Process table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Process (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                status TEXT NOT NULL,
                connectorId TEXT NOT NULL,
                outputDestinationsJson TEXT NOT NULL,
                createdAt TEXT NOT NULL,
                updatedAt TEXT NOT NULL
            )";
        command.ExecuteNonQuery();

        // Create ProcessVersion table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS ProcessVersion (
                processId TEXT NOT NULL,
                version TEXT NOT NULL,
                enabled BOOLEAN NOT NULL,
                sourceRequestJson TEXT NOT NULL,
                dslProfile TEXT NOT NULL,
                dslText TEXT NOT NULL,
                outputSchemaJson TEXT NOT NULL,
                sampleInputJson TEXT,
                createdAt TEXT NOT NULL,
                PRIMARY KEY (processId, version),
                FOREIGN KEY (processId) REFERENCES Process(id)
            )";
        command.ExecuteNonQuery();

        // Create Connector table
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Connector (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                baseUrl TEXT NOT NULL,
                timeoutSeconds INTEGER NOT NULL,
                authRef TEXT NOT NULL,
                createdAt TEXT NOT NULL,
                updatedAt TEXT NOT NULL
            )";
        command.ExecuteNonQuery();

        // Create Auth tables (001_auth_users migration)
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS auth_users (
                id                TEXT PRIMARY KEY,
                username          TEXT NOT NULL UNIQUE,
                display_name      TEXT NULL,
                email             TEXT NULL,
                password_hash     TEXT NOT NULL,
                is_active         INTEGER NOT NULL DEFAULT 1,
                failed_attempts   INTEGER NOT NULL DEFAULT 0,
                lockout_until_utc TEXT NULL,
                created_at_utc    TEXT NOT NULL,
                updated_at_utc    TEXT NOT NULL,
                last_login_utc    TEXT NULL
            )";
        command.ExecuteNonQuery();

        command.CommandText = @"CREATE INDEX IF NOT EXISTS idx_auth_users_username ON auth_users(username)";
        command.ExecuteNonQuery();

        command.CommandText = @"CREATE INDEX IF NOT EXISTS idx_auth_users_active ON auth_users(is_active)";
        command.ExecuteNonQuery();

        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS auth_user_roles (
                user_id   TEXT NOT NULL,
                role      TEXT NOT NULL,
                PRIMARY KEY (user_id, role),
                FOREIGN KEY (user_id) REFERENCES auth_users(id) ON DELETE CASCADE
            )";
        command.ExecuteNonQuery();

        command.CommandText = @"CREATE INDEX IF NOT EXISTS idx_auth_user_roles_role ON auth_user_roles(role)";
        command.ExecuteNonQuery();

        connection.Close();
    }

    public SqliteConnection GetConnection(string dbPath)
    {
        var connectionString = $"Data Source={dbPath}";
        return new SqliteConnection(connectionString);
    }
}
