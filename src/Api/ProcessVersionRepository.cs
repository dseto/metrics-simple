using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Metrics.Api;

public interface IProcessVersionRepository
{
    Task<ProcessVersionDto?> GetVersionAsync(string processId, string version);
    Task<ProcessVersionDto> CreateVersionAsync(ProcessVersionDto version);
    Task<ProcessVersionDto> UpdateVersionAsync(string processId, string version, ProcessVersionDto updated);
}

public sealed class ProcessVersionRepository : IProcessVersionRepository
{
    private readonly string _connectionString;

    public ProcessVersionRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<ProcessVersionDto?> GetVersionAsync(string processId, string version)
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
                version,
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

        await command.ExecuteNonQueryAsync();
        return version;
    }

    public async Task<ProcessVersionDto> UpdateVersionAsync(string processId, string version, ProcessVersionDto updated)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

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
}
