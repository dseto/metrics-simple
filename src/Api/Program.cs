using System.Text.Json;
using Metrics.Api;
using Metrics.Api.AI;
using Metrics.Api.AI.Engines;
using Metrics.Api.AI.Engines.PlanV1;
using Metrics.Api.Auth;
using Metrics.Engine;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Metrics Simple Config API",
        Version = "v1",
        Description = "API de configuração para Metrics Simple (Spec-Driven)"
    });

    // Add JWT bearer auth to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Load Auth configuration
var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

// Configure CORS with AllowedOrigins from Auth config
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", corsBuilder =>
    {
        // Use Auth.AllowedOrigins (priority) or CORS_ORIGINS env var
        var corsOrigins = authOptions.AllowedOrigins.Length > 0 
            ? authOptions.AllowedOrigins 
            : Environment.GetEnvironmentVariable("CORS_ORIGINS")?.Split(';') ?? new[] { "http://localhost:4200" };
        
        corsBuilder
            .WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Get configuration - METRICS_SQLITE_PATH env var takes precedence (required for integration tests)
var dbPath = Environment.GetEnvironmentVariable("METRICS_SQLITE_PATH") 
    ?? builder.Configuration["Database:Path"] 
    ?? "./config/config.db";
var secretsPath = builder.Configuration["Secrets:Path"] ?? "./config/secrets.local.json";

// Initialize database
var dbProvider = new DatabaseProvider();
dbProvider.InitializeDatabase(dbPath);

// Register repositories
builder.Services.AddScoped<IProcessRepository>(_ => new ProcessRepository(dbPath));
builder.Services.AddScoped<IProcessVersionRepository>(_ => new ProcessVersionRepository(dbPath));
builder.Services.AddScoped<IConnectorRepository>(_ => new ConnectorRepository(dbPath));
builder.Services.AddScoped<IConnectorTokenRepository>(_ => new ConnectorTokenRepository(dbPath));
builder.Services.AddScoped<IConnectorSecretsRepository>(_ => new ConnectorSecretsRepository(dbPath));
builder.Services.AddScoped<IAuthUserRepository>(_ => new AuthUserRepository(dbPath));

// Register Token Encryption Service (throws if METRICS_SECRET_KEY not set, but that's by design)
builder.Services.AddScoped<ITokenEncryptionService>(sp => new TokenEncryptionService());

// Register Auth services (including authentication, authorization, and rate limiting)
builder.Services.AddAuthServices(authOptions);
builder.Services.AddAuthRateLimiting(authOptions);

// Register Engine services
builder.Services.AddScoped<IDslTransformer, JsonataTransformer>();
builder.Services.AddScoped<ISchemaValidator, SchemaValidator>();
builder.Services.AddScoped<ICsvGenerator, CsvGenerator>();
builder.Services.AddScoped<EngineService>();

// Register Secrets
builder.Services.AddScoped<ISecretsProvider>(_ => new SecretsProvider());

// Load AI configuration from appsettings
var aiConfig = builder.Configuration.GetSection("AI").Get<AiConfiguration>() ?? new AiConfiguration
{
    Enabled = false,
    Provider = "HttpOpenAICompatible",
    EndpointUrl = "https://openrouter.ai/api/v1/chat/completions",
    Model = "nousresearch/hermes-3-llama-3.1-405b",
    PromptVersion = "1.0.0",
    TimeoutSeconds = 30,
    MaxRetries = 1,
    Temperature = 0.0,
    MaxTokens = 4096,
    TopP = 0.9
};

// API Key from environment variable takes precedence over appsettings
var apiKeyFromEnv = Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY")
    ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
if (!string.IsNullOrEmpty(apiKeyFromEnv))
{
    aiConfig.ApiKey = apiKeyFromEnv;
}

builder.Services.AddSingleton(aiConfig);

// Register AI Provider based on configuration
builder.Services.AddHttpClient<HttpOpenAiCompatibleProvider>();
builder.Services.AddSingleton<IAiProvider>(sp =>
{
    if (!aiConfig.Enabled)
    {
        // Return a disabled provider that will throw on invocation
        return new MockAiProvider(MockProviderConfig.WithError(
            AiErrorCodes.AiDisabled,
            "AI is disabled in configuration"));
    }

    if (aiConfig.Provider == "MockProvider")
    {
        return new MockAiProvider();
    }

    // Default to HttpOpenAICompatible
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(HttpOpenAiCompatibleProvider));
    var logger = sp.GetRequiredService<ILogger<HttpOpenAiCompatibleProvider>>();
    return new HttpOpenAiCompatibleProvider(httpClient, aiConfig, logger);
});

// Register AI Engines and Router
builder.Services.AddScoped<LegacyAiDslEngine>(sp => new LegacyAiDslEngine(
    sp.GetRequiredService<IAiProvider>(),
    sp.GetRequiredService<AiConfiguration>(),
    sp.GetRequiredService<EngineService>(),
    Log.ForContext<LegacyAiDslEngine>()));

// PlanV1AiEngine - optionally with LLM provider if API key is available
builder.Services.AddScoped<PlanV1AiEngine>(sp =>
{
    var apiKey = Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY")
                 ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    
    PlanV1LlmProvider? llmProvider = null;
    if (!string.IsNullOrEmpty(apiKey))
    {
        llmProvider = new PlanV1LlmProvider(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("AI"),
            sp.GetRequiredService<AiConfiguration>(),
            sp.GetRequiredService<ILogger<PlanV1LlmProvider>>());
    }
    else
    {
        Log.Warning("No API key configured for PlanV1 LLM - falling back to template-only mode");
    }
    
    return new PlanV1AiEngine(
        Log.ForContext<PlanV1AiEngine>(),
        sp.GetRequiredService<EngineService>(),
        llmProvider);
});

builder.Services.AddScoped<AiEngineRouter>(sp => new AiEngineRouter(
    sp.GetRequiredService<LegacyAiDslEngine>(),
    sp.GetRequiredService<PlanV1AiEngine>(),
    sp.GetRequiredService<AiConfiguration>(),
    Log.ForContext<AiEngineRouter>()));

var app = builder.Build();

// Bootstrap admin user if needed (LocalJwt mode)
using (var scope = app.Services.CreateScope())
{
    var bootstrapService = scope.ServiceProvider.GetRequiredService<IBootstrapAdminService>();
    await bootstrapService.EnsureAdminExistsAsync();
}

// 1) Correlation ID middleware (must be first to capture all requests)
app.UseCorrelationId();

// Enable Swagger in all environments for spec-driven development
// In non-dev, Swagger is still available but requires Admin auth (see route protection below)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Metrics Simple Config API v1");
    c.RoutePrefix = "swagger";
});

// Use HTTPS redirection only in non-test environments
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

// 2) CORS
app.UseCors("AllowFrontend");

// 3) Rate limiting
app.UseRateLimiter();

// 4) Authentication
if (authOptions.Mode != "Off")
{
    app.UseAuthentication();
}

// 5) Authorization (always needed because endpoints have RequireAuthorization)
app.UseAuthorization();

// 6) Claims normalization (after auth, before audit)
app.UseClaimsNormalization();

// 7) Audit logging middleware (logs all requests including auth failures)
app.UseAuditLogging();

// ============================================================================
// Auth Endpoints (LocalJwt mode only)
// ============================================================================
var authGroup = app.MapGroup("/api/auth")
    .WithTags("Auth");

// POST /api/auth/token - Login endpoint (only LocalJwt)
authGroup.MapPost("/token", async (
    TokenRequest request,
    IAuthUserRepository userRepo,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    AuthOptions options,
    HttpContext httpContext,
    ILogger<Program> logger) =>
{
    var correlationId = httpContext.GetCorrelationId();

    // Check if LocalJwt mode
    if (options.Mode != "LocalJwt")
    {
        return AuthErrorHandler.AuthDisabled(httpContext, "Token endpoint not available in current auth mode");
    }

    // Validate request
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        logger.LogWarning("Login attempt with empty credentials. CorrelationId={CorrelationId}", correlationId);
        return AuthErrorHandler.Unauthorized(httpContext, "Invalid credentials");
    }

    // Get user (case-insensitive)
    var user = await userRepo.GetByUsernameAsync(request.Username);
    if (user == null)
    {
        logger.LogWarning("Login attempt for non-existent user. Username={Username}, CorrelationId={CorrelationId}",
            request.Username.ToLowerInvariant(), correlationId);
        return AuthErrorHandler.Unauthorized(httpContext, "Invalid credentials");
    }

    // Check if user is active
    if (!user.IsActive)
    {
        logger.LogWarning("Login attempt for inactive user. UserId={UserId}, CorrelationId={CorrelationId}",
            user.Id, correlationId);
        return AuthErrorHandler.Unauthorized(httpContext, "Account is disabled");
    }

    // Check lockout
    if (user.LockoutUntilUtc.HasValue && user.LockoutUntilUtc.Value > DateTime.UtcNow)
    {
        logger.LogWarning("Login attempt for locked user. UserId={UserId}, LockoutUntil={LockoutUntil}, CorrelationId={CorrelationId}",
            user.Id, user.LockoutUntilUtc.Value, correlationId);
        return AuthErrorHandler.RateLimited(httpContext, "Account is temporarily locked. Try again later.");
    }

    // Verify password
    if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        // Increment failed attempts
        var newFailedAttempts = user.FailedAttempts + 1;
        DateTime? lockoutUntil = null;

        if (newFailedAttempts >= options.LocalJwt.MaxFailedAttempts)
        {
            lockoutUntil = DateTime.UtcNow.AddMinutes(options.LocalJwt.LockoutMinutes);
            logger.LogWarning("User locked out due to failed attempts. UserId={UserId}, FailedAttempts={FailedAttempts}, LockoutUntil={LockoutUntil}, CorrelationId={CorrelationId}",
                user.Id, newFailedAttempts, lockoutUntil, correlationId);
        }
        else
        {
            logger.LogWarning("Failed login attempt. UserId={UserId}, FailedAttempts={FailedAttempts}, CorrelationId={CorrelationId}",
                user.Id, newFailedAttempts, correlationId);
        }

        await userRepo.UpdateLoginAttemptAsync(user.Id, newFailedAttempts, lockoutUntil, null);
        return AuthErrorHandler.Unauthorized(httpContext, "Invalid credentials");
    }

    // Success: reset failed attempts and update last login
    await userRepo.UpdateLoginAttemptAsync(user.Id, 0, null, DateTime.UtcNow);

    // Generate token
    var token = tokenService.GenerateToken(user);
    var expiresIn = tokenService.GetExpiresInSeconds();

    logger.LogInformation("Login successful. UserId={UserId}, Username={Username}, CorrelationId={CorrelationId}",
        user.Id, user.Username, correlationId);

    return Results.Ok(new TokenResponse
    {
        AccessToken = token,
        TokenType = "Bearer",
        ExpiresIn = expiresIn
    });
})
.WithName("Token")
.RequireRateLimiting("login")
.AllowAnonymous();

// GET /api/auth/me - Get current user info (requires auth)
authGroup.MapGet("/me", (HttpContext httpContext) =>
{
    var user = httpContext.User;
    if (user.Identity?.IsAuthenticated != true)
    {
        return AuthErrorHandler.Unauthorized(httpContext);
    }

    var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown";
    var roles = user.FindAll("app_roles").Select(c => c.Value).ToList();
    var displayName = user.FindFirst("display_name")?.Value;
    var email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

    return Results.Ok(new UserInfoResponse
    {
        Sub = sub,
        Roles = roles,
        DisplayName = displayName,
        Email = email
    });
})
.WithName("GetCurrentUser")
.RequireAuthorization(AuthPolicies.Reader);

// ============================================================================
// Admin Auth Endpoints (optional but recommended)
// ============================================================================
var adminAuthGroup = app.MapGroup("/api/admin/auth/users")
    .WithTags("Admin - Auth")
    .RequireAuthorization(AuthPolicies.Admin);

// POST /api/admin/auth/users - Create new user
adminAuthGroup.MapPost("/", CreateUserHandler)
    .WithName("CreateUser")
    .Produces(201)
    .Produces(400)
    .Produces(409);

// GET /api/admin/auth/users/{userId} - Get user details by ID
adminAuthGroup.MapGet("/{userId}", GetUserHandler)
    .WithName("GetUser")
    .Produces(200)
    .Produces(404);

// GET /api/admin/auth/users/by-username/{username} - Get user details by username
adminAuthGroup.MapGet("/by-username/{username}", GetUserByUsernameHandler)
    .WithName("GetUserByUsername")
    .Produces(200)
    .Produces(404);

// PUT /api/admin/auth/users/{userId}/password - Change user password
adminAuthGroup.MapPut("/{userId}/password", ChangeUserPasswordHandler)
    .WithName("ChangeUserPassword")
    .Produces(200)
    .Produces(400)
    .Produces(404);

// PUT /api/admin/auth/users/{userId} - Update user profile and roles
adminAuthGroup.MapPut("/{userId}", UpdateUserHandler)
    .WithName("UpdateUser")
    .Produces(200)
    .Produces(404);

// ============================================================================
// Health check (no auth required, no versioning)
// ============================================================================
app.MapGet("/api/health", GetHealth)
    .WithName("Health")
    .AllowAnonymous();

// ============================================================================
// API v1 endpoints (all routes require auth unless specified)
// ============================================================================
var v1 = app.MapGroup("/api/v1");

// Process endpoints
var processGroup = v1.MapGroup("/processes")
    .WithTags("Processes");

// GET - Reader policy (read-only access)
processGroup.MapGet("/", GetAllProcesses)
    .WithName("ListProcesses")
    .RequireAuthorization(AuthPolicies.Reader);

// POST - Admin policy (create)
processGroup.MapPost("/", CreateProcess)
    .WithName("CreateProcess")
    .RequireAuthorization(AuthPolicies.Admin);

// GET - Reader policy
processGroup.MapGet("/{id}", GetProcessById)
    .WithName("GetProcess")
    .RequireAuthorization(AuthPolicies.Reader);

// PUT - Admin policy (update)
processGroup.MapPut("/{id}", UpdateProcess)
    .WithName("UpdateProcess")
    .RequireAuthorization(AuthPolicies.Admin);

// DELETE - Admin policy
processGroup.MapDelete("/{id}", DeleteProcess)
    .WithName("DeleteProcess")
    .RequireAuthorization(AuthPolicies.Admin);

// ProcessVersion endpoints
var versionGroup = v1.MapGroup("/processes/{processId}/versions")
    .WithTags("ProcessVersions");

// GET - List all versions for a process
versionGroup.MapGet("/", GetAllProcessVersions)
    .WithName("ListProcessVersions")
    .RequireAuthorization(AuthPolicies.Reader);

// POST - Admin policy
versionGroup.MapPost("/", CreateProcessVersion)
    .WithName("CreateProcessVersion")
    .RequireAuthorization(AuthPolicies.Admin);

// GET - Reader policy
versionGroup.MapGet("/{version}", GetProcessVersion)
    .WithName("GetProcessVersion")
    .RequireAuthorization(AuthPolicies.Reader);

// PUT - Admin policy
versionGroup.MapPut("/{version}", UpdateProcessVersion)
    .WithName("UpdateProcessVersion")
    .RequireAuthorization(AuthPolicies.Admin);

// DELETE - Admin policy
versionGroup.MapDelete("/{version}", DeleteProcessVersion)
    .WithName("DeleteProcessVersion")
    .RequireAuthorization(AuthPolicies.Admin);

// Connector endpoints
var connectorGroup = v1.MapGroup("/connectors")
    .WithTags("Connectors");

// GET - Reader policy
connectorGroup.MapGet("/", GetAllConnectors)
    .WithName("ListConnectors")
    .RequireAuthorization(AuthPolicies.Reader);

// GET /{id} - Reader policy
connectorGroup.MapGet("/{id}", GetConnectorById)
    .WithName("GetConnector")
    .RequireAuthorization(AuthPolicies.Reader);

// POST - Admin policy
connectorGroup.MapPost("/", CreateConnector)
    .WithName("CreateConnector")
    .RequireAuthorization(AuthPolicies.Admin);

// PUT /{id} - Admin policy
connectorGroup.MapPut("/{id}", UpdateConnector)
    .WithName("UpdateConnector")
    .RequireAuthorization(AuthPolicies.Admin);

// DELETE /{id} - Admin policy
connectorGroup.MapDelete("/{id}", DeleteConnector)
    .WithName("DeleteConnector")
    .RequireAuthorization(AuthPolicies.Admin);

// Preview Transform endpoint - Reader policy (design-time operation)
v1.MapPost("/preview/transform", PreviewTransform)
    .WithName("PreviewTransform")
    .WithTags("Preview")
    .RequireAuthorization(AuthPolicies.Reader);

// AI DSL Generate endpoint - Reader policy (design-time operation)
v1.MapPost("/ai/dsl/generate", GenerateDsl)
    .WithName("GenerateDsl")
    .WithTags("AI")
    .RequireAuthorization(AuthPolicies.Reader);

app.Run();

// Handlers
IResult GetHealth() => Results.Ok(new { status = "ok" });

async Task<IResult> GetAllProcesses(IProcessRepository repo)
{
    var processes = await repo.GetAllProcessesAsync();
    return Results.Ok(processes);
}

async Task<IResult> CreateProcess(ProcessDto process, IProcessRepository repo)
{
    var created = await repo.CreateProcessAsync(process);
    return Results.Created($"/api/v1/processes/{created.Id}", created);
}

async Task<IResult> GetProcessById(string id, IProcessRepository repo)
{
    var process = await repo.GetProcessByIdAsync(id);
    if (process == null) return Results.NotFound();
    return Results.Ok(process);
}

async Task<IResult> UpdateProcess(string id, ProcessDto process, IProcessRepository repo)
{
    var updated = await repo.UpdateProcessAsync(id, process);
    return Results.Ok(updated);
}

async Task<IResult> DeleteProcess(string id, IProcessRepository repo)
{
    await repo.DeleteProcessAsync(id);
    return Results.NoContent();
}

async Task<IResult> CreateProcessVersion(string processId, ProcessVersionDto version, IProcessVersionRepository repo)
{
    try
    {
        var created = await repo.CreateVersionAsync(version with { ProcessId = processId });
        return Results.Created($"/api/v1/processes/{processId}/versions/{created.Version}", created);
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
    {
        return Results.Conflict(new { error = ex.Message });
    }
}

async Task<IResult> GetProcessVersion(string processId, string version, IProcessVersionRepository repo)
{
    if (!int.TryParse(version, out var versionInt))
        return Results.BadRequest(new { error = "Version must be an integer" });
    
    var pv = await repo.GetVersionAsync(processId, versionInt);
    if (pv == null) return Results.NotFound();
    return Results.Ok(pv);
}

async Task<IResult> UpdateProcessVersion(string processId, string version, ProcessVersionDto updated, IProcessVersionRepository repo)
{
    if (!int.TryParse(version, out var versionInt))
        return Results.BadRequest(new { error = "Version must be an integer" });
    
    var result = await repo.UpdateVersionAsync(processId, versionInt, updated);
    if (result == null)
        return Results.NotFound();
    
    return Results.Ok(result);
}

async Task<IResult> GetAllProcessVersions(string processId, IProcessVersionRepository versionRepo, IProcessRepository processRepo)
{
    // Check if process exists first
    var process = await processRepo.GetProcessByIdAsync(processId);
    if (process == null)
    {
        return Results.NotFound(new { code = "PROCESS_NOT_FOUND", message = $"Process '{processId}' not found" });
    }
    
    var versions = await versionRepo.GetAllVersionsAsync(processId);
    return Results.Ok(versions);
}

async Task<IResult> DeleteProcessVersion(string processId, string version, IProcessVersionRepository repo)
{
    if (!int.TryParse(version, out var versionInt))
        return Results.BadRequest(new { error = "Version must be an integer" });
    
    var deleted = await repo.DeleteVersionAsync(processId, versionInt);
    if (!deleted)
        return Results.NotFound();
    
    return Results.NoContent();
}

async Task<IResult> GetAllConnectors(IConnectorRepository repo)
{
    var connectors = await repo.GetAllConnectorsAsync();
    return Results.Ok(connectors);
}

async Task<IResult> GetConnectorById(string id, IConnectorRepository repo)
{
    var connector = await repo.GetConnectorByIdAsync(id);
    if (connector == null)
        return Results.NotFound();
    return Results.Ok(connector);
}

async Task<IResult> CreateConnector(
    ConnectorCreateDto connector,
    IConnectorRepository repo,
    ITokenEncryptionService encryptionService,
    IConnectorSecretsRepository secretsRepo,
    HttpContext httpContext)
{
    try
    {
        var created = await repo.CreateConnectorAsync(connector, encryptionService, secretsRepo);
        return Results.Created($"/api/v1/connectors/{created.Id}", created);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("METRICS_SECRET_KEY"))
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
}

async Task<IResult> UpdateConnector(
    string id,
    ConnectorUpdateDto connector,
    IConnectorRepository repo,
    ITokenEncryptionService encryptionService,
    IConnectorSecretsRepository secretsRepo,
    HttpContext httpContext)
{
    try
    {
        var updated = await repo.UpdateConnectorAsync(id, connector, encryptionService, secretsRepo);
        return Results.Ok(updated);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("METRICS_SECRET_KEY"))
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
}

async Task<IResult> DeleteConnector(string id, IConnectorRepository repo)
{
    // Check if connector is in use
    var inUse = await repo.IsConnectorInUseAsync(id);
    if (inUse)
    {
        return Results.Conflict(new { code = "CONNECTOR_IN_USE", message = "Connector is in use by one or more processes." });
    }
    
    // Check if connector exists
    var connector = await repo.GetConnectorByIdAsync(id);
    if (connector == null)
    {
        return Results.NotFound();
    }
    
    await repo.DeleteConnectorAsync(id);
    return Results.NoContent();
}

async Task<IResult> PreviewTransform(PreviewTransformRequestDto request, EngineService engine)
{
    try
    {
        // Use sample input as the transformation input
        var inputJson = JsonSerializer.SerializeToElement(request.SampleInput);
        var outputSchemaJson = JsonSerializer.SerializeToElement(request.OutputSchema);

        var result = engine.TransformValidateToCsv(inputJson, request.Dsl.Profile, request.Dsl.Text, outputSchemaJson);

        var response = new PreviewTransformResponseDto(
            result.IsValid,
            result.Errors.ToList(),
            result.OutputJson,
            result.CsvPreview
        );

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new PreviewTransformResponseDto(false, new List<string> { ex.Message }));
    }
}

async Task<IResult> GenerateDsl(
    DslGenerateRequest request,
    AiEngineRouter engineRouter,
    HttpContext httpContext,
    ILogger<Program> logger)
{
    // Generate correlation ID
    var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N")[..12];

    // Validate engine field if provided
    if (!string.IsNullOrEmpty(request.Engine) && !EngineType.IsValid(request.Engine))
    {
        return Results.Json(new AiError
        {
            Code = "INVALID_ENGINE",
            Message = $"Invalid engine value: '{request.Engine}'. Valid values are: legacy, plan_v1, auto",
            CorrelationId = correlationId
        }, statusCode: 400);
    }

    // Log engine selection
    var resolvedEngine = engineRouter.GetResolvedEngineType(request);
    logger.LogInformation(
        "AI DSL Generate: CorrelationId={CorrelationId}, RequestedEngine={RequestedEngine}, EngineSelected={EngineSelected}",
        correlationId,
        request.Engine ?? "(default)",
        resolvedEngine);

    // Route to appropriate engine and execute
    var result = await engineRouter.RouteAndExecuteAsync(request, correlationId, httpContext.RequestAborted);

    // Return appropriate response based on result
    if (result.Success)
    {
        return Results.Ok(result.Result);
    }
    else
    {
        return Results.Json(result.Error, statusCode: result.StatusCode);
    }
}

// ============================================================================
// Admin User Management Handlers
// ============================================================================

static async Task<IResult> CreateUserHandler(
    CreateUserRequest request,
    IAuthUserRepository userRepo,
    IPasswordHasher passwordHasher,
    ILogger<Program> logger,
    HttpContext httpContext)
{
    var correlationId = httpContext.GetCorrelationId();

    // Validate request
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return AuthErrorHandler.BadRequest(httpContext, "Username and password are required");
    }

    if (request.Password.Length < 8)
    {
        return AuthErrorHandler.BadRequest(httpContext, "Password must be at least 8 characters");
    }

    // Check if user already exists
    var existing = await userRepo.GetByUsernameAsync(request.Username);
    if (existing != null)
    {
        return AuthErrorHandler.Conflict(httpContext, "User already exists");
    }

    // Create user
    var now = DateTime.UtcNow;
    var user = new AuthUser
    {
        Id = Guid.NewGuid().ToString("N"),
        Username = request.Username,
        DisplayName = request.DisplayName,
        Email = request.Email,
        PasswordHash = passwordHasher.HashPassword(request.Password),
        IsActive = true,
        FailedAttempts = 0,
        LockoutUntilUtc = null,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
        LastLoginUtc = null,
        Roles = request.Roles ?? new List<string> { AppRoles.Reader }
    };

    await userRepo.CreateAsync(user);

    logger.LogInformation(
        "User created. UserId={UserId}, Username={Username}, Roles={Roles}, CorrelationId={CorrelationId}",
        user.Id, user.Username, string.Join(",", user.Roles), correlationId);

    return Results.Created($"/api/admin/auth/users/{user.Id}", new
    {
        id = user.Id,
        username = user.Username,
        displayName = user.DisplayName,
        email = user.Email,
        isActive = user.IsActive,
        roles = user.Roles,
        createdAt = user.CreatedAtUtc
    });
}

static async Task<IResult> GetUserHandler(
    string userId,
    IAuthUserRepository userRepo)
{
    var user = await userRepo.GetByIdAsync(userId);
    if (user == null)
        return Results.NotFound();

    return Results.Ok(new
    {
        id = user.Id,
        username = user.Username,
        displayName = user.DisplayName,
        email = user.Email,
        isActive = user.IsActive,
        roles = user.Roles,
        createdAt = user.CreatedAtUtc,
        lastLogin = user.LastLoginUtc
    });
}

static async Task<IResult> GetUserByUsernameHandler(
    string username,
    IAuthUserRepository userRepo)
{
    var user = await userRepo.GetByUsernameAsync(username);
    if (user == null)
        return Results.NotFound();

    return Results.Ok(new
    {
        id = user.Id,
        username = user.Username,
        displayName = user.DisplayName,
        email = user.Email,
        isActive = user.IsActive,
        roles = user.Roles,
        createdAt = user.CreatedAtUtc,
        lastLogin = user.LastLoginUtc
    });
}

static async Task<IResult> ChangeUserPasswordHandler(
    string userId,
    ChangePasswordRequest request,
    IAuthUserRepository userRepo,
    IPasswordHasher passwordHasher,
    ILogger<Program> logger,
    HttpContext httpContext)
{
    var correlationId = httpContext.GetCorrelationId();

    if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
    {
        return AuthErrorHandler.BadRequest(httpContext, "Password must be at least 8 characters");
    }

    var user = await userRepo.GetByIdAsync(userId);
    if (user == null)
        return Results.NotFound();

    user = user with
    {
        PasswordHash = passwordHasher.HashPassword(request.NewPassword),
        UpdatedAtUtc = DateTime.UtcNow,
        FailedAttempts = 0,
        LockoutUntilUtc = null
    };

    await userRepo.UpdateAsync(user);

    logger.LogInformation(
        "User password changed. UserId={UserId}, CorrelationId={CorrelationId}",
        userId, correlationId);

    return Results.Ok(new { message = "Password updated successfully" });
}

static async Task<IResult> UpdateUserHandler(
    string userId,
    UpdateUserRequest request,
    IAuthUserRepository userRepo,
    ILogger<Program> logger,
    HttpContext httpContext)
{
    var correlationId = httpContext.GetCorrelationId();

    var user = await userRepo.GetByIdAsync(userId);
    if (user == null)
        return Results.NotFound();

    user = user with
    {
        DisplayName = request.DisplayName ?? user.DisplayName,
        Email = request.Email ?? user.Email,
        IsActive = request.IsActive ?? user.IsActive,
        UpdatedAtUtc = DateTime.UtcNow,
        Roles = request.Roles ?? user.Roles
    };

    await userRepo.UpdateAsync(user);

    logger.LogInformation(
        "User updated. UserId={UserId}, CorrelationId={CorrelationId}",
        userId, correlationId);

    return Results.Ok(new
    {
        id = user.Id,
        username = user.Username,
        displayName = user.DisplayName,
        email = user.Email,
        isActive = user.IsActive,
        roles = user.Roles,
        updatedAt = user.UpdatedAtUtc
    });
}
