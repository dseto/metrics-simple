using System.Text.Json;
using Metrics.Api;
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
