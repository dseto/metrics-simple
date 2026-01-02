using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Metrics.Api;

public interface IProcessRepository
{
    Task<List<ProcessDto>> GetAllProcessesAsync();
    Task<ProcessDto?> GetProcessByIdAsync(string id);
    Task<ProcessDto> CreateProcessAsync(ProcessDto process);
    Task<ProcessDto> UpdateProcessAsync(string id, ProcessDto process);
    Task DeleteProcessAsync(string id);
}

public sealed class ProcessRepository : IProcessRepository
{
    private readonly string _connectionString;

    public ProcessRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task<List<ProcessDto>> GetAllProcessesAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, status, connectorId, outputDestinationsJson FROM Process";

        var processes = new List<ProcessDto>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            var name = reader.GetString(1);
            var status = reader.GetString(2);
            var connectorId = reader.GetString(3);
            var destJson = reader.GetString(4);

            var destinations = JsonSerializer.Deserialize<List<OutputDestinationDto>>(destJson) ?? new();
            processes.Add(new ProcessDto(id, name, status, connectorId, destinations));
        }

        return processes;
    }

    public async Task<ProcessDto?> GetProcessByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, status, connectorId, outputDestinationsJson FROM Process WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            var name = reader.GetString(1);
            var status = reader.GetString(2);
            var connectorId = reader.GetString(3);
            var destJson = reader.GetString(4);

            var destinations = JsonSerializer.Deserialize<List<OutputDestinationDto>>(destJson) ?? new();
            return new ProcessDto(id, name, status, connectorId, destinations);
        }

        return null;
    }

    public async Task<ProcessDto> CreateProcessAsync(ProcessDto process)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Process (id, name, status, connectorId, outputDestinationsJson, createdAt, updatedAt)
            VALUES (@id, @name, @status, @connectorId, @destJson, @now, @now)";
        
        command.Parameters.AddWithValue("@id", process.Id);
        command.Parameters.AddWithValue("@name", process.Name);
        command.Parameters.AddWithValue("@status", process.Status);
        command.Parameters.AddWithValue("@connectorId", process.ConnectorId);
        command.Parameters.AddWithValue("@destJson", JsonSerializer.Serialize(process.OutputDestinations));
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
        return process;
    }

    public async Task<ProcessDto> UpdateProcessAsync(string id, ProcessDto process)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Process 
            SET name = @name, status = @status, connectorId = @connectorId, outputDestinationsJson = @destJson, updatedAt = @now
            WHERE id = @id";
        
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", process.Name);
        command.Parameters.AddWithValue("@status", process.Status);
        command.Parameters.AddWithValue("@connectorId", process.ConnectorId);
        command.Parameters.AddWithValue("@destJson", JsonSerializer.Serialize(process.OutputDestinations));
        command.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));

        await command.ExecuteNonQueryAsync();
        return process;
    }

    public async Task DeleteProcessAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Process WHERE id = @id";
        command.Parameters.AddWithValue("@id", id);

        await command.ExecuteNonQueryAsync();
    }
}
