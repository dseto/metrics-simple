using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT07 — Authentication Tests (LocalJwt)
/// 
/// Validates:
/// - Token endpoint with valid/invalid credentials
/// - Password validation rules
/// - JWT claims structure
/// - Error responses
/// - User account states (active/inactive, lockout)
/// </summary>
[Collection("Sequential")]
public class IT07_AuthenticationTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IT07_AuthenticationTests()
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

    // ========================================================================
    // Token Endpoint — Valid Credentials
    // ========================================================================

    [Fact]
    public async Task Token_WithValidAdminCredentials_Returns200_And_ValidJwt()
    {
        // Arrange
        var request = new { username = "admin", password = "testpass123" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        
        var accessToken = content.GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrEmpty();
        accessToken.Should().Match("eyJ*");  // JWT format
        
        content.GetProperty("token_type").GetString().Should().Be("Bearer");
        content.GetProperty("expires_in").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Token_JwtContainsExpectedClaims()
    {
        // Arrange
        var request = new { username = "admin", password = "testpass123" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = content.GetProperty("access_token").GetString();

        // Decode JWT (base64)
        var parts = token!.Split('.');
        var payload = parts[1];
        // Add padding if needed
        payload += new string('=', (4 - payload.Length % 4) % 4);
        var decoded = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(payload));
        var claims = JsonDocument.Parse(decoded).RootElement;

        // Assert
        claims.GetProperty("sub").GetString().Should().Be("admin");
        claims.TryGetProperty("app_roles", out var roles).Should().BeTrue();
        roles.GetString().Should().Be("Metrics.Admin");
        claims.TryGetProperty("jti", out var jti).Should().BeTrue();
        jti.GetString().Should().NotBeNullOrEmpty();
        claims.TryGetProperty("exp", out _).Should().BeTrue();
        claims.TryGetProperty("iat", out _).Should().BeTrue();
    }

    // ========================================================================
    // Token Endpoint — Invalid Credentials
    // ========================================================================

    [Fact]
    public async Task Token_WithWrongPassword_Returns401_Unauthorized()
    {
        // Arrange
        var request = new { username = "admin", password = "wrongpassword" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("code").GetString().Should().Be("AUTH_UNAUTHORIZED");
        content.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Token_WithNonExistentUser_Returns401_Unauthorized()
    {
        // Arrange
        var request = new { username = "nonexistent", password = "somepassword" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_WithEmptyUsername_Returns401()
    {
        // Arrange
        var request = new { username = "", password = "testpass123" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_WithEmptyPassword_Returns401()
    {
        // Arrange
        var request = new { username = "admin", password = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_PasswordIsCaseSensitive()
    {
        // Arrange
        var request = new { username = "admin", password = "TESTPASS123" };  // Wrong case

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_UsernameIsCaseInsensitive()
    {
        // Arrange
        var request = new { username = "ADMIN", password = "testpass123" };  // Different case

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ========================================================================
    // Token Endpoint — User Account States
    // ========================================================================

    [Fact]
    public async Task Token_WithInactiveUser_Returns401()
    {
        // Arrange: Create inactive user
        var adminToken = await GetAdminTokenAsync();
        var userId = await CreateUserAsync(adminToken, "inactive_user", "Pass123456!", isActive: false);

        // Act: Try to login
        var request = new { username = "inactive_user", password = "Pass123456!" };
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================================================
    // Me Endpoint
    // ========================================================================

    [Fact]
    public async Task Me_WithValidToken_ReturnsUserInfo()
    {
        // Arrange
        var token = await GetAdminTokenAsync();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        content.GetProperty("sub").GetString().Should().Be("admin");
        var roles = content.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.Should().Contain("Metrics.Admin");
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private async Task<string> GetAdminTokenAsync()
    {
        var request = new { username = "admin", password = "testpass123" };
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = content.GetProperty("access_token").GetString();
        return token!;
    }

    private async Task<string> CreateUserAsync(
        string adminToken, 
        string username, 
        string password,
        bool isActive = true,
        List<string>? roles = null)
    {
        var request = new
        {
            username = username,
            password = password,
            displayName = $"Test {username}",
            email = $"{username}@example.com",
            roles = roles ?? new List<string> { "Metrics.Reader" }
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/auth/users");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        httpRequest.Content = JsonContent.Create(request);
        
        var response = await _client.SendAsync(httpRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var userId = content.GetProperty("id").GetString();
        
        // Update isActive if needed
        if (!isActive)
        {
            await UpdateUserAsync(adminToken, userId!, new { isActive = false });
        }

        return userId!;
    }

    private async Task UpdateUserAsync(string adminToken, string userId, object updates)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/admin/auth/users/{userId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        request.Content = JsonContent.Create(updates);
        
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
