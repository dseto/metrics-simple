using System.Text.Json;
using Microsoft.Data.Sqlite;
using Metrics.Engine;

namespace Metrics.Api;

public interface IConnectorRepository
{
    Task<List<ConnectorDto>> GetAllConnectorsAsync();
    Task<ConnectorDto?> GetConnectorByIdAsync(string id);
    Task<ConnectorDto> CreateConnectorAsync(ConnectorCreateDto connector, ITokenEncryptionService encryptionService, IConnectorSecretsRepository secretsRepo);
    Task<ConnectorDto> UpdateConnectorAsync(string id, ConnectorUpdateDto connector, ITokenEncryptionService encryptionService, IConnectorSecretsRepository secretsRepo);
    Task<bool> DeleteConnectorAsync(string id);
    Task<bool> IsConnectorInUseAsync(string id);
}

/// <summary>
/// Auth configuration stored in authConfigJson (non-secret fields only).
/// </summary>
public record AuthConfigData
{
    public string? ApiKeyLocation { get; init; }
    public string? ApiKeyName { get; init; }
    public string? BasicUsername { get; init; }
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

        // First, get all connectors
        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, baseUrl, authType, authConfigJson, requestDefaultsJson, timeoutSeconds, enabled 
            FROM Connector 
            ORDER BY id ASC";

        var connectorData = new List<(string id, string name, string baseUrl, string authType, string? authConfigJson, string? requestDefaultsJson, int timeoutSeconds, bool enabled)>();
        
        using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var authConfigJson = reader.IsDBNull(4) ? null : reader.GetString(4);
                var requestDefaultsJson = reader.IsDBNull(5) ? null : reader.GetString(5);
                
                connectorData.Add((id, reader.GetString(1), reader.GetString(2), reader.GetString(3), authConfigJson, requestDefaultsJson, reader.GetInt32(6), reader.GetInt32(7) == 1));
            }
        } // Reader is disposed here

        // Now get all secrets in a separate command (reader is disposed)
        var secretsByConnector = new Dictionary<string, Dictionary<string, bool>>();
        
        var secretsCommand = connection.CreateCommand();
        secretsCommand.CommandText = @"
            SELECT connectorId, secretKind FROM connector_secrets";
        
        using (var secretsReader = await secretsCommand.ExecuteReaderAsync())
        {
            while (await secretsReader.ReadAsync())
            {
                var connectorId = secretsReader.GetString(0);
                var secretKind = secretsReader.GetString(1);
                
                if (!secretsByConnector.ContainsKey(connectorId))
                {
                    secretsByConnector[connectorId] = new Dictionary<string, bool>
                    {
                        [SecretKinds.BearerToken] = false,
                        [SecretKinds.ApiKeyValue] = false,
                        [SecretKinds.BasicPassword] = false
                    };
                }
                
                if (secretsByConnector[connectorId].ContainsKey(secretKind))
                {
                    secretsByConnector[connectorId][secretKind] = true;
                }
            }
        } // Reader is disposed here

        // Build final list with correct flags
        var connectors = new List<ConnectorDto>();
        foreach (var data in connectorData)
        {
            var hasFlags = new Dictionary<string, bool>
            {
                [SecretKinds.BearerToken] = false,
                [SecretKinds.ApiKeyValue] = false,
                [SecretKinds.BasicPassword] = false
            };
            
            if (secretsByConnector.TryGetValue(data.id, out var secrets))
            {
                hasFlags = secrets;
            }
            
            connectors.Add(MapToDto(
                id: data.id,
                name: data.name,
                baseUrl: data.baseUrl,
                authType: data.authType,
                authConfigJson: data.authConfigJson,
                requestDefaultsJson: data.requestDefaultsJson,
                timeoutSeconds: data.timeoutSeconds,
                enabled: data.enabled,
                hasFlags: hasFlags
            ));
        }

        return connectors;
    }

    public async Task<ConnectorDto?> GetConnectorByIdAsync(string connectorId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, baseUrl, authType, authConfigJson, requestDefaultsJson, timeoutSeconds, enabled 
            FROM Connector 
            WHERE id = @id";
        command.Parameters.AddWithValue("@id", connectorId);

        ConnectorDto? result = null;
        string? id = null, name = null, baseUrl = null, authType = null;
        string? authConfigJson = null, requestDefaultsJson = null;
        int timeoutSeconds = 0;
        bool enabled = false;
        
        using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                id = reader.GetString(0);
                name = reader.GetString(1);
                baseUrl = reader.GetString(2);
                authType = reader.GetString(3);
                authConfigJson = reader.IsDBNull(4) ? null : reader.GetString(4);
                requestDefaultsJson = reader.IsDBNull(5) ? null : reader.GetString(5);
                timeoutSeconds = reader.GetInt32(6);
                enabled = reader.GetInt32(7) == 1;
            }
        } // Reader is disposed here

        // If found, fetch secrets (now safe to execute another command)
        if (id != null)
        {
            var hasFlags = await GetHasSecretsFlagsAsync(connection, connectorId);
            
            result = MapToDto(
                id: id,
                name: name!,
                baseUrl: baseUrl!,
                authType: authType!,
                authConfigJson: authConfigJson,
                requestDefaultsJson: requestDefaultsJson,
                timeoutSeconds: timeoutSeconds,
                enabled: enabled,
                hasFlags: hasFlags
            );
        }

        return result;
    }

    public async Task<ConnectorDto> CreateConnectorAsync(
        ConnectorCreateDto connector,
        ITokenEncryptionService encryptionService,
        IConnectorSecretsRepository secretsRepo)
    {
        // Validate secrets if provided
        ValidateSecretLength(connector.ApiToken, "ApiToken");
        ValidateSecretLength(connector.ApiKeyValue, "ApiKeyValue");
        ValidateSecretLength(connector.BasicPassword, "BasicPassword");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");
        var authConfigJson = SerializeAuthConfig(connector.AuthType, connector.ApiKeyLocation, connector.ApiKeyName, connector.BasicUsername);
        var requestDefaultsJson = connector.RequestDefaults != null ? JsonSerializer.Serialize(connector.RequestDefaults) : null;

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Connector (id, name, baseUrl, authType, authConfigJson, requestDefaultsJson, timeoutSeconds, enabled, createdAt, updatedAt)
            VALUES (@id, @name, @baseUrl, @authType, @authConfigJson, @requestDefaultsJson, @timeoutSeconds, @enabled, @now, @now)";
        
        command.Parameters.AddWithValue("@id", connector.Id);
        command.Parameters.AddWithValue("@name", connector.Name);
        command.Parameters.AddWithValue("@baseUrl", connector.BaseUrl);
        command.Parameters.AddWithValue("@authType", connector.AuthType);
        command.Parameters.AddWithValue("@authConfigJson", (object?)authConfigJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@requestDefaultsJson", (object?)requestDefaultsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@timeoutSeconds", connector.TimeoutSeconds);
        command.Parameters.AddWithValue("@enabled", connector.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("@now", now);

        await command.ExecuteNonQueryAsync();

        // Store encrypted secrets if provided
        var hasApiToken = false;
        var hasApiKey = false;
        var hasBasicPassword = false;

        if (!string.IsNullOrEmpty(connector.ApiToken))
        {
            var encrypted = encryptionService.Encrypt(connector.ApiToken);
            await secretsRepo.UpsertAsync(connector.Id, SecretKinds.BearerToken, encrypted);
            hasApiToken = true;
        }

        if (!string.IsNullOrEmpty(connector.ApiKeyValue))
        {
            var encrypted = encryptionService.Encrypt(connector.ApiKeyValue);
            await secretsRepo.UpsertAsync(connector.Id, SecretKinds.ApiKeyValue, encrypted);
            hasApiKey = true;
        }

        if (!string.IsNullOrEmpty(connector.BasicPassword))
        {
            var encrypted = encryptionService.Encrypt(connector.BasicPassword);
            await secretsRepo.UpsertAsync(connector.Id, SecretKinds.BasicPassword, encrypted);
            hasBasicPassword = true;
        }

        return new ConnectorDto(
            Id: connector.Id,
            Name: connector.Name,
            BaseUrl: connector.BaseUrl,
            TimeoutSeconds: connector.TimeoutSeconds,
            Enabled: connector.Enabled,
            AuthType: connector.AuthType,
            ApiKeyLocation: connector.ApiKeyLocation,
            ApiKeyName: connector.ApiKeyName,
            BasicUsername: connector.BasicUsername,
            RequestDefaults: connector.RequestDefaults,
            HasApiToken: hasApiToken,
            HasApiKey: hasApiKey,
            HasBasicPassword: hasBasicPassword
        );
    }

    public async Task<ConnectorDto> UpdateConnectorAsync(
        string id,
        ConnectorUpdateDto connector,
        ITokenEncryptionService encryptionService,
        IConnectorSecretsRepository secretsRepo)
    {
        // Validate secrets if specified and not null
        if (connector.ApiTokenSpecified && connector.ApiToken != null)
            ValidateSecretLength(connector.ApiToken, "ApiToken");
        if (connector.ApiKeySpecified && connector.ApiKeyValue != null)
            ValidateSecretLength(connector.ApiKeyValue, "ApiKeyValue");
        if (connector.BasicPasswordSpecified && connector.BasicPassword != null)
            ValidateSecretLength(connector.BasicPassword, "BasicPassword");

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");
        var authConfigJson = SerializeAuthConfig(connector.AuthType, connector.ApiKeyLocation, connector.ApiKeyName, connector.BasicUsername);
        var requestDefaultsJson = connector.RequestDefaults != null ? JsonSerializer.Serialize(connector.RequestDefaults) : null;

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Connector 
            SET name = @name, baseUrl = @baseUrl, authType = @authType, authConfigJson = @authConfigJson, 
                requestDefaultsJson = @requestDefaultsJson, timeoutSeconds = @timeoutSeconds, enabled = @enabled, updatedAt = @now
            WHERE id = @id";
        
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", connector.Name);
        command.Parameters.AddWithValue("@baseUrl", connector.BaseUrl);
        command.Parameters.AddWithValue("@authType", connector.AuthType);
        command.Parameters.AddWithValue("@authConfigJson", (object?)authConfigJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@requestDefaultsJson", (object?)requestDefaultsJson ?? DBNull.Value);
        command.Parameters.AddWithValue("@timeoutSeconds", connector.TimeoutSeconds);
        command.Parameters.AddWithValue("@enabled", connector.Enabled ? 1 : 0);
        command.Parameters.AddWithValue("@now", now);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Connector not found: {id}");
        }

        // Handle secrets with *Specified semantics:
        // - *Specified=false => keep existing secret (do nothing)
        // - *Specified=true, value=null => remove secret
        // - *Specified=true, value=string => replace secret
        
        if (connector.ApiTokenSpecified)
        {
            if (connector.ApiToken == null)
            {
                await secretsRepo.DeleteByConnectorIdAndKindAsync(id, SecretKinds.BearerToken);
            }
            else
            {
                var encrypted = encryptionService.Encrypt(connector.ApiToken);
                await secretsRepo.UpsertAsync(id, SecretKinds.BearerToken, encrypted);
            }
        }

        if (connector.ApiKeySpecified)
        {
            if (connector.ApiKeyValue == null)
            {
                await secretsRepo.DeleteByConnectorIdAndKindAsync(id, SecretKinds.ApiKeyValue);
            }
            else
            {
                var encrypted = encryptionService.Encrypt(connector.ApiKeyValue);
                await secretsRepo.UpsertAsync(id, SecretKinds.ApiKeyValue, encrypted);
            }
        }

        if (connector.BasicPasswordSpecified)
        {
            if (connector.BasicPassword == null)
            {
                await secretsRepo.DeleteByConnectorIdAndKindAsync(id, SecretKinds.BasicPassword);
            }
            else
            {
                var encrypted = encryptionService.Encrypt(connector.BasicPassword);
                await secretsRepo.UpsertAsync(id, SecretKinds.BasicPassword, encrypted);
            }
        }

        var hasFlags = await GetHasSecretsFlagsAsync(connection, id);

        return new ConnectorDto(
            Id: id,
            Name: connector.Name,
            BaseUrl: connector.BaseUrl,
            TimeoutSeconds: connector.TimeoutSeconds,
            Enabled: connector.Enabled,
            AuthType: connector.AuthType,
            ApiKeyLocation: connector.ApiKeyLocation,
            ApiKeyName: connector.ApiKeyName,
            BasicUsername: connector.BasicUsername,
            RequestDefaults: connector.RequestDefaults,
            HasApiToken: hasFlags.GetValueOrDefault(SecretKinds.BearerToken),
            HasApiKey: hasFlags.GetValueOrDefault(SecretKinds.ApiKeyValue),
            HasBasicPassword: hasFlags.GetValueOrDefault(SecretKinds.BasicPassword)
        );
    }

    public async Task<bool> DeleteConnectorAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Connector WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> IsConnectorInUseAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Check if any Process references this connector
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Process WHERE connectorId = @id";
        command.Parameters.AddWithValue("@id", id);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    // Helper methods

    private static void ValidateSecretLength(string? secret, string paramName)
    {
        if (secret != null && (secret.Length < 1 || secret.Length > 4096))
        {
            throw new ArgumentException($"{paramName} must be between 1 and 4096 characters", paramName);
        }
    }

    private static string? SerializeAuthConfig(string authType, string? apiKeyLocation, string? apiKeyName, string? basicUsername)
    {
        if (authType == "NONE" || authType == "BEARER")
            return null;

        var config = new AuthConfigData
        {
            ApiKeyLocation = apiKeyLocation,
            ApiKeyName = apiKeyName,
            BasicUsername = basicUsername
        };

        return JsonSerializer.Serialize(config);
    }

    private async Task<Dictionary<string, bool>> GetHasSecretsFlagsAsync(SqliteConnection connection, string connectorId)
    {
        var command = connection.CreateCommand();
        command.CommandText = "SELECT secretKind FROM connector_secrets WHERE connectorId = @connectorId";
        command.Parameters.AddWithValue("@connectorId", connectorId);

        var result = new Dictionary<string, bool>
        {
            [SecretKinds.BearerToken] = false,
            [SecretKinds.ApiKeyValue] = false,
            [SecretKinds.BasicPassword] = false
        };

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var kind = reader.GetString(0);
            if (result.ContainsKey(kind))
            {
                result[kind] = true;
            }
        }

        return result;
    }

    private static ConnectorDto MapToDto(
        string id,
        string name,
        string baseUrl,
        string authType,
        string? authConfigJson,
        string? requestDefaultsJson,
        int timeoutSeconds,
        bool enabled,
        Dictionary<string, bool> hasFlags)
    {
        AuthConfigData? authConfig = null;
        if (!string.IsNullOrEmpty(authConfigJson))
        {
            authConfig = JsonSerializer.Deserialize<AuthConfigData>(authConfigJson);
        }

        RequestDefaultsDto? requestDefaults = null;
        if (!string.IsNullOrEmpty(requestDefaultsJson))
        {
            requestDefaults = JsonSerializer.Deserialize<RequestDefaultsDto>(requestDefaultsJson);
        }

        return new ConnectorDto(
            Id: id,
            Name: name,
            BaseUrl: baseUrl,
            TimeoutSeconds: timeoutSeconds,
            Enabled: enabled,
            AuthType: authType,
            ApiKeyLocation: authConfig?.ApiKeyLocation,
            ApiKeyName: authConfig?.ApiKeyName,
            BasicUsername: authConfig?.BasicUsername,
            RequestDefaults: requestDefaults,
            HasApiToken: hasFlags.GetValueOrDefault(SecretKinds.BearerToken),
            HasApiKey: hasFlags.GetValueOrDefault(SecretKinds.ApiKeyValue),
            HasBasicPassword: hasFlags.GetValueOrDefault(SecretKinds.BasicPassword)
        );
    }
}
