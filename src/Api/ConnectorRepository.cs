using Microsoft.Data.Sqlite;

namespace Metrics.Api;

public interface IConnectorRepository
{
    Task<List<ConnectorDto>> GetAllConnectorsAsync();
    Task<ConnectorDto?> GetConnectorByIdAsync(string id);
    Task<ConnectorDto> CreateConnectorAsync(ConnectorDto connector);
}

public sealed class ConnectorRepository : IConnectorRepository
{
    private readonly string _connectionString;

    public ConnectorRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<List<ConnectorDto>> GetAllConnectorsAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, baseUrl, authRef, timeoutSeconds FROM Connector";

        var connectors = new List<ConnectorDto>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            connectors.Add(new ConnectorDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4)
            ));
        }

        return connectors;
    }

    public async Task<ConnectorDto?> GetConnectorByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, baseUrl, authRef, timeoutSeconds FROM Connector WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return new ConnectorDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4)
            );
        }

        return null;
    }

    public async Task<ConnectorDto> CreateConnectorAsync(ConnectorDto connector)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Connector (id, name, baseUrl, authRef, timeoutSeconds, createdAt, updatedAt)
            VALUES (@id, @name, @baseUrl, @authRef, @timeoutSeconds, @now, @now)";
        
        command.Parameters.AddWithValue("@id", connector.Id);
        command.Parameters.AddWithValue("@name", connector.Name);
        command.Parameters.AddWithValue("@baseUrl", connector.BaseUrl);
        command.Parameters.AddWithValue("@authRef", connector.AuthRef);
        command.Parameters.AddWithValue("@timeoutSeconds", connector.TimeoutSeconds);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
        return connector;
    }
}
