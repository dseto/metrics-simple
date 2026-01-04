using Microsoft.Data.Sqlite;
using Metrics.Engine;

namespace Metrics.Api;

public interface IConnectorRepository
{
    Task<List<ConnectorDto>> GetAllConnectorsAsync();
    Task<ConnectorDto?> GetConnectorByIdAsync(string id);
    Task<ConnectorDto> CreateConnectorAsync(ConnectorCreateDto connector, ITokenEncryptionService encryptionService, IConnectorTokenRepository tokenRepo);
    Task<ConnectorDto> UpdateConnectorAsync(string id, ConnectorUpdateDto connector, ITokenEncryptionService encryptionService, IConnectorTokenRepository tokenRepo);
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
        command.CommandText = "SELECT id, name, baseUrl, authRef, timeoutSeconds FROM Connector ORDER BY id ASC";

        var connectors = new List<ConnectorDto>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            
            // Check if token exists (for hasApiToken)
            var hasToken = await HasTokenInternalAsync(connection, id);
            
            connectors.Add(new ConnectorDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                HasApiToken: hasToken
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
            var hasToken = await HasTokenInternalAsync(connection, id);
            
            return new ConnectorDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                HasApiToken: hasToken
            );
        }

        return null;
    }

    public async Task<ConnectorDto> CreateConnectorAsync(
        ConnectorCreateDto connector,
        ITokenEncryptionService encryptionService,
        IConnectorTokenRepository tokenRepo)
    {
        // Validate apiToken length if provided (non-null)
        if (connector.ApiToken != null)
        {
            if (connector.ApiToken.Length < 1 || connector.ApiToken.Length > 4096)
            {
                throw new ArgumentException("ApiToken must be between 1 and 4096 characters", nameof(connector.ApiToken));
            }
        }

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

        // Store encrypted token if provided
        if (!string.IsNullOrEmpty(connector.ApiToken))
        {
            var encrypted = encryptionService.Encrypt(connector.ApiToken);
            await tokenRepo.UpsertAsync(connector.Id, encrypted);
        }

        return new ConnectorDto(
            connector.Id,
            connector.Name,
            connector.BaseUrl,
            connector.AuthRef,
            connector.TimeoutSeconds,
            HasApiToken: !string.IsNullOrEmpty(connector.ApiToken)
        );
    }

    public async Task<ConnectorDto> UpdateConnectorAsync(
        string id,
        ConnectorUpdateDto connector,
        ITokenEncryptionService encryptionService,
        IConnectorTokenRepository tokenRepo)
    {
        // Validate apiToken length if provided and not null
        if (connector.ApiTokenSpecified && connector.ApiToken != null)
        {
            if (connector.ApiToken.Length < 1 || connector.ApiToken.Length > 4096)
            {
                throw new ArgumentException("ApiToken must be between 1 and 4096 characters", nameof(connector.ApiToken));
            }
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Connector 
            SET name = @name, baseUrl = @baseUrl, authRef = @authRef, timeoutSeconds = @timeoutSeconds, updatedAt = @now
            WHERE id = @id";
        
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", connector.Name);
        command.Parameters.AddWithValue("@baseUrl", connector.BaseUrl);
        command.Parameters.AddWithValue("@authRef", connector.AuthRef);
        command.Parameters.AddWithValue("@timeoutSeconds", connector.TimeoutSeconds);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Connector not found: {id}");
        }

        // Handle apiToken semantics:
        // - ApiTokenSpecified=false => keep existing token (do nothing)
        // - ApiTokenSpecified=true, ApiToken=null => remove token
        // - ApiTokenSpecified=true, ApiToken=string => replace token
        if (connector.ApiTokenSpecified)
        {
            if (connector.ApiToken == null)
            {
                // Remove token
                await tokenRepo.DeleteByConnectorIdAsync(id);
            }
            else
            {
                // Replace token
                var encrypted = encryptionService.Encrypt(connector.ApiToken);
                await tokenRepo.UpsertAsync(id, encrypted);
            }
        }

        var hasToken = await HasTokenInternalAsync(connection, id);

        return new ConnectorDto(
            id,
            connector.Name,
            connector.BaseUrl,
            connector.AuthRef,
            connector.TimeoutSeconds,
            HasApiToken: hasToken
        );
    }

    private async Task<bool> HasTokenInternalAsync(SqliteConnection connection, string connectorId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM connector_tokens WHERE connectorId = @connectorId";
        command.Parameters.AddWithValue("@connectorId", connectorId);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }
}
