using System.Text.Json.Serialization;

namespace Metrics.Api.Auth;

/// <summary>
/// Auth configuration options - loaded from appsettings.json
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Authentication mode: LocalJwt, ExternalOidc, or Off
    /// </summary>
    public string Mode { get; set; } = "LocalJwt";

    /// <summary>
    /// JWT Issuer (for LocalJwt and ExternalOidc validation)
    /// </summary>
    public string Issuer { get; set; } = "MetricsSimple";

    /// <summary>
    /// JWT Audience
    /// </summary>
    public string Audience { get; set; } = "MetricsSimple.Api";

    /// <summary>
    /// Signing key for LocalJwt mode (HS256). Required for LocalJwt.
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>
    /// Access token expiration in minutes (default: 60)
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>
    /// Allowed CORS origins (semicolon-separated)
    /// </summary>
    public string[] AllowedOrigins { get; set; } = new[] { "http://localhost:4200", "https://localhost:4200" };

    /// <summary>
    /// LocalJwt-specific settings
    /// </summary>
    public LocalJwtOptions LocalJwt { get; set; } = new();

    /// <summary>
    /// ExternalOidc-specific settings (Okta/Entra-ready)
    /// </summary>
    public ExternalOidcOptions ExternalOidc { get; set; } = new();

    /// <summary>
    /// Claim mapping configuration for normalization
    /// </summary>
    public ClaimMappingOptions ClaimMapping { get; set; } = new();
}

/// <summary>
/// LocalJwt-specific configuration
/// </summary>
public class LocalJwtOptions
{
    /// <summary>
    /// Maximum failed login attempts before lockout
    /// </summary>
    public int MaxFailedAttempts { get; set; } = 5;

    /// <summary>
    /// Lockout duration in minutes
    /// </summary>
    public int LockoutMinutes { get; set; } = 5;

    /// <summary>
    /// Enable bootstrap admin creation on startup
    /// </summary>
    public bool EnableBootstrapAdmin { get; set; } = true;

    /// <summary>
    /// Bootstrap admin username
    /// </summary>
    public string BootstrapAdminUsername { get; set; } = "admin";

    /// <summary>
    /// Bootstrap admin password (change immediately after first login!)
    /// </summary>
    public string BootstrapAdminPassword { get; set; } = "ChangeMe123!";
}

/// <summary>
/// ExternalOidc-specific configuration (Okta/Entra-ready)
/// </summary>
public class ExternalOidcOptions
{
    /// <summary>
    /// OIDC Authority URL (e.g., https://login.microsoftonline.com/{tenant}/v2.0)
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Client ID for OIDC validation
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Whether to require HTTPS metadata
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}

/// <summary>
/// Claim mapping configuration for role normalization
/// </summary>
public class ClaimMappingOptions
{
    /// <summary>
    /// Claim types to check for roles (in order of precedence)
    /// </summary>
    public string[] RoleSources { get; set; } = new[] { "app_roles", "roles", "role" };

    /// <summary>
    /// Group to role mapping for ExternalOidc (group claim value -> app role)
    /// </summary>
    public Dictionary<string, string> GroupToRole { get; set; } = new();
}

/// <summary>
/// Auth user entity (persisted in SQLite)
/// </summary>
public record AuthUser
{
    public required string Id { get; init; }
    public required string Username { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
    public required string PasswordHash { get; init; }
    public bool IsActive { get; init; } = true;
    public int FailedAttempts { get; init; }
    public DateTime? LockoutUntilUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? LastLoginUtc { get; init; }
    public List<string> Roles { get; init; } = new();
}

/// <summary>
/// Login request DTO
/// </summary>
public record TokenRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }
}

/// <summary>
/// Token response DTO
/// </summary>
public record TokenResponse
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }
}

/// <summary>
/// User info response DTO (GET /api/auth/me)
/// </summary>
public record UserInfoResponse
{
    [JsonPropertyName("sub")]
    public required string Sub { get; init; }

    [JsonPropertyName("roles")]
    public required List<string> Roles { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }
}

/// <summary>
/// API error response for auth errors
/// </summary>
public record ApiError
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("details")]
    public object? Details { get; init; }
}

/// <summary>
/// Known auth error codes
/// </summary>
public static class AuthErrorCodes
{
    public const string AuthUnauthorized = "AUTH_UNAUTHORIZED";
    public const string AuthForbidden = "AUTH_FORBIDDEN";
    public const string AuthRateLimited = "AUTH_RATE_LIMITED";
    public const string AuthDisabled = "AUTH_DISABLED";
}

/// <summary>
/// Known app roles
/// </summary>
public static class AppRoles
{
    public const string Admin = "Metrics.Admin";
    public const string Reader = "Metrics.Reader";
}

/// <summary>
/// Authorization policy names
/// </summary>
public static class AuthPolicies
{
    public const string Reader = "Reader";
    public const string Admin = "Admin";
}
/// <summary>
/// Create user request DTO (POST /api/admin/auth/users)
/// </summary>
public record CreateUserRequest
{
    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password")]
    public required string Password { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { get; init; }
}

/// <summary>
/// Change user password request DTO
/// </summary>
public record ChangePasswordRequest
{
    [JsonPropertyName("newPassword")]
    public required string NewPassword { get; init; }
}

/// <summary>
/// Update user request DTO (PUT /api/admin/auth/users/{userId})
/// </summary>
public record UpdateUserRequest
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; init; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { get; init; }
}