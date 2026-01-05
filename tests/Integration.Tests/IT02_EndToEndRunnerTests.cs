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
/// IT02 — E2E: API → Runner → FetchSource (HTTP mock) → CSV (OBRIGATÓRIO)
/// 
/// Per integration-tests.md:
/// - Start mock HTTP server (WireMock.Net) on dynamic port
/// - Configure mock to respond to GET /v1/servers with fixture JSON
/// - Require Authorization: Bearer TEST_TOKEN header
/// - Start API with WebApplicationFactory (same METRICS_SQLITE_PATH)
/// - Create Connector + Process + Version via API
/// - Execute Runner CLI as REAL process
/// - Validate:
///   - exit code = 0
///   - WireMock received exactly 1 request (FetchSource happened)
///   - CSV was created
///   - CSV content matches expected fixture (byte-by-byte, normalized newlines)
/// </summary>
public class IT02_EndToEndRunnerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _outputPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly WireMockServer _mockServer;

    public IT02_EndToEndRunnerTests()
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
    public async Task RunnerE2E_FetchFromMock_GeneratesCorrectCsv()
    {
        // Arrange: Configure WireMock to serve the fixture JSON
        var inputJson = TestFixtures.GetHostsCpuInputJson();
        _mockServer
            .Given(Request.Create()
                .WithPath("/v1/servers")
                .WithParam("limit", "100")
                .WithParam("filter", "active")
                .WithHeader("Authorization", "Bearer TEST_TOKEN")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(inputJson));

        // Create configuration via API
        var connectorId = "conn-e2e-001";
        var processId = "proc-e2e-001";

        // 1. Create Connector with baseUrl pointing to WireMock + BEARER auth with apiToken
        var connector = new ConnectorCreateDto(
            Id: connectorId,
            Name: "E2E Test Connector",
            BaseUrl: _mockServer.Url!,
            TimeoutSeconds: 30,
            AuthType: "BEARER",
            ApiToken: "TEST_TOKEN"
        );
        var connResponse = await _client.PostAsJsonAsync("/api/v1/connectors", connector);
        connResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Create Process
        var process = new ProcessDto(
            Id: processId,
            Name: "E2E Test Process",
            Status: "Active",
            ConnectorId: connectorId,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(Type: "LocalFileSystem", Local: new LocalFileSystemDto(_outputPath))
            }
        );
        var procResponse = await _client.PostAsJsonAsync("/api/v1/processes", process);
        procResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Create ProcessVersion with DSL and schema from fixtures
        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: processId,
            Version: 1,
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
        var verResponse = await _client.PostAsJsonAsync($"/api/v1/processes/{processId}/versions", version);
        verResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Execute Runner CLI as a real process (no authRef needed - token is stored encrypted in DB)
        var runnerResult = await RunRunnerProcessAsync(processId, "1", _outputPath, _dbPath);

        // Assert
        runnerResult.ExitCode.Should().Be(0, $"Runner should exit with 0. Stdout: {runnerResult.StdOut}, Stderr: {runnerResult.StdErr}");

        // Verify WireMock received exactly 1 request (FetchSource occurred)
        var requests = _mockServer.LogEntries;
        requests.Should().HaveCount(1, "WireMock should receive exactly 1 request from FetchSource");
        requests.First().RequestMessage.Path.Should().Be("/v1/servers");

        // Find CSV file in output directory
        var csvFiles = Directory.GetFiles(_outputPath, "*.csv", SearchOption.AllDirectories);
        csvFiles.Should().HaveCount(1, "Exactly one CSV file should be generated");

        // Compare CSV content (normalize newlines)
        var actualCsv = TestFixtures.NormalizeCsvNewlines(await File.ReadAllTextAsync(csvFiles[0]));
        var expectedCsv = TestFixtures.NormalizeCsvNewlines(TestFixtures.GetHostsCpuExpectedCsv());
        
        actualCsv.Should().Be(expectedCsv, "CSV content should match expected fixture byte-by-byte");
    }

    [Fact]
    public async Task RunnerE2E_WithoutAuthHeader_MockReturnsUnauthorized()
    {
        // This test verifies that the runner sends the correct Authorization header
        
        // Arrange: Configure WireMock to require auth and reject without it
        var inputJson = TestFixtures.GetHostsCpuInputJson();
        
        // Match with correct auth header
        _mockServer
            .Given(Request.Create()
                .WithPath("/v1/servers")
                .WithHeader("Authorization", "Bearer TEST_TOKEN")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(inputJson));

        // Match without correct auth header -> 401
        _mockServer
            .Given(Request.Create()
                .WithPath("/v1/servers")
                .UsingGet())
            .AtPriority(10)  // Lower priority (higher number)
            .RespondWith(Response.Create()
                .WithStatusCode(401)
                .WithBody("Unauthorized"));

        // Create configuration via API
        var connectorId = "conn-auth-001";
        var processId = "proc-auth-001";

        // Create Connector with BEARER auth and apiToken
        var connector = new ConnectorCreateDto(
            Id: connectorId,
            Name: "Auth Test Connector",
            BaseUrl: _mockServer.Url!,
            TimeoutSeconds: 30,
            AuthType: "BEARER",
            ApiToken: "TEST_TOKEN"
        );
        await _client.PostAsJsonAsync("/api/v1/connectors", connector);

        var process = new ProcessDto(
            Id: processId,
            Name: "Auth Test Process",
            Status: "Active",
            ConnectorId: connectorId,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(Type: "LocalFileSystem", Local: new LocalFileSystemDto(_outputPath))
            }
        );
        await _client.PostAsJsonAsync("/api/v1/processes", process);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: processId,
            Version: 1,
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
        await _client.PostAsJsonAsync($"/api/v1/processes/{processId}/versions", version);

        // Act: Run with token stored in DB -> should succeed
        var runnerResult = await RunRunnerProcessAsync(processId, "1", _outputPath, _dbPath);

        // Assert: Should succeed because we're passing the correct secret
        runnerResult.ExitCode.Should().Be(0, "Runner should succeed with correct auth token");
        
        // Verify request had auth header
        var requests = _mockServer.LogEntries;
        requests.Should().Contain(r => 
            r.RequestMessage.Headers != null && 
            r.RequestMessage.Headers.ContainsKey("Authorization") &&
            r.RequestMessage.Headers["Authorization"].Contains("Bearer TEST_TOKEN"));
    }

    private async Task<RunnerResult> RunRunnerProcessAsync(
        string processId, 
        string version, 
        string outputPath, 
        string dbPath)
    {
        // Find the Runner project directory
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

        // Set environment variables for the runner process
        startInfo.Environment["METRICS_SQLITE_PATH"] = dbPath;
        // Note: Token is now stored encrypted in the database, no env var needed

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdOut = await process.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        return new RunnerResult(process.ExitCode, stdOut, stdErr);
    }

    private string FindRunnerProjectPath()
    {
        // Navigate up from test bin directory to find src/Runner
        var testDir = AppContext.BaseDirectory;
        var rootDir = testDir;
        
        // Go up until we find the solution root (containing src folder)
        while (!Directory.Exists(Path.Combine(rootDir, "src", "Runner")))
        {
            var parent = Directory.GetParent(rootDir);
            if (parent == null)
            {
                throw new InvalidOperationException("Could not find Runner project. Test must be run from within the solution structure.");
            }
            rootDir = parent.FullName;
        }

        return Path.Combine(rootDir, "src", "Runner", "Runner.csproj");
    }

    private record RunnerResult(int ExitCode, string StdOut, string StdErr);
}
