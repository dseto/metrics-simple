using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT03 — Falha de source (OBRIGATÓRIO)
/// 
/// Per integration-tests.md:
/// - Mock server returns 500 (or controlled timeout)
/// - Runner should fail with exit code = 40 (SOURCE_ERROR)
/// - Logs indicate step FetchSource and corresponding errorCode
/// </summary>
public class IT03_SourceFailureTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _outputPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly WireMockServer _mockServer;

    public IT03_SourceFailureTests()
    {
        _dbPath = TestFixtures.CreateTempDbPath();
        _outputPath = TestFixtures.CreateTempOutputDirectory();
        _factory = new TestWebApplicationFactory(_dbPath);
        _client = _factory.CreateClient();
        
        // Start WireMock server on random port
        _mockServer = WireMockServer.Start();
    }

    public void Dispose()
    {
        _mockServer.Stop();
        _mockServer.Dispose();
        _client.Dispose();
        _factory.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
        TestFixtures.CleanupTempDirectory(_outputPath);
    }

    [Fact]
    public async Task Runner_WhenSourceReturns500_ExitsWith40_SourceError()
    {
        // Arrange: Configure WireMock to return 500 Internal Server Error
        _mockServer
            .Given(Request.Create()
                .WithPath("/v1/servers")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"error\": \"Internal Server Error\"}"));

        // Create configuration via API
        var connectorId = "conn-fail-001";
        var processId = "proc-fail-001";
        var authRef = "api_key_prod";

        var connector = new ConnectorDto(
            Id: connectorId,
            Name: "Fail Test Connector",
            BaseUrl: _mockServer.Url!,
            AuthRef: authRef,
            TimeoutSeconds: 30
        );
        var connResponse = await _client.PostAsJsonAsync("/api/connectors", connector);
        connResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var process = new ProcessDto(
            Id: processId,
            Name: "Fail Test Process",
            Status: "Active",
            ConnectorId: connectorId,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(Type: "LocalFileSystem", Local: new LocalFileSystemDto(_outputPath))
            }
        );
        await _client.PostAsJsonAsync("/api/processes", process);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: processId,
            Version: "1",
            Enabled: true,
            SourceRequest: new SourceRequestDto(
                Method: "GET",
                Path: "/v1/servers",
                QueryParams: new Dictionary<string, string>
                {
                    ["limit"] = "100",
                    ["filter"] = "active"
                }
            ),
            Dsl: new DslDto(Profile: "jsonata", Text: TestFixtures.GetHostsCpuDsl()),
            OutputSchema: outputSchema
        );
        await _client.PostAsJsonAsync($"/api/processes/{processId}/versions", version);

        // Act: Execute Runner CLI
        var runnerResult = await RunRunnerProcessAsync(processId, "1", _outputPath, _dbPath, authRef);

        // Assert: Exit code should be 40 (SOURCE_ERROR) per cli-contract.md
        runnerResult.ExitCode.Should().Be(40, 
            $"Runner should exit with 40 (SOURCE_ERROR) when source returns 500. Stdout: {runnerResult.StdOut}, Stderr: {runnerResult.StdErr}");

        // Verify WireMock received the request (FetchSource was attempted)
        var requests = _mockServer.LogEntries;
        requests.Should().HaveCount(1, "WireMock should receive exactly 1 request from FetchSource attempt");

        // Verify logs contain FetchSource failure indication
        var combinedOutput = runnerResult.StdOut + runnerResult.StdErr;
        combinedOutput.Should().Contain("FetchSource", "Logs should mention FetchSource step");
    }

    [Fact]
    public async Task Runner_WhenSourceReturns404_ExitsWith40_SourceError()
    {
        // Arrange: Configure WireMock to return 404 Not Found
        _mockServer
            .Given(Request.Create()
                .WithPath("/v1/servers")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(404)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"error\": \"Not Found\"}"));

        var connectorId = "conn-404-001";
        var processId = "proc-404-001";
        var authRef = "api_key_prod";

        var connector = new ConnectorDto(
            Id: connectorId,
            Name: "404 Test Connector",
            BaseUrl: _mockServer.Url!,
            AuthRef: authRef,
            TimeoutSeconds: 30
        );
        await _client.PostAsJsonAsync("/api/connectors", connector);

        var process = new ProcessDto(
            Id: processId,
            Name: "404 Test Process",
            Status: "Active",
            ConnectorId: connectorId,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(Type: "LocalFileSystem", Local: new LocalFileSystemDto(_outputPath))
            }
        );
        await _client.PostAsJsonAsync("/api/processes", process);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: processId,
            Version: "1",
            Enabled: true,
            SourceRequest: new SourceRequestDto(
                Method: "GET",
                Path: "/v1/servers",
                QueryParams: new Dictionary<string, string>
                {
                    ["limit"] = "100"
                }
            ),
            Dsl: new DslDto(Profile: "jsonata", Text: TestFixtures.GetHostsCpuDsl()),
            OutputSchema: outputSchema
        );
        await _client.PostAsJsonAsync($"/api/processes/{processId}/versions", version);

        // Act
        var runnerResult = await RunRunnerProcessAsync(processId, "1", _outputPath, _dbPath, authRef);

        // Assert: Exit code 40 (SOURCE_ERROR)
        runnerResult.ExitCode.Should().Be(40, "Runner should exit with 40 (SOURCE_ERROR) when source returns 404");
    }

    [Fact]
    public async Task Runner_WhenSecretNotFound_ExitsWith40_SourceError()
    {
        // Arrange: Create config but DON'T set the secret env var
        var inputJson = TestFixtures.GetHostsCpuInputJson();
        _mockServer
            .Given(Request.Create()
                .WithPath("/v1/servers")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody(inputJson));

        var connectorId = "conn-nosecret-001";
        var processId = "proc-nosecret-001";
        var authRef = "missing_secret_key";  // This secret won't be set

        var connector = new ConnectorDto(
            Id: connectorId,
            Name: "No Secret Connector",
            BaseUrl: _mockServer.Url!,
            AuthRef: authRef,
            TimeoutSeconds: 30
        );
        await _client.PostAsJsonAsync("/api/connectors", connector);

        var process = new ProcessDto(
            Id: processId,
            Name: "No Secret Process",
            Status: "Active",
            ConnectorId: connectorId,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(Type: "LocalFileSystem", Local: new LocalFileSystemDto(_outputPath))
            }
        );
        await _client.PostAsJsonAsync("/api/processes", process);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: processId,
            Version: "1",
            Enabled: true,
            SourceRequest: new SourceRequestDto(
                Method: "GET",
                Path: "/v1/servers",
                QueryParams: new Dictionary<string, string>()
            ),
            Dsl: new DslDto(Profile: "jsonata", Text: TestFixtures.GetHostsCpuDsl()),
            OutputSchema: outputSchema
        );
        await _client.PostAsJsonAsync($"/api/processes/{processId}/versions", version);

        // Act: Run WITHOUT setting the secret env var
        var runnerResult = await RunRunnerProcessWithoutSecretAsync(processId, "1", _outputPath, _dbPath);

        // Assert: Exit code 40 (SOURCE_ERROR) per spec - missing secret means we can't fetch
        runnerResult.ExitCode.Should().Be(40, 
            $"Runner should exit with 40 (SOURCE_ERROR) when secret not found. Stdout: {runnerResult.StdOut}, Stderr: {runnerResult.StdErr}");
    }

    [Fact]
    public async Task Runner_WhenProcessDisabled_ExitsWith30_Disabled()
    {
        // Arrange: Create config with DISABLED version
        var connectorId = "conn-disabled-001";
        var processId = "proc-disabled-001";
        var authRef = "api_key_prod";

        var connector = new ConnectorDto(
            Id: connectorId,
            Name: "Disabled Test Connector",
            BaseUrl: _mockServer.Url!,
            AuthRef: authRef,
            TimeoutSeconds: 30
        );
        await _client.PostAsJsonAsync("/api/connectors", connector);

        var process = new ProcessDto(
            Id: processId,
            Name: "Disabled Test Process",
            Status: "Active",
            ConnectorId: connectorId,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(Type: "LocalFileSystem", Local: new LocalFileSystemDto(_outputPath))
            }
        );
        await _client.PostAsJsonAsync("/api/processes", process);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: processId,
            Version: "1",
            Enabled: false,  // DISABLED
            SourceRequest: new SourceRequestDto(
                Method: "GET",
                Path: "/v1/servers",
                QueryParams: new Dictionary<string, string>()
            ),
            Dsl: new DslDto(Profile: "jsonata", Text: TestFixtures.GetHostsCpuDsl()),
            OutputSchema: outputSchema
        );
        await _client.PostAsJsonAsync($"/api/processes/{processId}/versions", version);

        // Act
        var runnerResult = await RunRunnerProcessAsync(processId, "1", _outputPath, _dbPath, authRef);

        // Assert: Exit code 30 (DISABLED) per cli-contract.md
        runnerResult.ExitCode.Should().Be(30, 
            $"Runner should exit with 30 (DISABLED) when version is disabled. Stdout: {runnerResult.StdOut}, Stderr: {runnerResult.StdErr}");
    }

    [Fact]
    public async Task Runner_WhenProcessNotFound_ExitsWith20_NotFound()
    {
        // Arrange: Don't create any config - just run with non-existent process
        var processId = "proc-nonexistent";
        var authRef = "api_key_prod";

        // Act
        var runnerResult = await RunRunnerProcessAsync(processId, "1", _outputPath, _dbPath, authRef);

        // Assert: Exit code 20 (NOT_FOUND) per cli-contract.md
        runnerResult.ExitCode.Should().Be(20, 
            $"Runner should exit with 20 (NOT_FOUND) when process doesn't exist. Stdout: {runnerResult.StdOut}, Stderr: {runnerResult.StdErr}");
    }

    private async Task<RunnerResult> RunRunnerProcessAsync(
        string processId, 
        string version, 
        string outputPath, 
        string dbPath,
        string authRef)
    {
        var runnerProjectPath = FindRunnerProjectPath();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{runnerProjectPath}\" -- run --processId {processId} --version {version} --dest local --outPath \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(runnerProjectPath)!
        };

        startInfo.Environment["METRICS_SQLITE_PATH"] = dbPath;
        startInfo.Environment[$"METRICS_SECRET__{authRef}"] = "TEST_TOKEN";

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOut = await process.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new RunnerResult(process.ExitCode, stdOut, stdErr);
    }

    private async Task<RunnerResult> RunRunnerProcessWithoutSecretAsync(
        string processId, 
        string version, 
        string outputPath, 
        string dbPath)
    {
        var runnerProjectPath = FindRunnerProjectPath();
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{runnerProjectPath}\" -- run --processId {processId} --version {version} --dest local --outPath \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(runnerProjectPath)!
        };

        // Only set SQLite path, NOT the secret
        startInfo.Environment["METRICS_SQLITE_PATH"] = dbPath;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOut = await process.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new RunnerResult(process.ExitCode, stdOut, stdErr);
    }

    private string FindRunnerProjectPath()
    {
        var testDir = AppContext.BaseDirectory;
        var rootDir = testDir;
        
        while (!Directory.Exists(Path.Combine(rootDir, "src", "Runner")))
        {
            var parent = Directory.GetParent(rootDir);
            if (parent == null)
            {
                throw new InvalidOperationException("Could not find Runner project.");
            }
            rootDir = parent.FullName;
        }

        return Path.Combine(rootDir, "src", "Runner", "Runner.csproj");
    }

    private record RunnerResult(int ExitCode, string StdOut, string StdErr);
}
