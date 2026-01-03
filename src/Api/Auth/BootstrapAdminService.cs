namespace Metrics.Api.Auth;

/// <summary>
/// Service for bootstrapping the admin user on first startup
/// </summary>
public interface IBootstrapAdminService
{
    Task EnsureAdminExistsAsync();
}

/// <summary>
/// Bootstrap admin service implementation
/// </summary>
public class BootstrapAdminService : IBootstrapAdminService
{
    private readonly IAuthUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<BootstrapAdminService> _logger;

    public BootstrapAdminService(
        IAuthUserRepository userRepository,
        IPasswordHasher passwordHasher,
        AuthOptions authOptions,
        ILogger<BootstrapAdminService> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _authOptions = authOptions;
        _logger = logger;
    }

    public async Task EnsureAdminExistsAsync()
    {
        // Only bootstrap if enabled and in LocalJwt mode
        if (_authOptions.Mode != "LocalJwt" || !_authOptions.LocalJwt.EnableBootstrapAdmin)
        {
            _logger.LogInformation("Bootstrap admin disabled or not in LocalJwt mode. Skipping.");
            return;
        }

        // Check if any admin exists
        var hasAdmin = await _userRepository.HasAnyAdminAsync();
        if (hasAdmin)
        {
            _logger.LogInformation("Admin user already exists. Bootstrap skipped.");
            return;
        }

        // Create bootstrap admin
        var username = _authOptions.LocalJwt.BootstrapAdminUsername;
        var password = _authOptions.LocalJwt.BootstrapAdminPassword;

        var now = DateTime.UtcNow;
        var user = new AuthUser
        {
            Id = Guid.NewGuid().ToString("N"),
            Username = username,
            DisplayName = "Bootstrap Admin",
            Email = null,
            PasswordHash = _passwordHasher.HashPassword(password),
            IsActive = true,
            FailedAttempts = 0,
            LockoutUntilUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastLoginUtc = null,
            Roles = new List<string> { AppRoles.Admin }
        };

        await _userRepository.CreateAsync(user);

        _logger.LogWarning(
            "SECURITY WARNING: Bootstrap admin user '{Username}' created with default password. " +
            "Change the password immediately and set Auth:LocalJwt:EnableBootstrapAdmin=false in production!",
            username);
    }
}
