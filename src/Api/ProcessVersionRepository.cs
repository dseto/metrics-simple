using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Metrics.Api;

public interface IProcessVersionRepository
{
    Task<List<ProcessVersionDto>> GetAllVersionsAsync(string processId);
    Task<ProcessVersionDto?> GetVersionAsync(string processId, int version);
    Task<ProcessVersionDto> CreateVersionAsync(ProcessVersionDto version);
    Task<ProcessVersionDto?> UpdateVersionAsync(string processId, int version, ProcessVersionDto updated);
    Task<bool> DeleteVersionAsync(string processId, int version);
}

public sealed class ProcessVersionRepository : IProcessVersionRepository
{
    private readonly string _connectionString;

    public ProcessVersionRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<List<ProcessVersionDto>> GetAllVersionsAsync(string processId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT processId, version, enabled, sourceRequestJson, dslProfile, dslText, outputSchemaJson, sampleInputJson
            FROM ProcessVersion
            WHERE processId = @processId
            ORDER BY version ASC";
        
        command.Parameters.AddWithValue("@processId", processId);

        var versions = new List<ProcessVersionDto>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var sourceRequestJson = reader.GetString(3);
            var dslProfile = reader.GetString(4);
            var dslText = reader.GetString(5);
            var outputSchemaJson = reader.GetString(6);
            var sampleInputJson = reader.IsDBNull(7) ? null : reader.GetString(7);

            var sourceRequest = JsonSerializer.Deserialize<SourceRequestDto>(sourceRequestJson)!;
            var outputSchema = JsonSerializer.Deserialize<object>(outputSchemaJson)!;
            var sampleInput = sampleInputJson != null ? JsonSerializer.Deserialize<object>(sampleInputJson) : null;

            versions.Add(new ProcessVersionDto(
                processId,
                reader.GetInt32(1),
                reader.GetBoolean(2),
                sourceRequest,
                new DslDto(dslProfile, dslText),
                outputSchema,
                sampleInput
            ));
        }

        return versions;
    }

    public async Task<ProcessVersionDto?> GetVersionAsync(string processId, int version)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT processId, version, enabled, sourceRequestJson, dslProfile, dslText, outputSchemaJson, sampleInputJson
            FROM ProcessVersion
            WHERE processId = @processId AND version = @version";
        
        command.Parameters.AddWithValue("@processId", processId);
        command.Parameters.AddWithValue("@version", version);

        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            var sourceRequestJson = reader.GetString(3);
            var dslProfile = reader.GetString(4);
            var dslText = reader.GetString(5);
            var outputSchemaJson = reader.GetString(6);
            var sampleInputJson = reader.IsDBNull(7) ? null : reader.GetString(7);

            var sourceRequest = JsonSerializer.Deserialize<SourceRequestDto>(sourceRequestJson)!;
            var outputSchema = JsonSerializer.Deserialize<object>(outputSchemaJson)!;
            var sampleInput = sampleInputJson != null ? JsonSerializer.Deserialize<object>(sampleInputJson) : null;

            return new ProcessVersionDto(
                processId,
                reader.GetInt32(1),
                reader.GetBoolean(2),
                sourceRequest,
                new DslDto(dslProfile, dslText),
                outputSchema,
                sampleInput
            );
        }

        return null;
    }

    public async Task<ProcessVersionDto> CreateVersionAsync(ProcessVersionDto version)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO ProcessVersion (processId, version, enabled, sourceRequestJson, dslProfile, dslText, outputSchemaJson, sampleInputJson, createdAt)
            VALUES (@processId, @version, @enabled, @sourceRequestJson, @dslProfile, @dslText, @outputSchemaJson, @sampleInputJson, @now)";
        
        command.Parameters.AddWithValue("@processId", version.ProcessId);
        command.Parameters.AddWithValue("@version", version.Version);
        command.Parameters.AddWithValue("@enabled", version.Enabled);
        command.Parameters.AddWithValue("@sourceRequestJson", JsonSerializer.Serialize(version.SourceRequest));
        command.Parameters.AddWithValue("@dslProfile", version.Dsl.Profile);
        command.Parameters.AddWithValue("@dslText", version.Dsl.Text);
        command.Parameters.AddWithValue("@outputSchemaJson", JsonSerializer.Serialize(version.OutputSchema));
        command.Parameters.AddWithValue("@sampleInputJson", version.SampleInput != null ? JsonSerializer.Serialize(version.SampleInput) : DBNull.Value);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        try
        {
            await command.ExecuteNonQueryAsync();
            return version;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // UNIQUE constraint violation - version already exists
            throw new InvalidOperationException($"Version {version.Version} already exists for process {version.ProcessId}", ex);
        }
    }

    public async Task<ProcessVersionDto?> UpdateVersionAsync(string processId, int version, ProcessVersionDto updated)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Check if version exists
        var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = "SELECT 1 FROM ProcessVersion WHERE processId = @processId AND version = @version";
        checkCommand.Parameters.AddWithValue("@processId", processId);
        checkCommand.Parameters.AddWithValue("@version", version);
        
        var exists = await checkCommand.ExecuteScalarAsync() != null;
        if (!exists)
            return null;

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE ProcessVersion
            SET enabled = @enabled, sourceRequestJson = @sourceRequestJson, dslProfile = @dslProfile, dslText = @dslText, outputSchemaJson = @outputSchemaJson, sampleInputJson = @sampleInputJson
            WHERE processId = @processId AND version = @version";
        
        command.Parameters.AddWithValue("@processId", processId);
        command.Parameters.AddWithValue("@version", version);
        command.Parameters.AddWithValue("@enabled", updated.Enabled);
        command.Parameters.AddWithValue("@sourceRequestJson", JsonSerializer.Serialize(updated.SourceRequest));
        command.Parameters.AddWithValue("@dslProfile", updated.Dsl.Profile);
        command.Parameters.AddWithValue("@dslText", updated.Dsl.Text);
        command.Parameters.AddWithValue("@outputSchemaJson", JsonSerializer.Serialize(updated.OutputSchema));
        command.Parameters.AddWithValue("@sampleInputJson", updated.SampleInput != null ? JsonSerializer.Serialize(updated.SampleInput) : DBNull.Value);

        await command.ExecuteNonQueryAsync();
        return updated;
    }

    public async Task<bool> DeleteVersionAsync(string processId, int version)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ProcessVersion WHERE processId = @processId AND version = @version";
        command.Parameters.AddWithValue("@processId", processId);
        command.Parameters.AddWithValue("@version", version);

        var rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }
}
