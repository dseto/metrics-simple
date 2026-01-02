using System.Text.Json;
using Metrics.Api;
using Metrics.Api.AI;
using Metrics.Engine;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Metrics Simple Config API",
        Version = "v1",
        Description = "API de configuração para Metrics Simple (Spec-Driven)"
    });
});

// Get configuration - METRICS_SQLITE_PATH env var takes precedence (required for integration tests)
var dbPath = Environment.GetEnvironmentVariable("METRICS_SQLITE_PATH") 
    ?? builder.Configuration["Database:Path"] 
    ?? "./config/config.db";
var secretsPath = builder.Configuration["Secrets:Path"] ?? "./config/secrets.local.json";

// Initialize database
var dbProvider = new DatabaseProvider();
dbProvider.InitializeDatabase(dbPath);

// Register repositories
builder.Services.AddScoped<IProcessRepository>(_ => new ProcessRepository(dbPath));
builder.Services.AddScoped<IProcessVersionRepository>(_ => new ProcessVersionRepository(dbPath));
builder.Services.AddScoped<IConnectorRepository>(_ => new ConnectorRepository(dbPath));

// Register Engine services
builder.Services.AddScoped<IDslTransformer, JsonataTransformer>();
builder.Services.AddScoped<ISchemaValidator, SchemaValidator>();
builder.Services.AddScoped<ICsvGenerator, CsvGenerator>();
builder.Services.AddScoped<EngineService>();

// Register Secrets
builder.Services.AddScoped<ISecretsProvider>(_ => new SecretsProvider());

// Load AI configuration from appsettings
var aiConfig = builder.Configuration.GetSection("AI").Get<AiConfiguration>() ?? new AiConfiguration
{
    Enabled = false,
    Provider = "HttpOpenAICompatible",
    EndpointUrl = "https://openrouter.ai/api/v1/chat/completions",
    Model = "openai/gpt-4o-mini",
    PromptVersion = "1.0.0",
    TimeoutSeconds = 30,
    MaxRetries = 1,
    Temperature = 0.0,
    MaxTokens = 4096,
    TopP = 0.9
};

// API Key from environment variable takes precedence over appsettings
var apiKeyFromEnv = Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
if (!string.IsNullOrEmpty(apiKeyFromEnv))
{
    aiConfig.ApiKey = apiKeyFromEnv;
}

builder.Services.AddSingleton(aiConfig);

// Register AI Provider based on configuration
builder.Services.AddHttpClient<HttpOpenAiCompatibleProvider>();
builder.Services.AddSingleton<IAiProvider>(sp =>
{
    if (!aiConfig.Enabled)
    {
        // Return a disabled provider that will throw on invocation
        return new MockAiProvider(MockProviderConfig.WithError(
            AiErrorCodes.AiDisabled,
            "AI is disabled in configuration"));
    }

    if (aiConfig.Provider == "MockProvider")
    {
        return new MockAiProvider();
    }

    // Default to HttpOpenAICompatible
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(HttpOpenAiCompatibleProvider));
    var logger = sp.GetRequiredService<ILogger<HttpOpenAiCompatibleProvider>>();
    return new HttpOpenAiCompatibleProvider(httpClient, aiConfig, logger);
});

var app = builder.Build();

// Enable Swagger in all environments for spec-driven development
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Metrics Simple Config API v1");
    c.RoutePrefix = "swagger";
});

// Use HTTPS redirection only in non-test environments
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

// Health check
app.MapGet("/api/health", GetHealth)
    .WithName("Health");

// Process endpoints
var processGroup = app.MapGroup("/api/processes")
    .WithTags("Processes");

processGroup.MapGet("/", GetAllProcesses)
    .WithName("ListProcesses");

processGroup.MapPost("/", CreateProcess)
    .WithName("CreateProcess");

processGroup.MapGet("/{id}", GetProcessById)
    .WithName("GetProcess");

processGroup.MapPut("/{id}", UpdateProcess)
    .WithName("UpdateProcess");

processGroup.MapDelete("/{id}", DeleteProcess)
    .WithName("DeleteProcess");

// ProcessVersion endpoints
var versionGroup = app.MapGroup("/api/processes/{processId}/versions")
    .WithTags("ProcessVersions");

versionGroup.MapPost("/", CreateProcessVersion)
    .WithName("CreateProcessVersion");

versionGroup.MapGet("/{version}", GetProcessVersion)
    .WithName("GetProcessVersion");

versionGroup.MapPut("/{version}", UpdateProcessVersion)
    .WithName("UpdateProcessVersion");

// Connector endpoints
var connectorGroup = app.MapGroup("/api/connectors")
    .WithTags("Connectors");

connectorGroup.MapGet("/", GetAllConnectors)
    .WithName("ListConnectors");

connectorGroup.MapPost("/", CreateConnector)
    .WithName("CreateConnector");

// Preview Transform endpoint
app.MapPost("/api/preview/transform", PreviewTransform)
    .WithName("PreviewTransform")
    .WithTags("Preview");

// AI DSL Generate endpoint
app.MapPost("/api/ai/dsl/generate", GenerateDsl)
    .WithName("GenerateDsl")
    .WithTags("AI");

app.Run();

// Handlers
IResult GetHealth() => Results.Ok(new { status = "ok" });

async Task<IResult> GetAllProcesses(IProcessRepository repo)
{
    var processes = await repo.GetAllProcessesAsync();
    return Results.Ok(processes);
}

async Task<IResult> CreateProcess(ProcessDto process, IProcessRepository repo)
{
    var created = await repo.CreateProcessAsync(process);
    return Results.Created($"/api/processes/{created.Id}", created);
}

async Task<IResult> GetProcessById(string id, IProcessRepository repo)
{
    var process = await repo.GetProcessByIdAsync(id);
    if (process == null) return Results.NotFound();
    return Results.Ok(process);
}

async Task<IResult> UpdateProcess(string id, ProcessDto process, IProcessRepository repo)
{
    var updated = await repo.UpdateProcessAsync(id, process);
    return Results.Ok(updated);
}

async Task<IResult> DeleteProcess(string id, IProcessRepository repo)
{
    await repo.DeleteProcessAsync(id);
    return Results.NoContent();
}

async Task<IResult> CreateProcessVersion(string processId, ProcessVersionDto version, IProcessVersionRepository repo)
{
    var created = await repo.CreateVersionAsync(version with { ProcessId = processId });
    return Results.Created($"/api/processes/{processId}/versions/{created.Version}", created);
}

async Task<IResult> GetProcessVersion(string processId, string version, IProcessVersionRepository repo)
{
    var pv = await repo.GetVersionAsync(processId, version);
    if (pv == null) return Results.NotFound();
    return Results.Ok(pv);
}

async Task<IResult> UpdateProcessVersion(string processId, string version, ProcessVersionDto updated, IProcessVersionRepository repo)
{
    var result = await repo.UpdateVersionAsync(processId, version, updated);
    return Results.Ok(result);
}

async Task<IResult> GetAllConnectors(IConnectorRepository repo)
{
    var connectors = await repo.GetAllConnectorsAsync();
    return Results.Ok(connectors);
}

async Task<IResult> CreateConnector(ConnectorDto connector, IConnectorRepository repo)
{
    var created = await repo.CreateConnectorAsync(connector);
    return Results.Created($"/api/connectors/{created.Id}", created);
}

async Task<IResult> PreviewTransform(PreviewTransformRequestDto request, EngineService engine)
{
    try
    {
        // Use sample input as the transformation input
        var inputJson = JsonSerializer.SerializeToElement(request.SampleInput);
        var outputSchemaJson = JsonSerializer.SerializeToElement(request.OutputSchema);

        var result = engine.TransformValidateToCsv(inputJson, request.Dsl.Profile, request.Dsl.Text, outputSchemaJson);

        var response = new PreviewTransformResponseDto(
            result.IsValid,
            result.Errors.ToList(),
            result.OutputJson,
            result.CsvPreview
        );

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new PreviewTransformResponseDto(false, new List<string> { ex.Message }));
    }
}

async Task<IResult> GenerateDsl(
    DslGenerateRequest request,
    IAiProvider aiProvider,
    AiConfiguration aiConfig,
    EngineService engine,
    HttpContext httpContext,
    ILogger<Program> logger)
{
    // Generate correlation ID
    var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N")[..12];

    try
    {
        // Check if AI is enabled
        if (!aiConfig.Enabled)
        {
            return Results.Json(new AiError
            {
                Code = AiErrorCodes.AiDisabled,
                Message = "AI functionality is disabled. Enable it in appsettings.json under AI.Enabled.",
                CorrelationId = correlationId
            }, statusCode: 503);
        }

        // Validate request against guardrails
        var requestValidation = AiGuardrails.ValidateRequest(request);
        if (!requestValidation.IsValid)
        {
            return Results.Json(new AiError
            {
                Code = AiErrorCodes.AiOutputInvalid,
                Message = "Request validation failed",
                Details = requestValidation.Errors,
                CorrelationId = correlationId
            }, statusCode: 400);
        }

        // Log request (without sensitive data)
        var inputHash = AiGuardrails.ComputeInputHash(request.SampleInput);
        logger.LogInformation(
            "AI DSL Generate: CorrelationId={CorrelationId}, Profile={Profile}, GoalLength={GoalLength}, InputHash={InputHash}",
            correlationId, request.DslProfile, request.GoalText.Length, inputHash);

        // Call AI provider with optional repair attempt
        var startTime = DateTime.UtcNow;
        const int maxRepairAttempts = 1;
        
        DslGenerateResult? result = null;
        List<string>? lastErrors = null;
        string? lastDslAttempt = null;
        
        for (int attempt = 0; attempt <= maxRepairAttempts; attempt++)
        {
            var isRepairAttempt = attempt > 0;
            
            // Build request (with repair hints if this is a retry)
            var currentRequest = request;
            if (isRepairAttempt && lastErrors != null && lastDslAttempt != null)
            {
                logger.LogInformation(
                    "AI DSL Repair Attempt: CorrelationId={CorrelationId}, Attempt={Attempt}",
                    correlationId, attempt);
                
                // Create repair request with hints containing validation errors
                var repairHints = new Dictionary<string, string>
                {
                    ["ValidationErrors"] = string.Join("; ", lastErrors),
                    ["JsonataDialectRules"] = "Review: no $.path (root is implicit), no [!cond] (use [cond=false] or [not cond]), $sum(array) and $average(array) are valid, $match returns [0].groups[n]"
                };
                
                currentRequest = request with
                {
                    ExistingDsl = lastDslAttempt,
                    Hints = repairHints
                };
            }
            
            try
            {
                result = await aiProvider.GenerateDslAsync(currentRequest, httpContext.RequestAborted);
            }
            catch (AiProviderException ex)
            {
                logger.LogError(ex, "AI provider error: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                return Results.Json(new AiError
                {
                    Code = ex.ErrorCode,
                    Message = ex.Message,
                    Details = ex.Details,
                    CorrelationId = correlationId
                }, statusCode: ex.ErrorCode == AiErrorCodes.AiOutputInvalid ? 502 : 503);
            }

            // Validate result structure
            var resultValidation = await AiGuardrails.ValidateResultAsync(result);
            if (!resultValidation.IsValid)
            {
                lastErrors = resultValidation.Errors.Select(e => $"{e.Path}: {e.Message}").ToList();
                lastDslAttempt = result.Dsl.Text;
                
                if (attempt >= maxRepairAttempts)
                {
                    logger.LogWarning("AI output validation failed after repair: {Errors}", string.Join(", ", lastErrors));
                    return Results.Json(new AiError
                    {
                        Code = AiErrorCodes.AiOutputInvalid,
                        Message = "AI provider returned invalid output after repair attempt",
                        Details = resultValidation.Errors,
                        CorrelationId = correlationId
                    }, statusCode: 502);
                }
                continue;
            }

            // Run preview/validation using the Engine
            try
            {
                var previewResult = engine.TransformValidateToCsv(
                    request.SampleInput,
                    result.Dsl.Profile,
                    result.Dsl.Text,
                    result.OutputSchema);

                if (!previewResult.IsValid)
                {
                    lastErrors = previewResult.Errors.ToList();
                    lastDslAttempt = result.Dsl.Text;
                    
                    // Compute DSL hash for debugging
                    var dslHash = AiGuardrails.ComputeDslHash(result.Dsl.Text);
                    var dslPreview = result.Dsl.Text.Length > 200 
                        ? result.Dsl.Text[..200] + "..." 
                        : result.Dsl.Text;
                    
                    if (attempt >= maxRepairAttempts)
                    {
                        logger.LogWarning("AI-generated DSL preview failed after repair: DSL_INVALID: Failed to parse/compile Jsonata expression. dslHash={DslHash} dslPreview={DslPreview}",
                            dslHash, dslPreview);
                        return Results.Json(new AiError
                        {
                            Code = AiErrorCodes.AiOutputInvalid,
                            Message = "AI-generated DSL failed preview validation after repair attempt",
                            Details = lastErrors.Select(e => new AiErrorDetail
                            {
                                Path = "preview",
                                Message = e
                            }).ToList(),
                            CorrelationId = correlationId
                        }, statusCode: 502);
                    }
                    
                    logger.LogInformation("DSL preview failed, attempting repair: {Errors}", string.Join(", ", lastErrors));
                    continue;
                }
                
                // Success! DSL validated successfully
                break;
            }
            catch (Exception ex)
            {
                lastErrors = new List<string> { ex.Message };
                lastDslAttempt = result.Dsl.Text;
                
                if (attempt >= maxRepairAttempts)
                {
                    logger.LogWarning(ex, "AI-generated DSL preview threw exception after repair");
                    return Results.Json(new AiError
                    {
                        Code = AiErrorCodes.AiOutputInvalid,
                        Message = $"AI-generated DSL failed preview after repair: {ex.Message}",
                        CorrelationId = correlationId
                    }, statusCode: 502);
                }
                
                logger.LogInformation("DSL preview exception, attempting repair: {Error}", ex.Message);
                continue;
            }
        }

        var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;
        
        logger.LogInformation(
            "AI DSL Generate success: CorrelationId={CorrelationId}, Latency={Latency}ms, DslLength={DslLength}",
            correlationId, latency, result!.Dsl.Text.Length);

        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error in AI DSL generate: {Message}", ex.Message);
        return Results.Json(new AiError
        {
            Code = AiErrorCodes.AiProviderUnavailable,
            Message = "An unexpected error occurred",
            CorrelationId = correlationId
        }, statusCode: 503);
    }
}
