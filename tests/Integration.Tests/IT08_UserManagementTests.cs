using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT08 — User Management Tests
/// 
/// Validates:
/// - Create user (CRUD)
/// - Get user by ID
/// - Get user by username (NEW endpoint)
/// - Update user
/// - Change user password
/// - Case-insensitive username handling
/// - Authorization (Admin role required)
/// - Error responses (409 duplicate, 404 not found, 400 validation, 403 forbidden)
/// </summary>
[Collection("Sequential")]
public class IT08_UserManagementTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string _adminToken = string.Empty;

    public IT08_UserManagementTests()
    {
        _dbPath = TestFixtures.CreateTempDbPath();
        // Enable auth for this test suite
        _factory = new TestWebApplicationFactory(_dbPath, disableAuth: false);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
    }

    private async Task<string> GetAdminTokenAsync()
    {
        if (!string.IsNullOrEmpty(_adminToken))
            return _adminToken;

        var request = new { username = "admin", password = "testpass123" };
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        _adminToken = content.GetProperty("access_token").GetString()!;
        return _adminToken;
    }

    // ========================================================================
    // Create User
    // ========================================================================

    [Fact]
    public async Task CreateUser_WithValidData_Returns201_Created()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var request = new
        {
            username = "john_doe",
            password = "Password123!",
            displayName = "John Doe",
            email = "john@example.com",
            roles = new[] { "Metrics.Reader" }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        content.GetProperty("username").GetString().Should().Be("john_doe");
        content.GetProperty("displayName").GetString().Should().Be("John Doe");
        content.GetProperty("email").GetString().Should().Be("john@example.com");
        content.GetProperty("isActive").GetBoolean().Should().BeTrue();
        
        var roles = content.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().Contain("Metrics.Reader");
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_Returns409_Conflict()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var username = "duplicate_user";
        
        // Create first user
        var request1 = new { username = username, password = "Pass123456!" };
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        req1.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req1.Content = JsonContent.Create(request1);
        var resp1 = await _client.SendAsync(req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Try to create duplicate
        var request2 = new { username = username, password = "DifferentPass123!" };
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        req2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req2.Content = JsonContent.Create(request2);

        // Act
        var response = await _client.SendAsync(req2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("code").GetString().Should().Be("CONFLICT");
    }

    [Fact]
    public async Task CreateUser_WithDuplicateUsername_CaseInsensitive_Returns409()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        
        // Create user with lowercase
        var request1 = new { username = "testuser", password = "Pass123456!" };
        var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        req1.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req1.Content = JsonContent.Create(request1);
        var resp1 = await _client.SendAsync(req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Try to create with different case
        var request2 = new { username = "TESTUSER", password = "DifferentPass123!" };
        var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        req2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        req2.Content = JsonContent.Create(request2);

        // Act
        var response = await _client.SendAsync(req2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateUser_WithPasswordUnder8Chars_Returns400_BadRequest()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var request = new { username = "short_pass", password = "Short1!" };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_WithoutAdminRole_Returns403_Forbidden()
    {
        // Arrange: Create a Reader user
        var adminToken = await GetAdminTokenAsync();
        var createReaderReq = new { username = "reader_user", password = "Pass123456!" };
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        req.Content = JsonContent.Create(createReaderReq);
        var resp = await _client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Login as Reader
        var tokenReq = new { username = "reader_user", password = "Pass123456!" };
        var tokenResp = await _client.PostAsJsonAsync("/api/auth/token", tokenReq);
        var tokenContent = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
        var readerToken = tokenContent.GetProperty("access_token").GetString();

        // Try to create user as Reader
        var createUserReq = new { username = "another_user", password = "Pass123456!" };
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", readerToken);
        httpRequest.Content = JsonContent.Create(createUserReq);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ========================================================================
    // Get User by ID
    // ========================================================================

    [Fact]
    public async Task GetUserById_WithValidId_Returns200_And_UserData()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var userId = await CreateUserAsync(token, "test_user_get", "Pass123456!");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/auth/users/{userId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("id").GetString().Should().Be(userId);
        content.GetProperty("username").GetString().Should().Be("test_user_get");
        content.TryGetProperty("passwordHash", out _).Should().BeFalse();  // Must not return hash
    }

    [Fact]
    public async Task GetUserById_WithInvalidId_Returns404_NotFound()
    {
        // Arrange
        var token = await GetAdminTokenAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/auth/users/invalid-id-xyz");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ========================================================================
    // Get User by Username (NEW ENDPOINT)
    // ========================================================================

    [Fact]
    public async Task GetUserByUsername_WithValidUsername_Returns200_And_UserData()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        await CreateUserAsync(token, "alice", "Pass123456!");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/auth/users/by-username/alice");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("username").GetString().Should().Be("alice");
        content.TryGetProperty("passwordHash", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetUserByUsername_WithInvalidUsername_Returns404_NotFound()
    {
        // Arrange
        var token = await GetAdminTokenAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/auth/users/by-username/nonexistent");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUserByUsername_CaseInsensitive_Works()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        await CreateUserAsync(token, "bob", "Pass123456!");

        // Act: Query with different case
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/auth/users/by-username/BOB");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("username").GetString().Should().Be("bob");
    }

    // ========================================================================
    // Change Password
    // ========================================================================

    [Fact]
    public async Task ChangePassword_WithValidPassword_Returns200_And_InvalidatesOldPassword()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var userId = await CreateUserAsync(adminToken, "pwd_user", "OldPassword123!");

        // Act: Change password
        var changeReq = new { newPassword = "NewPassword456!" };
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/auth/users/{userId}/password");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        request.Content = JsonContent.Create(changeReq);
        var response = await _client.SendAsync(request);

        // Assert: Password change succeeds
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert: Old password doesn't work
        var oldLoginReq = new { username = "pwd_user", password = "OldPassword123!" };
        var oldLoginResp = await _client.PostAsJsonAsync("/api/auth/token", oldLoginReq);
        oldLoginResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Assert: New password works
        var newLoginReq = new { username = "pwd_user", password = "NewPassword456!" };
        var newLoginResp = await _client.PostAsJsonAsync("/api/auth/token", newLoginReq);
        newLoginResp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithPasswordUnder8Chars_Returns400()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var userId = await CreateUserAsync(token, "short_pwd", "Pass123456!");

        // Act
        var request = new { newPassword = "Short1!" };
        var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/auth/users/{userId}/password");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(request);
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ========================================================================
    // Update User
    // ========================================================================

    [Fact]
    public async Task UpdateUser_ChangesDisplayName_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var userId = await CreateUserAsync(token, "update_user", "Pass123456!", displayName: "Old Name");

        // Act
        var updateReq = new { displayName = "New Name" };
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/auth/users/{userId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(updateReq);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("displayName").GetString().Should().Be("New Name");
    }

    [Fact]
    public async Task UpdateUser_ChangesRoles_Returns200()
    {
        // Arrange
        var token = await GetAdminTokenAsync();
        var userId = await CreateUserAsync(token, "role_user", "Pass123456!", roles: new[] { "Metrics.Reader" });

        // Act
        var updateReq = new { roles = new[] { "Metrics.Admin", "Metrics.Reader" } };
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/auth/users/{userId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(updateReq);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var roles = content.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().Contain("Metrics.Admin");
        roles.Should().Contain("Metrics.Reader");
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private async Task<string> CreateUserAsync(
        string adminToken,
        string username,
        string password,
        string? displayName = null,
        string? email = null,
        string[]? roles = null)
    {
        var request = new
        {
            username = username,
            password = password,
            displayName = displayName ?? $"Test {username}",
            email = email ?? $"{username}@example.com",
            roles = roles ?? new[] { "Metrics.Reader" }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(httpRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        return content.GetProperty("id").GetString()!;
    }
}
