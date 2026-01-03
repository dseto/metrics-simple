using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Metrics.Api.Auth;

/// <summary>
/// Service for JWT token generation (LocalJwt mode)
/// </summary>
public interface ITokenService
{
    string GenerateToken(AuthUser user);
    int GetExpiresInSeconds();
}

/// <summary>
/// JWT token service implementation for LocalJwt mode
/// </summary>
public class TokenService : ITokenService
{
    private readonly AuthOptions _options;
    private readonly ILogger<TokenService> _logger;

    public TokenService(AuthOptions options, ILogger<TokenService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string GenerateToken(AuthUser user)
    {
        if (string.IsNullOrEmpty(_options.SigningKey))
        {
            throw new InvalidOperationException("SigningKey is required for LocalJwt mode");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var jti = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        // Add app_roles claim (as multiple claims for the same type)
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim("app_roles", role));
        }

        // Add display_name and email if present
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            claims.Add(new Claim("display_name", user.DisplayName));
        }
        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        return tokenHandler.WriteToken(token);
    }

    public int GetExpiresInSeconds()
    {
        return _options.AccessTokenMinutes * 60;
    }
}
