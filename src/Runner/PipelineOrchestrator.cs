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

/// <summary>
/// Exit codes per cli-contract.md:
/// 0 = OK
/// 10 = VALIDATION_ERROR
/// 20 = NOT_FOUND
/// 30 = DISABLED
/// 40 = SOURCE_ERROR
/// 50 = TRANSFORM_ERROR
/// 60 = STORAGE_ERROR
/// 70 = UNEXPECTED_ERROR
/// </summary>
public sealed class PipelineOrchestrator : IPipelineOrchestrator
{
    private readonly EngineService _engine;
    private readonly ISecretsProvider _secretsProvider;
    private readonly IDatabaseProvider _databaseProvider;
    private readonly HttpClient? _httpClient;

    public PipelineOrchestrator(EngineService engine, ISecretsProvider secretsProvider, IDatabaseProvider databaseProvider, HttpClient? httpClient = null)
    {
        _engine = engine;
        _secretsProvider = secretsProvider;
        _databaseProvider = databaseProvider;
        _httpClient = httpClient;
    }

    public async Task<PipelineResult> ExecutePipelineAsync(PipelineContext context)
    {
        try
        {
            Log.Information("Pipeline starting for process {ProcessId} version {Version}", context.ProcessId, context.Version);

            // Step 1: Load config from SQLite
            Log.Information("Step 1: LoadProcessConfig - Loading configuration from SQLite");
            if (string.IsNullOrEmpty(context.DbPath))
            {
                Log.Error("Database path not provided");
                return new PipelineResult(10, "Database path is required"); // VALIDATION_ERROR
            }

            // Ensure database is initialized (tables exist)
            _databaseProvider.InitializeDatabase(context.DbPath);

            var connection = _databaseProvider.GetConnection(context.DbPath);
            connection.Open();

            // Load ProcessVersion
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT pv.sourceRequestJson, pv.dslProfile, pv.dslText, pv.outputSchemaJson, pv.sampleInputJson, p.connectorId, pv.enabled
                FROM ProcessVersion pv
                JOIN Process p ON pv.processId = p.id
                WHERE pv.processId = @processId AND pv.version = @version";
            
            command.Parameters.AddWithValue("@processId", context.ProcessId);
            command.Parameters.AddWithValue("@version", context.Version);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                Log.Error("Process version not found: processId={ProcessId}, version={Version}", context.ProcessId, context.Version);
                return new PipelineResult(20, "Process version not found"); // NOT_FOUND
            }

            var sourceRequestJson = reader.GetString(0);
            var dslProfile = reader.GetString(1);
            var dslText = reader.GetString(2);
            var outputSchemaJson = reader.GetString(3);
            var sampleInputJson = reader.IsDBNull(4) ? null : reader.GetString(4);
            var connectorId = reader.GetString(5);
            var enabled = reader.GetBoolean(6);
            reader.Close();

            // Check if version is enabled
            if (!enabled)
            {
                Log.Error("Process version is disabled: processId={ProcessId}, version={Version}", context.ProcessId, context.Version);
                return new PipelineResult(30, "Process version is disabled"); // DISABLED
            }

            // Load Connector
            var connectorCommand = connection.CreateCommand();
            connectorCommand.CommandText = @"
                SELECT baseUrl, authRef, timeoutSeconds FROM Connector WHERE id = @connectorId";
            connectorCommand.Parameters.AddWithValue("@connectorId", connectorId);

            using var connectorReader = connectorCommand.ExecuteReader();
            if (!connectorReader.Read())
            {
                Log.Error("Connector not found: connectorId={ConnectorId}", connectorId);
                return new PipelineResult(20, $"Connector not found: {connectorId}"); // NOT_FOUND
            }

            var baseUrl = connectorReader.GetString(0);
            var authRef = connectorReader.GetString(1);
            var timeoutSeconds = connectorReader.GetInt32(2);
            connectorReader.Close();
            connection.Close();

            // Step 2: Resolve secret via METRICS_SECRET__<authRef> env var (normative for integration tests)
            Log.Information("Step 2: Resolving secret for authRef={AuthRef}", authRef);
            var secretEnvKey = $"METRICS_SECRET__{authRef}";
            var secret = Environment.GetEnvironmentVariable(secretEnvKey);
            
            // Fallback: try secrets file if env var not set
            if (string.IsNullOrEmpty(secret) && !string.IsNullOrEmpty(context.SecretsPath))
            {
                try
                {
                    var secretConfig = _secretsProvider.LoadSecrets(context.SecretsPath);
                    secret = _secretsProvider.GetConnectorSecret(secretConfig, connectorId, "token");
                }
                catch
                {
                    // Ignore file-based secrets errors
                }
            }

            if (string.IsNullOrEmpty(secret))
            {
                Log.Error("Secret not found for authRef={AuthRef} (env: {EnvKey})", authRef, secretEnvKey);
                return new PipelineResult(40, $"Secret not found for authRef: {authRef}"); // SOURCE_ERROR per spec
            }

            // Step 3: FetchSource - Fetch data from external API
            Log.Information("Step 3: FetchSource - Fetching data from external API");
            var sourceRequest = JsonSerializer.Deserialize<SourceRequestDto>(sourceRequestJson);
            if (sourceRequest == null)
            {
                Log.Error("Invalid source request configuration");
                return new PipelineResult(10, "Invalid source request configuration"); // VALIDATION_ERROR
            }

            JsonElement inputData;
            try
            {
                inputData = await FetchExternalDataAsync(baseUrl, sourceRequest, secret, timeoutSeconds);
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "FetchSource failed: HTTP error");
                return new PipelineResult(40, $"FetchSource failed: {ex.Message}"); // SOURCE_ERROR
            }
            catch (TaskCanceledException ex)
            {
                Log.Error(ex, "FetchSource failed: Timeout");
                return new PipelineResult(40, $"FetchSource timeout: {ex.Message}"); // SOURCE_ERROR
            }
            catch (Exception ex)
            {
                Log.Error(ex, "FetchSource failed: Unexpected error");
                return new PipelineResult(40, $"FetchSource failed: {ex.Message}"); // SOURCE_ERROR
            }

            // Step 4: Transform - Execute DSL transformation
            Log.Information("Step 4: Transform - Executing DSL transformation");
            var outputSchema = JsonDocument.Parse(outputSchemaJson).RootElement;
            var transformResult = _engine.TransformValidateToCsv(inputData, dslProfile, dslText, outputSchema);

            if (!transformResult.IsValid)
            {
                Log.Error("Transform failed: {Errors}", string.Join(", ", transformResult.Errors));
                return new PipelineResult(50, $"Transform failed: {string.Join(", ", transformResult.Errors)}"); // TRANSFORM_ERROR
            }

            // Step 5: ValidateOutputSchema
            Log.Information("Step 5: ValidateOutputSchema - Validating output schema");
            if (transformResult.OutputJson == null)
            {
                Log.Error("Transformation produced no output");
                return new PipelineResult(50, "Transformation produced no output"); // TRANSFORM_ERROR
            }

            // Step 6: GenerateCsv
            Log.Information("Step 6: GenerateCsv - Generating CSV");
            var csv = transformResult.CsvPreview;
            if (string.IsNullOrEmpty(csv))
            {
                Log.Error("CSV generation failed");
                return new PipelineResult(50, "CSV generation failed"); // TRANSFORM_ERROR
            }

            // Step 7: StoreCsv - Save CSV locally or to Blob
            Log.Information("Step 7: StoreCsv - Saving CSV to {Destination}", context.OutputDestination);
            string csvPath;
            try
            {
                csvPath = await SaveCsvAsync(context, csv);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "StoreCsv failed");
                return new PipelineResult(60, $"StoreCsv failed: {ex.Message}"); // STORAGE_ERROR
            }

            // Step 8: FinalizeExecution
            Log.Information("Step 8: FinalizeExecution - Pipeline completed successfully");

            return new PipelineResult(0, "Pipeline executed successfully", csvPath); // OK
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Unexpected error during pipeline execution");
            return new PipelineResult(70, $"Unexpected error: {ex.Message}"); // UNEXPECTED_ERROR
        }
    }

    private async Task<JsonElement> FetchExternalDataAsync(string baseUrl, SourceRequestDto request, string authToken, int timeoutSeconds)
    {
        // Use injected HttpClient or create new one
        var client = _httpClient ?? new HttpClient();
        var shouldDisposeClient = _httpClient == null;

        try
        {
            if (shouldDisposeClient)
            {
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            }
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {authToken}");

            // Construct full URL: baseUrl + path + queryParams
            var url = baseUrl.TrimEnd('/') + "/" + request.Path.TrimStart('/');
            if (request.QueryParams != null && request.QueryParams.Count > 0)
            {
                var queryString = string.Join("&", request.QueryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
                url = $"{url}?{queryString}";
            }

            Log.Information("FetchSource: {Method} {Url}", request.Method, url);

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
        finally
        {
            if (shouldDisposeClient)
            {
                client.Dispose();
            }
        }
    }

    private async Task<string> SaveCsvAsync(PipelineContext context, string csv)
    {
        // Build path per blob-and-local-storage.md: basePath/processId/yyyy/MM/dd/executionId.csv
        var now = DateTime.UtcNow;
        var datePath = $"{now:yyyy}/{now:MM}/{now:dd}";
        var fileName = $"{context.ExecutionId}.csv";

        if (context.OutputDestination == "local" || context.OutputDestination == "both")
        {
            var outputPath = context.OutputPath ?? "./exports";
            var fullDir = Path.Combine(outputPath, context.ProcessId, datePath);
            Directory.CreateDirectory(fullDir);
            var filePath = Path.Combine(fullDir, fileName);
            await File.WriteAllTextAsync(filePath, csv);
            Log.Information("CSV saved to {FilePath}", filePath);
            return filePath;
        }

        return $"blob://{context.ProcessId}/{datePath}/{fileName}";
    }
}

public record SourceRequestDto(
    string Method,
    string Path,
    Dictionary<string, string>? Headers = null,
    Dictionary<string, string>? QueryParams = null
);
