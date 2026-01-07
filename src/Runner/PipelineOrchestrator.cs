using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Metrics.Engine;
using Serilog;
using Metrics.Api.AI.Engines.Ai;

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

            // Load Connector (v1.2.0 schema - no authRef, with authType)
            var connectorCommand = connection.CreateCommand();
            connectorCommand.CommandText = @"
                SELECT baseUrl, timeoutSeconds, authType, authConfigJson FROM Connector WHERE id = @connectorId";
            connectorCommand.Parameters.AddWithValue("@connectorId", connectorId);

            using var connectorReader = connectorCommand.ExecuteReader();
            if (!connectorReader.Read())
            {
                Log.Error("Connector not found: connectorId={ConnectorId}", connectorId);
                return new PipelineResult(20, $"Connector not found: {connectorId}"); // NOT_FOUND
            }

            var baseUrl = connectorReader.GetString(0);
            var timeoutSeconds = connectorReader.GetInt32(1);
            var authType = connectorReader.IsDBNull(2) ? "NONE" : connectorReader.GetString(2);
            var authConfigJson = connectorReader.IsDBNull(3) ? null : connectorReader.GetString(3);
            connectorReader.Close();

            // Load encrypted secrets from connector_secrets table (v1.2.0+)
            string? bearerToken = null;
            string? apiKeyValue = null;
            string? basicPassword = null;
            string? apiKeyName = null;
            string? apiKeyLocation = null;
            string? basicUsername = null;

            // Parse authConfig for non-secret fields
            if (!string.IsNullOrEmpty(authConfigJson))
            {
                try
                {
                    var authConfig = JsonSerializer.Deserialize<AuthConfigData>(authConfigJson);
                    apiKeyName = authConfig?.ApiKeyName;
                    apiKeyLocation = authConfig?.ApiKeyLocation;
                    basicUsername = authConfig?.BasicUsername;
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to parse authConfigJson: {Error}", ex.Message);
                }
            }

            // Load all secrets for this connector
            var secretsCommand = connection.CreateCommand();
            secretsCommand.CommandText = @"
                SELECT secretKind, encNonce, encCiphertext 
                FROM connector_secrets 
                WHERE connectorId = @connectorId";
            secretsCommand.Parameters.AddWithValue("@connectorId", connectorId);

            using var secretsReader = secretsCommand.ExecuteReader();
            var encryptionService = new TokenEncryptionService();
            
            while (secretsReader.Read())
            {
                var secretKind = secretsReader.GetString(0);
                var encNonce = secretsReader.GetString(1);
                var encCiphertext = secretsReader.GetString(2);

                try
                {
                    var decryptedValue = encryptionService.Decrypt(encNonce, encCiphertext);
                    switch (secretKind)
                    {
                        case "BEARER_TOKEN":
                            bearerToken = decryptedValue;
                            break;
                        case "API_KEY_VALUE":
                            apiKeyValue = decryptedValue;
                            break;
                        case "BASIC_PASSWORD":
                            basicPassword = decryptedValue;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to decrypt secret {SecretKind}: {Error}", secretKind, ex.Message);
                }
            }
            secretsReader.Close();

            // Fallback: Try loading from legacy connector_tokens table
            if (string.IsNullOrEmpty(bearerToken))
            {
                var tokenCommand = connection.CreateCommand();
                tokenCommand.CommandText = @"
                    SELECT encVersion, encAlg, encNonce, encCiphertext 
                    FROM connector_tokens 
                    WHERE connectorId = @connectorId";
                tokenCommand.Parameters.AddWithValue("@connectorId", connectorId);

                using var tokenReader = tokenCommand.ExecuteReader();
                if (tokenReader.Read())
                {
                    var encNonce = tokenReader.GetString(2);
                    var encCiphertext = tokenReader.GetString(3);
                    tokenReader.Close();

                    try
                    {
                        bearerToken = encryptionService.Decrypt(encNonce, encCiphertext);
                        Log.Information("Legacy API token loaded for connector {ConnectorId}", connectorId);
                    }
                    catch (InvalidOperationException ex)
                    {
                        Log.Error("Failed to decrypt legacy API token: {Error}", ex.Message);
                        connection.Close();
                        return new PipelineResult(40, $"Token decryption failed: {ex.Message}"); // SOURCE_ERROR
                    }
                    catch (CryptographicException ex)
                    {
                        Log.Error("Failed to decrypt legacy API token: {Error}", ex.Message);
                        connection.Close();
                        return new PipelineResult(40, $"Token decryption failed: {ex.Message}"); // SOURCE_ERROR
                    }
                }
                else
                {
                    tokenReader.Close();
                }
            }

            connection.Close();

            // Step 2: Resolve auth based on authType (v1.2.0+)
            Log.Information("Step 2: Resolving auth for authType={AuthType}", authType);

            AuthResolution auth;
            switch (authType.ToUpperInvariant())
            {
                case "NONE":
                    auth = new AuthResolution("NONE");
                    break;

                case "BEARER":
                    // Try encrypted secret first, then env var fallback
                    var resolvedBearer = bearerToken;
                    if (string.IsNullOrEmpty(resolvedBearer))
                    {
                        var secretEnvKey = $"METRICS_SECRET__{connectorId}__BEARER";
                        resolvedBearer = Environment.GetEnvironmentVariable(secretEnvKey);
                        
                        // Fallback: try secrets file
                        if (string.IsNullOrEmpty(resolvedBearer) && !string.IsNullOrEmpty(context.SecretsPath))
                        {
                            try
                            {
                                var secretConfig = _secretsProvider.LoadSecrets(context.SecretsPath);
                                resolvedBearer = _secretsProvider.GetConnectorSecret(secretConfig, connectorId, "token");
                            }
                            catch
                            {
                                // Ignore file-based secrets errors
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(resolvedBearer))
                    {
                        Log.Error("Bearer token not found for connector {ConnectorId}", connectorId);
                        return new PipelineResult(40, $"Bearer token not found for connector: {connectorId}"); // SOURCE_ERROR
                    }
                    auth = new AuthResolution("BEARER", BearerToken: resolvedBearer);
                    break;

                case "API_KEY":
                    // Try encrypted secret first, then env var fallback
                    var resolvedApiKey = apiKeyValue;
                    if (string.IsNullOrEmpty(resolvedApiKey))
                    {
                        var secretEnvKey = $"METRICS_SECRET__{connectorId}__API_KEY";
                        resolvedApiKey = Environment.GetEnvironmentVariable(secretEnvKey);
                    }

                    if (string.IsNullOrEmpty(resolvedApiKey))
                    {
                        Log.Error("API key not found for connector {ConnectorId}", connectorId);
                        return new PipelineResult(40, $"API key not found for connector: {connectorId}"); // SOURCE_ERROR
                    }
                    if (string.IsNullOrEmpty(apiKeyName))
                    {
                        Log.Error("API key name not configured for connector {ConnectorId}", connectorId);
                        return new PipelineResult(10, $"API key name not configured for connector: {connectorId}"); // VALIDATION_ERROR
                    }
                    auth = new AuthResolution("API_KEY", 
                        ApiKeyValue: resolvedApiKey, 
                        ApiKeyName: apiKeyName, 
                        ApiKeyLocation: apiKeyLocation ?? "HEADER");
                    break;

                case "BASIC":
                    // Try encrypted secret first, then env var fallback
                    var resolvedBasicPassword = basicPassword;
                    if (string.IsNullOrEmpty(resolvedBasicPassword))
                    {
                        var secretEnvKey = $"METRICS_SECRET__{connectorId}__BASIC";
                        resolvedBasicPassword = Environment.GetEnvironmentVariable(secretEnvKey);
                    }

                    if (string.IsNullOrEmpty(resolvedBasicPassword))
                    {
                        Log.Error("Basic password not found for connector {ConnectorId}", connectorId);
                        return new PipelineResult(40, $"Basic password not found for connector: {connectorId}"); // SOURCE_ERROR
                    }
                    if (string.IsNullOrEmpty(basicUsername))
                    {
                        Log.Error("Basic username not configured for connector {ConnectorId}", connectorId);
                        return new PipelineResult(10, $"Basic username not configured for connector: {connectorId}"); // VALIDATION_ERROR
                    }
                    auth = new AuthResolution("BASIC", 
                        BasicUsername: basicUsername, 
                        BasicPassword: resolvedBasicPassword);
                    break;

                default:
                    Log.Error("Unknown auth type {AuthType} for connector {ConnectorId}", authType, connectorId);
                    return new PipelineResult(10, $"Unknown auth type: {authType}"); // VALIDATION_ERROR
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
                inputData = await FetchExternalDataAsync(baseUrl, sourceRequest, auth, timeoutSeconds);
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
            Log.Information("Step 4: Transform - Executing transformation (profile: {Profile})", dslProfile);
            var outputSchema = JsonDocument.Parse(outputSchemaJson).RootElement;
            
            // Only support IR (plan-based) execution
            if (!string.Equals(dslProfile, "ir", StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("Unsupported DSL profile: {Profile}", dslProfile);
                return new PipelineResult(50, $"Unsupported DSL profile '{dslProfile}'. Only 'ir' profile is supported."); // TRANSFORM_ERROR
            }

            // Deserialize and execute plan
            TransformPlan? plan;
            try
            {
                plan = JsonSerializer.Deserialize<TransformPlan>(dslText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (plan == null)
                {
                    Log.Error("Plan deserialization failed: null result");
                    return new PipelineResult(50, "Plan deserialization failed"); // TRANSFORM_ERROR
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Plan deserialization failed");
                return new PipelineResult(50, $"Plan deserialization failed: {ex.Message}"); // TRANSFORM_ERROR
            }

            // Execute plan
            var execResult = PlanExecutor.Execute(plan, inputData);
            if (!execResult.Success)
            {
                Log.Error("Plan execution failed: {Error}", execResult.Error);
                return new PipelineResult(50, $"Plan execution failed: {execResult.Error}"); // TRANSFORM_ERROR
            }

            // Normalize rows to JsonElement
            var rowsJson = ShapeNormalizer.ToJsonElement(execResult.Rows!);

            // Validate and generate CSV
            var transformResult = _engine.TransformValidateToCsvFromRows(rowsJson, outputSchema);

            if (!transformResult.IsValid)
            {
                Log.Error("Transform validation failed: {Errors}", string.Join(", ", transformResult.Errors));
                return new PipelineResult(50, $"Transform validation failed: {string.Join(", ", transformResult.Errors)}"); // TRANSFORM_ERROR
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

    private async Task<JsonElement> FetchExternalDataAsync(string baseUrl, SourceRequestDto request, AuthResolution auth, int timeoutSeconds)
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

            // Apply auth based on type
            switch (auth.AuthType.ToUpperInvariant())
            {
                case "BEARER":
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {auth.BearerToken}");
                    break;

                case "BASIC":
                    var basicCredentials = Convert.ToBase64String(
                        System.Text.Encoding.UTF8.GetBytes($"{auth.BasicUsername}:{auth.BasicPassword}"));
                    client.DefaultRequestHeaders.Add("Authorization", $"Basic {basicCredentials}");
                    break;

                case "API_KEY":
                    // API key is injected later based on location (header or query)
                    break;

                case "NONE":
                    // No auth
                    break;
            }

            // Construct full URL: baseUrl + path + queryParams
            var url = baseUrl.TrimEnd('/') + "/" + request.Path.TrimStart('/');
            var queryParams = request.QueryParams != null 
                ? new Dictionary<string, string>(request.QueryParams) 
                : new Dictionary<string, string>();

            // For API_KEY with QUERY location, add to query params
            if (auth.AuthType.ToUpperInvariant() == "API_KEY" && 
                auth.ApiKeyLocation?.ToUpperInvariant() == "QUERY" &&
                !string.IsNullOrEmpty(auth.ApiKeyName))
            {
                queryParams[auth.ApiKeyName] = auth.ApiKeyValue!;
            }
            // For API_KEY with HEADER location (default), add to headers
            else if (auth.AuthType.ToUpperInvariant() == "API_KEY" && 
                     !string.IsNullOrEmpty(auth.ApiKeyName))
            {
                client.DefaultRequestHeaders.Add(auth.ApiKeyName, auth.ApiKeyValue);
            }

            if (queryParams.Count > 0)
            {
                var queryString = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
                url = $"{url}?{queryString}";
            }

            Log.Information("FetchSource: {Method} {Url} (auth={AuthType})", request.Method, url, auth.AuthType);

            HttpContent? httpContent = null;
            if (!string.IsNullOrEmpty(request.Body))
            {
                httpContent = new StringContent(request.Body, System.Text.Encoding.UTF8, 
                    request.ContentType ?? "application/json");
            }

            HttpResponseMessage response = request.Method.ToUpper() switch
            {
                "GET" => await client.GetAsync(url),
                "POST" => await client.PostAsync(url, httpContent),
                "PUT" => await client.PutAsync(url, httpContent),
                "PATCH" => await client.PatchAsync(url, httpContent),
                "DELETE" => await client.DeleteAsync(url),
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
    Dictionary<string, string>? QueryParams = null,
    string? Body = null,
    string? ContentType = null
);

/// <summary>
/// Configuration data for non-secret auth fields stored in authConfigJson
/// </summary>
public record AuthConfigData
{
    public string? ApiKeyName { get; init; }
    public string? ApiKeyLocation { get; init; }
    public string? BasicUsername { get; init; }
}

/// <summary>
/// Auth resolution result for passing to FetchExternalDataAsync
/// </summary>
public record AuthResolution(
    string AuthType,
    string? BearerToken = null,
    string? ApiKeyValue = null,
    string? ApiKeyName = null,
    string? ApiKeyLocation = null,
    string? BasicUsername = null,
    string? BasicPassword = null
);
