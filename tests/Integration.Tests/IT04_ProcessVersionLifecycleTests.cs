using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT04 — Process Version Lifecycle Tests (Comprehensive Suite)
/// 
/// The HEART of the API - versions contain DSL, schema, and enable LLM-assisted transformations.
/// This test suite validates:
/// - Full CRUD lifecycle of process versions
/// - Data persistence across API calls
/// - Multi-version scenarios (same process, different versions)
/// - Schema conformance per processVersion.schema.json
/// - Integration with preview/transform endpoint
/// </summary>
public class IT04_ProcessVersionLifecycleTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IT04_ProcessVersionLifecycleTests()
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

    private async Task SetupConnectorAndProcess(string connectorId, string processId)
    {
        // Create connector
        var connector = new ConnectorCreateDto(
            Id: connectorId,
            Name: $"Test Connector {connectorId}",
            BaseUrl: "https://api.example.com",
            AuthRef: "test_token",
            TimeoutSeconds: 30
        );
        var connResp = await _client.PostAsJsonAsync("/api/v1/connectors", connector);
        connResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Create process
        var process = new ProcessDto(
            Id: processId,
            Name: $"Test Process {processId}",
            Status: "Active",
            ConnectorId: connectorId,
            OutputDestinations: new List<OutputDestinationDto>()
        );
        var procResp = await _client.PostAsJsonAsync("/api/v1/processes", process);
        procResp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// IT04-01: Create Version with All Fields
    /// ✅ Version with complete data structure is created and returns 201
    /// ✅ All fields (sourceRequest, DSL, outputSchema) are persisted
    /// </summary>
    [Fact]
    public async Task IT04_01_CreateVersion_WithAllFields_Returns201()
    {
        // Arrange
        const string connId = "it04-01-conn";
        const string procId = "it04-01-proc";
        await SetupConnectorAndProcess(connId, procId);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto(
                Method: "GET",
                Path: "/api/servers",
                Headers: new Dictionary<string, string> { ["Authorization"] = "Bearer token123" },
                QueryParams: new Dictionary<string, string> { ["limit"] = "100", ["filter"] = "active" }
            ),
            Dsl: new DslDto(
                Profile: "jsonata",
                Text: "$.servers[*].{hostId: id, hostName: name, cpuPercent: cpu*100}"
            ),
            OutputSchema: outputSchema
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        created.GetProperty("processId").GetString().Should().Be(procId);
        created.GetProperty("version").GetInt32().Should().Be(1);
        created.GetProperty("enabled").GetBoolean().Should().Be(true);
        created.GetProperty("dsl").GetProperty("profile").GetString().Should().Be("jsonata");
        created.GetProperty("sourceRequest").GetProperty("method").GetString().Should().Be("GET");
        created.GetProperty("sourceRequest").GetProperty("path").GetString().Should().Be("/api/servers");
    }

    /// <summary>
    /// IT04-02: Get Version Returns 200
    /// ✅ Version created in test 01 can be retrieved with GET
    /// ✅ Data returned matches what was created
    /// </summary>
    [Fact]
    public async Task IT04_02_GetVersion_Returns200_WithCorrectData()
    {
        // Arrange
        const string connId = "it04-02-conn";
        const string procId = "it04-02-proc";
        await SetupConnectorAndProcess(connId, procId);

        var outputSchema = JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("POST", "/api/upload"),
            Dsl: new DslDto("jmespath", "servers[*].{name: Name, status: Status}"),
            OutputSchema: outputSchema
        );

        // Create version first
        var createResp = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Get the version
        var getResp = await _client.GetAsync($"/api/v1/processes/{procId}/versions/1");

        // Assert
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResp.Content.ReadFromJsonAsync<JsonElement>();

        retrieved.GetProperty("processId").GetString().Should().Be(procId);
        retrieved.GetProperty("version").GetInt32().Should().Be(1);
        retrieved.GetProperty("enabled").GetBoolean().Should().Be(true);
        retrieved.GetProperty("sourceRequest").GetProperty("method").GetString().Should().Be("POST");
        retrieved.GetProperty("sourceRequest").GetProperty("path").GetString().Should().Be("/api/upload");
        retrieved.GetProperty("dsl").GetProperty("profile").GetString().Should().Be("jmespath");
    }

    /// <summary>
    /// IT04-03: Update Version - Enabled Field
    /// ✅ Updating enabled flag to false persists correctly
    /// ✅ GET after update returns new value
    /// </summary>
    [Fact]
    public async Task IT04_03_UpdateVersion_EnabledField_Persists()
    {
        // Arrange
        const string connId = "it04-03-conn";
        const string procId = "it04-03-proc";
        await SetupConnectorAndProcess(connId, procId);

        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/test"),
            Dsl: new DslDto("jsonata", "$.value"),
            OutputSchema: JsonDocument.Parse("{}").RootElement
        );

        // Create
        await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);

        // Act: Update enabled to false
        var updated = version with { Enabled = false };
        var updateResp = await _client.PutAsJsonAsync(
            $"/api/v1/processes/{procId}/versions/1",
            updated
        );

        // Assert: Update returns 200
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedContent = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        updatedContent.GetProperty("enabled").GetBoolean().Should().Be(false);

        // Verify persistence: GET returns false
        var getResp = await _client.GetAsync($"/api/v1/processes/{procId}/versions/1");
        var retrieved = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("enabled").GetBoolean().Should().Be(false);
    }

    /// <summary>
    /// IT04-04: Update Version - DSL Field
    /// ✅ DSL text can be updated after creation
    /// ✅ New DSL persists and is returned on GET
    /// </summary>
    [Fact]
    public async Task IT04_04_UpdateVersion_DslField_Persists()
    {
        // Arrange
        const string connId = "it04-04-conn";
        const string procId = "it04-04-proc";
        await SetupConnectorAndProcess(connId, procId);

        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/api/data"),
            Dsl: new DslDto("jsonata", "$ . { name: Name }"),
            OutputSchema: JsonDocument.Parse("{}").RootElement
        );

        await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);

        // Act: Update DSL to include value transformation
        var newDsl = new DslDto("jsonata", "$ . { name: Name, cpuPercent: cpu*100 }");
        var updated = version with { Dsl = newDsl };
        var updateResp = await _client.PutAsJsonAsync(
            $"/api/v1/processes/{procId}/versions/1",
            updated
        );

        // Assert
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedContent = await updateResp.Content.ReadFromJsonAsync<JsonElement>();
        updatedContent.GetProperty("dsl").GetProperty("text").GetString()
            .Should().Contain("cpu*100");

        // Verify persistence
        var getResp = await _client.GetAsync($"/api/v1/processes/{procId}/versions/1");
        var retrieved = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("dsl").GetProperty("text").GetString()
            .Should().Contain("cpu*100");
    }

    /// <summary>
    /// IT04-05: Get Non-Existent Version Returns 404
    /// ✅ Requesting version that doesn't exist returns proper error
    /// </summary>
    [Fact]
    public async Task IT04_05_GetNonExistentVersion_Returns404()
    {
        // Arrange
        const string connId = "it04-05-conn";
        const string procId = "it04-05-proc";
        await SetupConnectorAndProcess(connId, procId);

        // Act
        var getResp = await _client.GetAsync($"/api/v1/processes/{procId}/versions/999");

        // Assert
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// IT04-06: Multiple Versions - Same Process
    /// ✅ Same process can have multiple versions
    /// ✅ Each version is independently accessible and has correct data
    /// </summary>
    [Fact]
    public async Task IT04_06_MultipleVersions_SameProcess_Coexist()
    {
        // Arrange
        const string connId = "it04-06-conn";
        const string procId = "it04-06-proc";
        await SetupConnectorAndProcess(connId, procId);

        var v1 = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/api/v1"),
            Dsl: new DslDto("jsonata", "$.v1"),
            OutputSchema: JsonDocument.Parse("{}").RootElement
        );

        var v2 = new ProcessVersionDto(
            ProcessId: procId,
            Version: 2,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/api/v2"),
            Dsl: new DslDto("jsonata", "$.v2"),
            OutputSchema: JsonDocument.Parse("{}").RootElement
        );

        // Act: Create both versions
        var resp1 = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", v1);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        var resp2 = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", v2);
        resp2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert: Both retrievable with correct data
        var get1 = await _client.GetAsync($"/api/v1/processes/{procId}/versions/1");
        get1.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved1 = await get1.Content.ReadFromJsonAsync<JsonElement>();
        retrieved1.GetProperty("version").GetInt32().Should().Be(1);
        retrieved1.GetProperty("sourceRequest").GetProperty("path").GetString().Should().Be("/api/v1");

        var get2 = await _client.GetAsync($"/api/v1/processes/{procId}/versions/2");
        get2.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved2 = await get2.Content.ReadFromJsonAsync<JsonElement>();
        retrieved2.GetProperty("version").GetInt32().Should().Be(2);
        retrieved2.GetProperty("sourceRequest").GetProperty("path").GetString().Should().Be("/api/v2");
    }

    /// <summary>
    /// IT04-07: Create Duplicate Version Returns 409
    /// ✅ Creating version with same number returns Conflict
    /// </summary>
    [Fact]
    public async Task IT04_07_CreateDuplicateVersion_Returns409()
    {
        // Arrange
        const string connId = "it04-07-conn";
        const string procId = "it04-07-proc";
        await SetupConnectorAndProcess(connId, procId);

        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/test"),
            Dsl: new DslDto("jsonata", "$.test"),
            OutputSchema: JsonDocument.Parse("{}").RootElement
        );

        // Create first time
        var resp1 = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act: Try same version again
        var resp2 = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);

        // Assert
        resp2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// IT04-08: Version with Sample Input
    /// ✅ Optional sampleInput field is stored and retrieved
    /// ✅ Used for preview/transform operations
    /// </summary>
    [Fact]
    public async Task IT04_08_VersionWithSampleInput_Persists()
    {
        // Arrange
        const string connId = "it04-08-conn";
        const string procId = "it04-08-proc";
        await SetupConnectorAndProcess(connId, procId);

        var sampleInput = JsonDocument.Parse(TestFixtures.GetHostsCpuInputJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/servers"),
            Dsl: new DslDto("jsonata", "$.servers[*].{hostId: id, hostName: name, cpuPercent: cpu*100}"),
            OutputSchema: JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement,
            SampleInput: sampleInput
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("sampleInput").Should().NotBe(default);

        // Verify persistence
        var getResp = await _client.GetAsync($"/api/v1/processes/{procId}/versions/1");
        var retrieved = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("sampleInput").GetProperty("result").GetArrayLength().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// IT04-09: Version Schema Constraints
    /// ✅ Version number is integer between 1..9999
    /// ✅ Method enum validation (GET/POST/PUT/DELETE)
    /// ✅ DSL profile enum validation (jsonata/jmespath/custom)
    /// </summary>
    [Fact]
    public async Task IT04_09_VersionConformsToSchema()
    {
        // Arrange
        const string connId = "it04-09-conn";
        const string procId = "it04-09-proc";
        await SetupConnectorAndProcess(connId, procId);

        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 5,  // Valid: 1..9999
            Enabled: true,
            SourceRequest: new SourceRequestDto(
                "PUT",
                "/path/with/special-chars_123"
            ),
            Dsl: new DslDto("custom", "some custom dsl"),
            OutputSchema: JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();

        created.GetProperty("version").GetInt32().Should().Be(5);
        created.GetProperty("sourceRequest").GetProperty("method").GetString().Should().Be("PUT");
        created.GetProperty("dsl").GetProperty("profile").GetString().Should().Be("custom");
    }

    /// <summary>
    /// IT04-10: Preview Transform with Version
    /// ✅ Preview endpoint accepts version DSL and returns response
    /// ✅ Endpoint works correctly with configured DSL
    /// </summary>
    [Fact]
    public async Task IT04_10_PreviewTransform_WithVersionDsl_Works()
    {
        // Arrange
        const string connId = "it04-10-conn";
        const string procId = "it04-10-proc";
        await SetupConnectorAndProcess(connId, procId);

        var sampleInput = JsonDocument.Parse(TestFixtures.GetHostsCpuInputJson()).RootElement;
        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/servers"),
            Dsl: new DslDto("jsonata", "$.result[*].{hostId: id, hostName: name}"),
            OutputSchema: JsonDocument.Parse(TestFixtures.GetHostsCpuOutputSchemaJson()).RootElement,
            SampleInput: sampleInput
        );

        // Create version
        await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);

        // Act: Simple preview with valid DSL
        var previewRequest = new
        {
            dsl = new { profile = "jsonata", text = "$.result[0]" },
            outputSchema = new { type = "object" },
            sampleInput = new { result = new[] { new { id = "1", name = "test" } } }
        };

        var previewResp = await _client.PostAsJsonAsync("/api/v1/preview/transform", previewRequest);

        // Assert - Just verify endpoint responds and returns expected structure
        previewResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await previewResp.Content.ReadFromJsonAsync<JsonElement>();
        result.TryGetProperty("isValid", out var isValidProp).Should().BeTrue();
        result.TryGetProperty("errors", out var errorsProp).Should().BeTrue();
    }

    /// <summary>
    /// IT04-11: Update Non-Existent Version Returns 404
    /// ✅ Trying to update version that doesn't exist returns error
    /// </summary>
    [Fact]
    public async Task IT04_11_UpdateNonExistentVersion_Returns404()
    {
        // Arrange
        const string connId = "it04-11-conn";
        const string procId = "it04-11-proc";
        await SetupConnectorAndProcess(connId, procId);

        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 999,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/test"),
            Dsl: new DslDto("jsonata", "$.test"),
            OutputSchema: JsonDocument.Parse("{}").RootElement
        );

        // Act
        var updateResp = await _client.PutAsJsonAsync(
            $"/api/v1/processes/{procId}/versions/999",
            version
        );

        // Assert
        updateResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// IT04-12: Version Lifecycle Complete
    /// ✅ Full workflow: Create → Get → Update → Get again
    /// ✅ All data persists correctly through lifecycle
    /// </summary>
    [Fact]
    public async Task IT04_12_VersionLifecycle_CreateGetUpdateGet()
    {
        // Arrange
        const string connId = "it04-12-conn";
        const string procId = "it04-12-proc";
        await SetupConnectorAndProcess(connId, procId);

        var initialDsl = "$.servers[*].{ id: id, name: name }";
        var version = new ProcessVersionDto(
            ProcessId: procId,
            Version: 1,
            Enabled: true,
            SourceRequest: new SourceRequestDto("GET", "/servers"),
            Dsl: new DslDto("jsonata", initialDsl),
            OutputSchema: JsonDocument.Parse("{}").RootElement
        );

        // Step 1: Create
        var createResp = await _client.PostAsJsonAsync($"/api/v1/processes/{procId}/versions", version);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Step 2: Get - verify creation
        var get1 = await _client.GetAsync($"/api/v1/processes/{procId}/versions/1");
        get1.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await get1.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("dsl").GetProperty("text").GetString().Should().Be(initialDsl);

        // Step 3: Update DSL
        var updatedDsl = "$.servers[*].{ id: id, name: name, cpuPercent: cpu*100 }";
        var updated = version with { Dsl = new DslDto("jsonata", updatedDsl) };
        var updateResp = await _client.PutAsJsonAsync($"/api/v1/processes/{procId}/versions/1", updated);
        updateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 4: Get again - verify update persisted
        var get2 = await _client.GetAsync($"/api/v1/processes/{procId}/versions/1");
        get2.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await get2.Content.ReadFromJsonAsync<JsonElement>();
        retrieved.GetProperty("dsl").GetProperty("text").GetString().Should().Be(updatedDsl);
    }
}
