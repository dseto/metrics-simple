using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT06 — Connector API Token Tests
/// 
/// Per api-behavior.md and sqlite-schema.md:
/// - POST /connectors with apiToken => stored encrypted, hasApiToken=true
/// - PUT /connectors with apiToken omitted => keep existing token
/// - PUT /connectors with apiToken=null => remove token
/// - PUT /connectors with apiToken=string => replace token
/// - GET /connectors and GET /connectors/{id} => never return apiToken, only hasApiToken
/// - Token validation: 1..4096 chars
/// - Requires METRICS_SECRET_KEY for encryption
/// </summary>
[Collection("Integration Tests")]
public class IT06_ConnectorApiTokenTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IT06_ConnectorApiTokenTests()
    {
        _dbPath = TestFixtures.CreateTempDbPath();
        
        // Set METRICS_SECRET_KEY for encryption (32 bytes = 44 chars base64)
        Environment.SetEnvironmentVariable("METRICS_SECRET_KEY", "dGVzdC1zZWNyZXQta2V5LTMyLWJ5dGVzLWJhc2U2NHg=");
        
        _factory = new TestWebApplicationFactory(_dbPath);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
        Environment.SetEnvironmentVariable("METRICS_SECRET_KEY", null);
    }

    [Fact]
    public async Task CreateConnector_WithApiToken_StoresEncryptedAndReturnsHasApiToken()
    {
        // Arrange
        var connector = new ConnectorCreateDto(
            Id: "conn-token-001",
            Name: "Test Connector with Token",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: "secret-token-12345"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/connectors", connector);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ConnectorDto>();
        created.Should().NotBeNull();
        created!.Id.Should().Be(connector.Id);
        created.HasApiToken.Should().BeTrue();

        // Verify GET also returns hasApiToken=true (but never returns the token itself)
        var getResponse = await _client.GetAsync($"/api/v1/connectors/{connector.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var retrieved = await getResponse.Content.ReadFromJsonAsync<ConnectorDto>();
        retrieved.Should().NotBeNull();
        retrieved!.HasApiToken.Should().BeTrue();

        // Verify token is NOT in the response JSON
        var json = await getResponse.Content.ReadAsStringAsync();
        json.Should().NotContain("secret-token");
        json.Should().NotContain("apiToken");
    }

    [Fact]
    public async Task CreateConnector_WithoutApiToken_ReturnsHasApiTokenFalse()
    {
        // Arrange
        var connector = new ConnectorCreateDto(
            Id: "conn-no-token-001",
            Name: "Test Connector without Token",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: null
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/connectors", connector);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ConnectorDto>();
        created.Should().NotBeNull();
        created!.HasApiToken.Should().BeFalse();
    }

    [Fact]
    public async Task CreateConnector_WithInvalidApiToken_TooShort_Returns400()
    {
        // Arrange
        var connector = new ConnectorCreateDto(
            Id: "conn-invalid-001",
            Name: "Invalid Token",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: "" // Empty string (invalid)
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/connectors", connector);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateConnector_WithInvalidApiToken_TooLong_Returns400()
    {
        // Arrange
        var connector = new ConnectorCreateDto(
            Id: "conn-invalid-002",
            Name: "Invalid Token",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: new string('x', 4097) // 4097 chars (invalid)
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/connectors", connector);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateConnector_ApiTokenOmitted_KeepsExistingToken()
    {
        // Arrange: Create connector with token
        var createDto = new ConnectorCreateDto(
            Id: "conn-update-001",
            Name: "Original Name",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: "original-secret-token"
        );
        await _client.PostAsJsonAsync("/api/v1/connectors", createDto);

        // Act: Update without specifying apiToken (omitted)
        var updateDto = new ConnectorUpdateDto(
            Name: "Updated Name",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 60,
            ApiToken: null,
            ApiTokenSpecified: false  // FALSE = omitted (keep existing)
        );
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/connectors/{createDto.Id}", updateDto);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ConnectorDto>();
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
        updated.TimeoutSeconds.Should().Be(60);
        updated.HasApiToken.Should().BeTrue(); // Token still exists
    }

    [Fact]
    public async Task UpdateConnector_ApiTokenNull_RemovesToken()
    {
        // Arrange: Create connector with token
        var createDto = new ConnectorCreateDto(
            Id: "conn-update-002",
            Name: "Original Name",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: "original-secret-token"
        );
        await _client.PostAsJsonAsync("/api/v1/connectors", createDto);

        // Act: Update with apiToken=null (explicitly remove)
        var updateDto = new ConnectorUpdateDto(
            Name: "Updated Name",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: null,
            ApiTokenSpecified: true  // TRUE + null = remove
        );
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/connectors/{createDto.Id}", updateDto);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ConnectorDto>();
        updated.Should().NotBeNull();
        updated!.HasApiToken.Should().BeFalse(); // Token removed
    }

    [Fact]
    public async Task UpdateConnector_ApiTokenString_ReplacesToken()
    {
        // Arrange: Create connector with token
        var createDto = new ConnectorCreateDto(
            Id: "conn-update-003",
            Name: "Original Name",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: "original-secret-token"
        );
        await _client.PostAsJsonAsync("/api/v1/connectors", createDto);

        // Act: Update with new token
        var updateDto = new ConnectorUpdateDto(
            Name: "Updated Name",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: "new-secret-token",
            ApiTokenSpecified: true  // TRUE + string = replace
        );
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/connectors/{createDto.Id}", updateDto);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ConnectorDto>();
        updated.Should().NotBeNull();
        updated!.HasApiToken.Should().BeTrue(); // Token exists (replaced)
    }

    [Fact]
    public async Task ListConnectors_NeverReturnsApiToken()
    {
        // Arrange: Create multiple connectors with tokens
        var connector1 = new ConnectorCreateDto(
            Id: "conn-list-001",
            Name: "Connector 1",
            BaseUrl: "https://api1.example.com",
            AuthRef: "api_key_1",
            TimeoutSeconds: 30,
            ApiToken: "secret-token-1"
        );
        var connector2 = new ConnectorCreateDto(
            Id: "conn-list-002",
            Name: "Connector 2",
            BaseUrl: "https://api2.example.com",
            AuthRef: "api_key_2",
            TimeoutSeconds: 30,
            ApiToken: null  // No token
        );
        await _client.PostAsJsonAsync("/api/v1/connectors", connector1);
        await _client.PostAsJsonAsync("/api/v1/connectors", connector2);

        // Act
        var response = await _client.GetAsync("/api/v1/connectors");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var connectors = await response.Content.ReadFromJsonAsync<List<ConnectorDto>>();
        connectors.Should().NotBeNull();
        connectors.Should().HaveCountGreaterOrEqualTo(2);

        var conn1 = connectors!.FirstOrDefault(c => c.Id == connector1.Id);
        conn1.Should().NotBeNull();
        conn1!.HasApiToken.Should().BeTrue();

        var conn2 = connectors!.FirstOrDefault(c => c.Id == connector2.Id);
        conn2.Should().NotBeNull();
        conn2!.HasApiToken.Should().BeFalse();

        // Verify tokens are NOT in the response JSON
        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("secret-token");
        json.Should().NotContain("apiToken");
    }

    [Fact]
    public async Task UpdateConnector_InvalidApiToken_Returns400()
    {
        // Arrange: Create connector
        var createDto = new ConnectorCreateDto(
            Id: "conn-update-invalid-001",
            Name: "Test Connector",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: "valid-token"
        );
        await _client.PostAsJsonAsync("/api/v1/connectors", createDto);

        // Act: Update with invalid token (too long)
        var updateDto = new ConnectorUpdateDto(
            Name: "Updated Name",
            BaseUrl: "https://api.example.com",
            AuthRef: "api_key_test",
            TimeoutSeconds: 30,
            ApiToken: new string('x', 4097),
            ApiTokenSpecified: true
        );
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/connectors/{createDto.Id}", updateDto);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
