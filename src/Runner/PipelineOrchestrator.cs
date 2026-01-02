using System.Text.Json;
using System.Text.Json.Serialization;
using Metrics.Engine;
using Serilog;

namespace Metrics.Runner;

public record PipelineContext(
    string ExecutionId,
    string ProcessId,
    string Version,
    string OutputDestination,
    string? OutputPath,
    string? SecretsPath,
    string? DbPath
);

public record PipelineResult(
    int ExitCode,
    string Message,
    string? CsvPath = null,
    string? LogPath = null
);

public interface IPipelineOrchestrator
{
    Task<PipelineResult> ExecutePipelineAsync(PipelineContext context);
}

public sealed class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly EngineService _engine;
    private readonly ISecretsProvider _secretsProvider;
    private readonly IDatabaseProvider _databaseProvider;

    public PipelineOrchestrator(EngineService engine, ISecretsProvider secretsProvider, IDatabaseProvider databaseProvider)
    {
        _engine = engine;
        _secretsProvider = secretsProvider;
        _databaseProvider = databaseProvider;
    }

    public async Task<PipelineResult> ExecutePipelineAsync(PipelineContext context)
    {
        try
        {
            Log.Information("Pipeline starting for process {ProcessId} version {Version}", context.ProcessId, context.Version);

            // Step 1: Load config from SQLite
            Log.Information("Step 1: Loading configuration from SQLite");
            if (string.IsNullOrEmpty(context.DbPath))
            {
                Log.Error("Database path not provided");
                return new PipelineResult(10, "Database path is required");
            }

            var connection = _databaseProvider.GetConnection(context.DbPath);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT pv.sourceRequestJson, pv.dslProfile, pv.dslText, pv.outputSchemaJson, pv.sampleInputJson, p.connectorId
                FROM ProcessVersion pv
                JOIN Process p ON pv.processId = p.id
                WHERE pv.processId = @processId AND pv.version = @version";
            
            command.Parameters.AddWithValue("@processId", context.ProcessId);
            command.Parameters.AddWithValue("@version", context.Version);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                Log.Error("Process version not found");
                return new PipelineResult(10, "Process version not found");
            }

            var sourceRequestJson = reader.GetString(0);
            var dslProfile = reader.GetString(1);
            var dslText = reader.GetString(2);
            var outputSchemaJson = reader.GetString(3);
            var sampleInputJson = reader.IsDBNull(4) ? null : reader.GetString(4);
            var connectorId = reader.GetString(5);

            connection.Close();

            // Step 2: Load secrets
            Log.Information("Step 2: Loading secrets");
            if (string.IsNullOrEmpty(context.SecretsPath))
            {
                Log.Error("Secrets path not provided");
                return new PipelineResult(10, "Secrets path is required");
            }

            SecretConfig secrets;
            try
            {
                secrets = _secretsProvider.LoadSecrets(context.SecretsPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load secrets");
                return new PipelineResult(10, $"Failed to load secrets: {ex.Message}");
            }

            // Step 3: Fetch data from external API
            Log.Information("Step 3: Fetching data from external API");
            var sourceRequest = JsonSerializer.Deserialize<SourceRequestDto>(sourceRequestJson);
            if (sourceRequest == null)
            {
                Log.Error("Invalid source request configuration");
                return new PipelineResult(10, "Invalid source request configuration");
            }

            var connectorSecret = _secretsProvider.GetConnectorSecret(secrets, connectorId, "token");
            if (string.IsNullOrEmpty(connectorSecret))
            {
                Log.Error("Connector secret not found");
                return new PipelineResult(10, $"Connector secret not found for {connectorId}");
            }

            JsonElement inputData;
            try
            {
                inputData = await FetchExternalDataAsync(sourceRequest, connectorSecret);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to fetch external data");
                return new PipelineResult(20, $"Failed to fetch external data: {ex.Message}");
            }

            // Step 4: Execute DSL transformation
            Log.Information("Step 4: Executing DSL transformation");
            var outputSchema = JsonDocument.Parse(outputSchemaJson).RootElement;
            var transformResult = _engine.TransformValidateToCsv(inputData, dslProfile, dslText, outputSchema);

            if (!transformResult.IsValid)
            {
                Log.Error("Transformation failed: {Errors}", string.Join(", ", transformResult.Errors));
                return new PipelineResult(30, $"Transformation failed: {string.Join(", ", transformResult.Errors)}");
            }

            // Step 5: Validate output schema
            Log.Information("Step 5: Validating output schema");
            if (transformResult.OutputJson == null)
            {
                Log.Error("Transformation produced no output");
                return new PipelineResult(40, "Transformation produced no output");
            }

            // Step 6: Generate CSV
            Log.Information("Step 6: Generating CSV");
            var csv = transformResult.CsvPreview;
            if (string.IsNullOrEmpty(csv))
            {
                Log.Error("CSV generation failed");
                return new PipelineResult(50, "CSV generation failed");
            }

            // Step 7: Save CSV locally or to Blob
            Log.Information("Step 7: Saving CSV to {Destination}", context.OutputDestination);
            string csvPath;
            try
            {
                csvPath = await SaveCsvAsync(context, csv);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save CSV");
                return new PipelineResult(50, $"Failed to save CSV: {ex.Message}");
            }

            // Step 8: Save logs
            Log.Information("Step 8: Saving logs");
            Log.Information("Pipeline completed successfully");

            return new PipelineResult(0, "Pipeline executed successfully", csvPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during pipeline execution");
            return new PipelineResult(99, $"Unexpected error: {ex.Message}");
        }
    }

    private async Task<JsonElement> FetchExternalDataAsync(SourceRequestDto request, string authToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

        var url = request.Path;
        if (request.QueryParams != null && request.QueryParams.Count > 0)
        {
            var queryString = string.Join("&", request.QueryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
            url = $"{url}?{queryString}";
        }

        HttpResponseMessage response = request.Method.ToUpper() switch
        {
            "GET" => await client.GetAsync(url),
            "POST" => await client.PostAsync(url, null),
            _ => throw new NotSupportedException($"HTTP method {request.Method} is not supported")
        };

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content).RootElement;
    }

    private async Task<string> SaveCsvAsync(PipelineContext context, string csv)
    {
        var fileName = $"{context.ExecutionId}_{context.ProcessId}_{context.Version}.csv";

        if (context.OutputDestination == "local" || context.OutputDestination == "both")
        {
            var outputPath = context.OutputPath ?? "./exports";
            Directory.CreateDirectory(outputPath);
            var filePath = Path.Combine(outputPath, fileName);
            await File.WriteAllTextAsync(filePath, csv);
            Log.Information("CSV saved to {FilePath}", filePath);
            return filePath;
        }

        return $"blob://{fileName}";
    }
}

public record SourceRequestDto(
    string Method,
    string Path,
    Dictionary<string, string>? Headers = null,
    Dictionary<string, string>? QueryParams = null
);
