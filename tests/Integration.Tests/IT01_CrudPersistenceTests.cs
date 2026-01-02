using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT01 — CRUD + persistence via API (smoke test)
/// 
/// Per integration-tests.md:
/// - Use WebApplicationFactory
/// - Use SQLite (file) via METRICS_SQLITE_PATH
/// - Execute CRUD operations via HTTP
/// - Validate status codes and persistence
/// </summary>
public class IT01_CrudPersistenceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IT01_CrudPersistenceTests()
    {
        _dbPath = TestFixtures.CreateTempDbPath();
        _factory = new TestWebApplicationFactory(_dbPath);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task CreateConnector_ReturnsCreated_AndPersists()
    {
        // Arrange
        var connector = new ConnectorDto(
            Id: "conn-test-001",
            Name: "Test Connector",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/connectors", connector);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ConnectorDto>();
        created.Should().NotBeNull();
        created!.Id.Should().Be(connector.Id);
        created.Name.Should().Be(connector.Name);
        created.BaseUrl.Should().Be(connector.BaseUrl);
        created.AuthRef.Should().Be(connector.AuthRef);
        created.TimeoutSeconds.Should().Be(connector.TimeoutSeconds);

        // Verify persistence via GET
        var getResponse = await _client.GetAsync("/api/connectors");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var connectors = await getResponse.Content.ReadFromJsonAsync<List<ConnectorDto>>();
        connectors.Should().ContainSingle(c => c.Id == connector.Id);
    }

    [Fact]
    public async Task CreateProcess_ReturnsCreated_AndPersists()
    {
        // Arrange: First create a connector
        var connector = new ConnectorDto(
            Id: "conn-proc-001",
            Name: "Test Connector for Process",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30
        );
        await _client.PostAsJsonAsync("/api/connectors", connector);

        var process = new ProcessDto(
            Id: "proc-test-001",
            Name: "Test Process",
            Status: "Active",
            ConnectorId: connector.Id,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(
                    Type: "LocalFileSystem",
                    Local: new LocalFileSystemDto(BasePath: "./output")
                )
            }
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/processes", process);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("id").GetString().Should().Be(process.Id);
        created.GetProperty("name").GetString().Should().Be(process.Name);
        created.GetProperty("status").GetString().Should().Be(process.Status);

        // Verify persistence via GET
        var getResponse = await _client.GetAsync($"/api/processes/{process.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("id").GetString().Should().Be(process.Id);
    }

    [Fact]
    public async Task CreateProcessVersion_ReturnsCreated_AndPersists()
    {
        // Arrange: First create connector and process
        var connector = new ConnectorDto(
            Id: "conn-ver-001",
            Name: "Test Connector for Version",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30
        );
        await _client.PostAsJsonAsync("/api/connectors", connector);

        var process = new ProcessDto(
            Id: "proc-ver-001",
            Name: "Test Process for Version",
            Status: "Active",
            ConnectorId: connector.Id,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(
                    Type: "LocalFileSystem",
                    Local: new LocalFileSystemDto(BasePath: "./output")
                )
            }
        );
        await _client.PostAsJsonAsync("/api/processes", process);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: process.Id,
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
            Dsl: new DslDto(
                Profile: "jsonata",
                Text: TestFixtures.GetHostsCpuDsl()
            ),
            OutputSchema: outputSchema
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/processes/{process.Id}/versions", version);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("processId").GetString().Should().Be(process.Id);
        created.GetProperty("version").GetString().Should().Be("1");
        created.GetProperty("enabled").GetBoolean().Should().BeTrue();

        // Verify persistence via GET
        var getResponse = await _client.GetAsync($"/api/processes/{process.Id}/versions/1");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("processId").GetString().Should().Be(process.Id);
    }

    [Fact]
    public async Task FullCrudWorkflow_Success()
    {
        // This test verifies the complete CRUD workflow needed for E2E tests

        // 1. Create Connector
        var connector = new ConnectorDto(
            Id: "conn-full-001",
            Name: "Full Workflow Connector",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_prod",
            TimeoutSeconds: 30
        );
        var connResponse = await _client.PostAsJsonAsync("/api/connectors", connector);
        connResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 2. Create Process
        var process = new ProcessDto(
            Id: "proc-full-001",
            Name: "Full Workflow Process",
            Status: "Active",
            ConnectorId: connector.Id,
            OutputDestinations: new List<OutputDestinationDto>
            {
                new OutputDestinationDto(Type: "LocalFileSystem", Local: new LocalFileSystemDto(BasePath: "./output"))
            }
        );
        var procResponse = await _client.PostAsJsonAsync("/api/processes", process);
        procResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 3. Create ProcessVersion
        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: process.Id,
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
        var verResponse = await _client.PostAsJsonAsync($"/api/processes/{process.Id}/versions", version);
        verResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // 4. Verify all data persisted
        var allConnectors = await _client.GetFromJsonAsync<List<JsonElement>>("/api/connectors");
        allConnectors.Should().Contain(c => c.GetProperty("id").GetString() == connector.Id);

        var allProcesses = await _client.GetFromJsonAsync<List<JsonElement>>("/api/processes");
        allProcesses.Should().Contain(p => p.GetProperty("id").GetString() == process.Id);

        var retrievedVersion = await _client.GetFromJsonAsync<JsonElement>($"/api/processes/{process.Id}/versions/1");
        retrievedVersion.GetProperty("enabled").GetBoolean().Should().BeTrue();
    }
}
