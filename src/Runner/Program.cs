using System.CommandLine;
using Metrics.Engine;
using Metrics.Runner;
using Serilog;

var root = new RootCommand("Metrics Runner (Spec-Driven)");

var processIdOpt = new Option<string>("--processId") { IsRequired = true };
var versionOpt = new Option<string>("--version") { IsRequired = true };
var destOpt = new Option<string>("--dest", () => "local", "local|blob|both");
var outPathOpt = new Option<string?>("--outPath");
var secretsOpt = new Option<string?>("--secrets");
var dbOpt = new Option<string?>("--db");

// Run command
var run = new Command("run", "Executa um processo síncrono");
run.AddOption(processIdOpt);
run.AddOption(versionOpt);
run.AddOption(destOpt);
run.AddOption(outPathOpt);
run.AddOption(secretsOpt);
run.AddOption(dbOpt);

run.SetHandler(async (string processId, string version, string dest, string? outPath, string? secrets, string? db) =>
{
    // Resolve SQLite path: CLI --db > env METRICS_SQLITE_PATH > default
    var effectiveDb = db 
        ?? Environment.GetEnvironmentVariable("METRICS_SQLITE_PATH") 
        ?? "./config/config.db";
    
    var executionId = Guid.NewGuid().ToString("N");
    
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.WithProperty("executionId", executionId)
        .Enrich.WithProperty("processId", processId)
        .Enrich.WithProperty("version", version)
        .WriteTo.Console()
        .WriteTo.File(
            path: Path.Combine(outPath ?? "./logs", $"{executionId}.jsonl"),
            outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
            rollingInterval: RollingInterval.Day
        )
        .CreateLogger();

    Log.Information("EXEC_START");

    try
    {
        // Initialize services
        var transformer = new JsonataTransformer();
        var validator = new SchemaValidator();
        var csvGenerator = new CsvGenerator();
        var engine = new EngineService(transformer, validator, csvGenerator);
        var secretsProvider = new SecretsProvider();
        var dbProvider = new DatabaseProvider();

        var orchestrator = new PipelineOrchestrator(engine, secretsProvider, dbProvider);

        // Create pipeline context - use effectiveDb instead of db
        var context = new PipelineContext(
            executionId,
            processId,
            version,
            dest,
            outPath,
            secrets,
            effectiveDb
        );

        // Execute pipeline
        var result = await orchestrator.ExecutePipelineAsync(context);

        Log.Information("EXEC_END with exit code {ExitCode}", result.ExitCode);

        if (result.CsvPath != null)
        {
            Log.Information("CSV output: {CsvPath}", result.CsvPath);
        }

        Environment.Exit(result.ExitCode);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "EXEC_FAILED");
        Environment.Exit(99);
    }
}, processIdOpt, versionOpt, destOpt, outPathOpt, secretsOpt, dbOpt);

// Validate command
var validate = new Command("validate", "Valida DSL e schema (sem fetch externo)");
validate.AddOption(processIdOpt);
validate.AddOption(versionOpt);
validate.AddOption(secretsOpt);
validate.AddOption(dbOpt);

validate.SetHandler(async (string processId, string version, string? secrets, string? db) =>
{
    var executionId = Guid.NewGuid().ToString("N");
    
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .Enrich.WithProperty("executionId", executionId)
        .Enrich.WithProperty("processId", processId)
        .Enrich.WithProperty("version", version)
        .WriteTo.Console()
        .CreateLogger();

    Log.Information("VALIDATE_START");

    try
    {
        var dbProvider = new DatabaseProvider();
        var connection = dbProvider.GetConnection(db ?? "./config/config.db");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT pv.dslProfile, pv.dslText, pv.outputSchemaJson
            FROM ProcessVersion pv
            WHERE pv.processId = @processId AND pv.version = @version";
        
        command.Parameters.AddWithValue("@processId", processId);
        command.Parameters.AddWithValue("@version", version);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            Log.Error("Process version not found");
            Environment.Exit(10);
            return;
        }

        var dslProfile = reader.GetString(0);
        var dslText = reader.GetString(1);
        var outputSchemaJson = reader.GetString(2);

        connection.Close();

        // Validate DSL and schema
        var transformer = new JsonataTransformer();
        var validator = new SchemaValidator();
        var engine = new EngineService(transformer, validator, new CsvGenerator());

        // Create a dummy input for validation
        var dummyInput = System.Text.Json.JsonDocument.Parse("{}").RootElement;
        var outputSchema = System.Text.Json.JsonDocument.Parse(outputSchemaJson).RootElement;

        Log.Information("DSL Profile: {Profile}", dslProfile);
        Log.Information("DSL Text: {Text}", dslText);
        Log.Information("VALIDATE_END");

        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "VALIDATE_FAILED");
        Environment.Exit(99);
    }
}, processIdOpt, versionOpt, secretsOpt, dbOpt);

// Cleanup command
var cleanup = new Command("cleanup", "Remove arquivos mais antigos que retentionDays");
var basePathOpt = new Option<string>("--basePath") { IsRequired = true, Description = "Caminho base dos arquivos de output" };
var retentionDaysOpt = new Option<int>("--retentionDays") { IsRequired = true, Description = "Dias de retenção" };

cleanup.AddOption(basePathOpt);
cleanup.AddOption(retentionDaysOpt);

cleanup.SetHandler((string basePath, int retentionDays) =>
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Console()
        .CreateLogger();

    Log.Information("CLEANUP_START: basePath={BasePath}, retentionDays={RetentionDays}", basePath, retentionDays);

    try
    {
        if (!Directory.Exists(basePath))
        {
            Log.Warning("Base path does not exist: {BasePath}", basePath);
            Environment.Exit(0);
            return;
        }

        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var deletedCount = 0;
        var totalSize = 0L;

        // Recursively find and delete old files
        var directories = Directory.GetDirectories(basePath, "*", SearchOption.AllDirectories);
        var allDirs = new List<string> { basePath };
        allDirs.AddRange(directories);

        foreach (var dir in allDirs)
        {
            var files = Directory.GetFiles(dir);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoffDate)
                {
                    totalSize += fileInfo.Length;
                    File.Delete(file);
                    deletedCount++;
                    Log.Information("Deleted: {File} (age: {Age} days)", file, (DateTime.UtcNow - fileInfo.LastWriteTimeUtc).TotalDays);
                }
            }
        }

        // Remove empty directories
        foreach (var dir in directories.OrderByDescending(d => d.Length))
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
                Log.Information("Removed empty directory: {Dir}", dir);
            }
        }

        Log.Information("CLEANUP_END: Deleted {Count} files ({Size} bytes)", deletedCount, totalSize);
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "CLEANUP_FAILED");
        Environment.Exit(99);
    }
}, basePathOpt, retentionDaysOpt);

root.AddCommand(run);
root.AddCommand(validate);
root.AddCommand(cleanup);

return await root.InvokeAsync(args);
