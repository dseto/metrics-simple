using Microsoft.Data.Sqlite;
using Metrics.Engine;

namespace Metrics.Api;

/// <summary>
/// Repository for encrypted connector API tokens.
/// Per spec: stores encrypted tokens in connector_tokens table.
/// </summary>
public interface IConnectorTokenRepository
{
    Task<EncryptedTokenRecord?> GetByConnectorIdAsync(string connectorId);
    Task UpsertAsync(string connectorId, EncryptedToken encryptedToken);
    Task DeleteByConnectorIdAsync(string connectorId);
    Task<bool> HasTokenAsync(string connectorId);
}

public record EncryptedTokenRecord(
    string ConnectorId,
    int EncVersion,
    string EncAlg,
    string EncNonce,
    string EncCiphertext,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public sealed class ConnectorTokenRepository : IConnectorTokenRepository
{
    private readonly string _connectionString;

    public ConnectorTokenRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<EncryptedTokenRecord?> GetByConnectorIdAsync(string connectorId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT connectorId, encVersion, encAlg, encNonce, encCiphertext, createdAt, updatedAt
            FROM connector_tokens
            WHERE connectorId = @connectorId";
        command.Parameters.AddWithValue("@connectorId", connectorId);

        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new EncryptedTokenRecord(
            ConnectorId: reader.GetString(0),
            EncVersion: reader.GetInt32(1),
            EncAlg: reader.GetString(2),
            EncNonce: reader.GetString(3),
            EncCiphertext: reader.GetString(4),
            CreatedAt: DateTime.Parse(reader.GetString(5)),
            UpdatedAt: DateTime.Parse(reader.GetString(6))
        );
    }

    public async Task UpsertAsync(string connectorId, EncryptedToken encryptedToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var now = DateTime.UtcNow.ToString("O");

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO connector_tokens (connectorId, encVersion, encAlg, encNonce, encCiphertext, createdAt, updatedAt)
            VALUES (@connectorId, @encVersion, @encAlg, @encNonce, @encCiphertext, @now, @now)
            ON CONFLICT(connectorId) DO UPDATE SET
                encVersion = @encVersion,
                encAlg = @encAlg,
                encNonce = @encNonce,
                encCiphertext = @encCiphertext,
                updatedAt = @now";

        command.Parameters.AddWithValue("@connectorId", connectorId);
        command.Parameters.AddWithValue("@encVersion", encryptedToken.Version);
        command.Parameters.AddWithValue("@encAlg", encryptedToken.Algorithm);
        command.Parameters.AddWithValue("@encNonce", encryptedToken.NonceBase64);
        command.Parameters.AddWithValue("@encCiphertext", encryptedToken.CiphertextBase64);
        command.Parameters.AddWithValue("@now", now);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteByConnectorIdAsync(string connectorId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM connector_tokens WHERE connectorId = @connectorId";
        command.Parameters.AddWithValue("@connectorId", connectorId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> HasTokenAsync(string connectorId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM connector_tokens WHERE connectorId = @connectorId";
        command.Parameters.AddWithValue("@connectorId", connectorId);

        var count = (long)(await command.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }
}
