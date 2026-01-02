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

        connection.Close();
    }

    public SqliteConnection GetConnection(string dbPath)
    {
        var connectionString = $"Data Source={dbPath}";
        return new SqliteConnection(connectionString);
    }
}
