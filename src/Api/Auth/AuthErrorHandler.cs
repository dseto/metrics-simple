using System.Text.Json;

namespace Metrics.Api.Auth;

/// <summary>
/// Handlers for auth-related error responses
/// </summary>
public static class AuthErrorHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Creates a 401 Unauthorized ApiError result
    /// </summary>
    public static IResult Unauthorized(HttpContext context, string? message = null)
    {
        var error = new ApiError
        {
            Code = AuthErrorCodes.AuthUnauthorized,
            Message = message ?? "Authentication required",
            CorrelationId = context.GetCorrelationId()
        };
        return Results.Json(error, JsonOptions, statusCode: 401);
    }

    /// <summary>
    /// Creates a 403 Forbidden ApiError result
    /// </summary>
    public static IResult Forbidden(HttpContext context, string? message = null)
    {
        var error = new ApiError
        {
            Code = AuthErrorCodes.AuthForbidden,
            Message = message ?? "Access denied",
            CorrelationId = context.GetCorrelationId()
        };
        return Results.Json(error, JsonOptions, statusCode: 403);
    }

    /// <summary>
    /// Creates a 429 Rate Limited ApiError result
    /// </summary>
    public static IResult RateLimited(HttpContext context, string? message = null)
    {
        var error = new ApiError
        {
            Code = AuthErrorCodes.AuthRateLimited,
            Message = message ?? "Too many requests",
            CorrelationId = context.GetCorrelationId()
        };
        return Results.Json(error, JsonOptions, statusCode: 429);
    }

    /// <summary>
    /// Creates a 503 Auth Disabled ApiError result
    /// </summary>
    public static IResult AuthDisabled(HttpContext context, string? message = null)
    {
        var error = new ApiError
        {
            Code = AuthErrorCodes.AuthDisabled,
            Message = message ?? "Authentication endpoint not available in current mode",
            CorrelationId = context.GetCorrelationId()
        };
        return Results.Json(error, JsonOptions, statusCode: 503);
    }

    /// <summary>
    /// Creates a 400 Bad Request ApiError result
    /// </summary>
    public static IResult BadRequest(HttpContext context, string? message = null)
    {
        var error = new ApiError
        {
            Code = "BAD_REQUEST",
            Message = message ?? "Invalid request",
            CorrelationId = context.GetCorrelationId()
        };
        return Results.Json(error, JsonOptions, statusCode: 400);
    }

    /// <summary>
    /// Creates a 409 Conflict ApiError result
    /// </summary>
    public static IResult Conflict(HttpContext context, string? message = null)
    {
        var error = new ApiError
        {
            Code = "CONFLICT",
            Message = message ?? "Resource already exists",
            CorrelationId = context.GetCorrelationId()
        };
        return Results.Json(error, JsonOptions, statusCode: 409);
    }

    /// <summary>
    /// Writes a standard ApiError to the response (for middleware use)
    /// </summary>
    public static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString("N")[..12];
        
        var error = new ApiError
        {
            Code = code,
            Message = message,
            CorrelationId = correlationId
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
    }
}
