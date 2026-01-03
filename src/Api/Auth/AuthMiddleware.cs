using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Serilog.Context;

namespace Metrics.Api.Auth;

/// <summary>
/// Middleware for correlation ID handling
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string HeaderName = "X-Correlation-Id";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Get or generate correlation ID
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N")[..12];

        // Store in HttpContext for later use
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Add to Serilog context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

/// <summary>
/// Middleware for audit logging (ApiRequestCompleted)
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;
    private readonly AuthOptions _authOptions;

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger, AuthOptions authOptions)
    {
        _next = next;
        _logger = logger;
        _authOptions = authOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            LogRequestCompleted(context, stopwatch.ElapsedMilliseconds);
        }
    }

    private void LogRequestCompleted(HttpContext context, long durationMs)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? "unknown";
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "/";
        var statusCode = context.Response.StatusCode;

        // Extract claims if authenticated
        string? actorSub = null;
        string? tokenId = null;
        var actorRoles = new List<string>();

        if (context.User.Identity?.IsAuthenticated == true)
        {
            actorSub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;
            tokenId = context.User.FindFirst("jti")?.Value;
            actorRoles = context.User.FindAll("app_roles").Select(c => c.Value).ToList();
        }

        _logger.LogInformation(
            "ApiRequestCompleted: {CorrelationId} {AuthMode} {ActorSub} {ActorRoles} {TokenId} {Method} {Path} {StatusCode} {DurationMs}ms",
            correlationId,
            _authOptions.Mode,
            actorSub ?? "anonymous",
            actorRoles.Count > 0 ? string.Join(",", actorRoles) : "none",
            tokenId ?? "none",
            method,
            path,
            statusCode,
            durationMs);
    }
}

/// <summary>
/// Middleware for claims normalization (app_roles)
/// </summary>
public class ClaimsNormalizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ClaimMappingOptions _claimMapping;

    public ClaimsNormalizationMiddleware(RequestDelegate next, ClaimMappingOptions claimMapping)
    {
        _next = next;
        _claimMapping = claimMapping;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var existingAppRoles = context.User.FindAll("app_roles").ToList();

            if (existingAppRoles.Count == 0)
            {
                // Try to map from other role sources
                var rolesToAdd = new List<string>();

                // Check role sources in order
                foreach (var source in _claimMapping.RoleSources)
                {
                    if (source == "app_roles") continue; // Already checked

                    var claims = context.User.FindAll(source).ToList();
                    foreach (var claim in claims)
                    {
                        rolesToAdd.Add(claim.Value);
                    }

                    if (rolesToAdd.Count > 0) break;
                }

                // Check groups and map to roles
                var groupClaims = context.User.FindAll("groups").ToList();
                foreach (var group in groupClaims)
                {
                    if (_claimMapping.GroupToRole.TryGetValue(group.Value, out var role))
                    {
                        if (!rolesToAdd.Contains(role))
                        {
                            rolesToAdd.Add(role);
                        }
                    }
                }

                // Add normalized roles to the identity
                if (rolesToAdd.Count > 0 && context.User.Identity is ClaimsIdentity identity)
                {
                    foreach (var role in rolesToAdd)
                    {
                        identity.AddClaim(new Claim("app_roles", role));
                    }
                }
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for auth middleware registration
/// </summary>
public static class AuthMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuditLoggingMiddleware>();
    }

    public static IApplicationBuilder UseClaimsNormalization(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ClaimsNormalizationMiddleware>();
    }

    /// <summary>
    /// Gets the correlation ID from the current HttpContext
    /// </summary>
    public static string GetCorrelationId(this HttpContext context)
    {
        return context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N")[..12];
    }
}
