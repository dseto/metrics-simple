using Microsoft.Data.Sqlite;
using Metrics.Engine;

namespace Metrics.Api;

/// <summary>
/// Repository for encrypted connector secrets (v1.2.0+).
/// Supports multiple secret kinds per connector: BEARER_TOKEN, API_KEY_VALUE, BASIC_PASSWORD.
/// </summary>
public interface IConnectorSecretsRepository
{
    Task<EncryptedSecretRecord?> GetByConnectorIdAndKindAsync(string connectorId, string secretKind);
    Task<Dictionary<string, bool>> GetHasSecretsFlagsAsync(string connectorId);
    Task UpsertAsync(string connectorId, string secretKind, EncryptedToken encryptedToken);
    Task DeleteByConnectorIdAndKindAsync(string connectorId, string secretKind);
    Task DeleteAllByConnectorIdAsync(string connectorId);
}

public record EncryptedSecretRecord(
    string ConnectorId,
    string SecretKind,
    int EncVersion,
    string EncAlg,
    string EncNonce,
    string EncCiphertext,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// Secret kinds supported by connector_secrets table.
/// </summary>
public static class SecretKinds
{
    public const string BearerToken = "BEARER_TOKEN";
    public const string ApiKeyValue = "API_KEY_VALUE";
    public const string BasicPassword = "BASIC_PASSWORD";
}

public sealed class ConnectorSecretsRepository : IConnectorSecretsRepository
{
    private readonly string _connectionString;

    public ConnectorSecretsRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<EncryptedSecretRecord?> GetByConnectorIdAndKindAsync(string connectorId, string secretKind)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT connectorId, secretKind, encVersion, encAlg, encNonce, encCiphertext, createdAt, updatedAt
            FROM connector_secrets
            WHERE connectorId = @connectorId AND secretKind = @secretKind";
        command.Parameters.AddWithValue("@connectorId", connectorId);
        command.Parameters.AddWithValue("@secretKind", secretKind);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new EncryptedSecretRecord(
            ConnectorId: reader.GetString(0),
            SecretKind: reader.GetString(1),
            EncVersion: reader.GetInt32(2),
            EncAlg: reader.GetString(3),
            EncNonce: reader.GetString(4),
            EncCiphertext: reader.GetString(5),
            CreatedAt: DateTime.Parse(reader.GetString(6)),
            UpdatedAt: DateTime.Parse(reader.GetString(7))
        );
    }

    public async Task<Dictionary<string, bool>> GetHasSecretsFlagsAsync(string connectorId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT secretKind FROM connector_secrets WHERE connectorId = @connectorId";
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

    public async Task UpsertAsync(string connectorId, string secretKind, EncryptedToken encryptedToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO connector_secrets (connectorId, secretKind, encVersion, encAlg, encNonce, encCiphertext, createdAt, updatedAt)
            VALUES (@connectorId, @secretKind, @encVersion, @encAlg, @encNonce, @encCiphertext, @now, @now)
            ON CONFLICT(connectorId, secretKind) DO UPDATE SET
                encVersion = @encVersion,
                encAlg = @encAlg,
                encNonce = @encNonce,
                encCiphertext = @encCiphertext,
                updatedAt = @now";

        command.Parameters.AddWithValue("@connectorId", connectorId);
        command.Parameters.AddWithValue("@secretKind", secretKind);
        command.Parameters.AddWithValue("@encVersion", encryptedToken.Version);
        command.Parameters.AddWithValue("@encAlg", encryptedToken.Algorithm);
        command.Parameters.AddWithValue("@encNonce", encryptedToken.NonceBase64);
        command.Parameters.AddWithValue("@encCiphertext", encryptedToken.CiphertextBase64);
        command.Parameters.AddWithValue("@now", now);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteByConnectorIdAndKindAsync(string connectorId, string secretKind)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM connector_secrets WHERE connectorId = @connectorId AND secretKind = @secretKind";
        command.Parameters.AddWithValue("@connectorId", connectorId);
        command.Parameters.AddWithValue("@secretKind", secretKind);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAllByConnectorIdAsync(string connectorId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM connector_secrets WHERE connectorId = @connectorId";
        command.Parameters.AddWithValue("@connectorId", connectorId);

        await command.ExecuteNonQueryAsync();
    }
}
