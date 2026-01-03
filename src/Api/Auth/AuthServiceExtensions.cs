using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Metrics.Api.Auth;

/// <summary>
/// Extension methods for configuring auth services
/// </summary>
public static class AuthServiceExtensions
{
    /// <summary>
    /// Adds authentication and authorization services based on AuthOptions
    /// </summary>
    public static IServiceCollection AddAuthServices(this IServiceCollection services, AuthOptions authOptions)
    {
        // Register auth options
        services.AddSingleton(authOptions);
        services.AddSingleton(authOptions.ClaimMapping);

        // Register password hasher
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // Register token service (always, but will only be used in LocalJwt mode)
        services.AddSingleton<ITokenService, TokenService>();

        // Register bootstrap admin service
        services.AddScoped<IBootstrapAdminService, BootstrapAdminService>();

        // Configure authentication
        if (authOptions.Mode == "Off")
        {
            // No authentication - configure authorization to allow anonymous
            services.AddAuthorization(options =>
            {
                // Create pass-through policies for when auth is disabled
                options.AddPolicy(AuthPolicies.Reader, policy => policy.RequireAssertion(_ => true));
                options.AddPolicy(AuthPolicies.Admin, policy => policy.RequireAssertion(_ => true));
            });
            return services;
        }

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        if (authOptions.Mode == "LocalJwt")
        {
            ConfigureLocalJwt(authBuilder, authOptions);
        }
        else if (authOptions.Mode == "ExternalOidc")
        {
            ConfigureExternalOidc(authBuilder, authOptions);
        }

        // Configure authorization policies
        services.AddAuthorization(options =>
        {
            // Reader policy: requires either Metrics.Reader or Metrics.Admin
            options.AddPolicy(AuthPolicies.Reader, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireAssertion(context =>
                    context.User.HasClaim("app_roles", AppRoles.Reader) ||
                    context.User.HasClaim("app_roles", AppRoles.Admin));
            });

            // Admin policy: requires Metrics.Admin only
            options.AddPolicy(AuthPolicies.Admin, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireClaim("app_roles", AppRoles.Admin);
            });
        });

        return services;
    }

    private static void ConfigureLocalJwt(Microsoft.AspNetCore.Authentication.AuthenticationBuilder builder, AuthOptions options)
    {
        if (string.IsNullOrEmpty(options.SigningKey))
        {
            throw new InvalidOperationException("Auth:SigningKey is required for LocalJwt mode");
        }

        var key = Encoding.UTF8.GetBytes(options.SigningKey);

        builder.AddJwtBearer(jwt =>
        {
            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = options.Issuer,
                ValidAudience = options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.FromMinutes(1),
                NameClaimType = "sub",
                RoleClaimType = "app_roles"
            };

            jwt.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    // Suppress the default response
                    context.HandleResponse();
                    
                    await AuthErrorHandler.WriteErrorAsync(
                        context.HttpContext,
                        401,
                        AuthErrorCodes.AuthUnauthorized,
                        "Authentication required");
                },
                OnForbidden = async context =>
                {
                    await AuthErrorHandler.WriteErrorAsync(
                        context.HttpContext,
                        403,
                        AuthErrorCodes.AuthForbidden,
                        "Access denied");
                }
            };
        });
    }

    private static void ConfigureExternalOidc(Microsoft.AspNetCore.Authentication.AuthenticationBuilder builder, AuthOptions options)
    {
        if (string.IsNullOrEmpty(options.ExternalOidc.Authority))
        {
            throw new InvalidOperationException("Auth:ExternalOidc:Authority is required for ExternalOidc mode");
        }

        builder.AddJwtBearer(jwt =>
        {
            jwt.Authority = options.ExternalOidc.Authority;
            jwt.Audience = options.Audience;
            jwt.RequireHttpsMetadata = options.ExternalOidc.RequireHttpsMetadata;

            jwt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                NameClaimType = "sub",
                RoleClaimType = "app_roles"
            };

            jwt.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    
                    await AuthErrorHandler.WriteErrorAsync(
                        context.HttpContext,
                        401,
                        AuthErrorCodes.AuthUnauthorized,
                        "Authentication required");
                },
                OnForbidden = async context =>
                {
                    await AuthErrorHandler.WriteErrorAsync(
                        context.HttpContext,
                        403,
                        AuthErrorCodes.AuthForbidden,
                        "Access denied");
                }
            };
        });
    }

    /// <summary>
    /// Adds rate limiting services for auth
    /// </summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, AuthOptions authOptions)
    {
        services.AddRateLimiter(options =>
        {
            // Login rate limit: 10 requests per minute per IP
            options.AddPolicy("login", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // General rate limit for authenticated users: 120 requests per minute per sub
            // Fallback for unauthenticated: 60 requests per minute per IP
            options.AddPolicy("general", context =>
            {
                var sub = context.User?.FindFirst("sub")?.Value;
                var key = sub ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var limit = sub != null ? 120 : 60;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: key,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Global fallback
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Handle rate limit exceeded
            options.OnRejected = async (context, _) =>
            {
                await AuthErrorHandler.WriteErrorAsync(
                    context.HttpContext,
                    429,
                    AuthErrorCodes.AuthRateLimited,
                    "Too many requests. Please try again later.");
            };
        });

        return services;
    }
}
