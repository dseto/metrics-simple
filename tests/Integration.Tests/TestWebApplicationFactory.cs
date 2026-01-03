using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures API to use a specific SQLite database via METRICS_SQLITE_PATH env var.
/// By default, disables auth for easier testing. Use WithAuth() to enable auth testing.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly bool _disableAuth;

    public TestWebApplicationFactory(string dbPath, bool disableAuth = true)
    {
        _dbPath = dbPath;
        _disableAuth = disableAuth;
        
        // Set environment variables BEFORE creating the client
        // because Program.cs reads configuration during startup
        Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", _dbPath);
        
        if (_disableAuth)
        {
            Environment.SetEnvironmentVariable("Auth__Mode", "Off");
        }
        else
        {
            Environment.SetEnvironmentVariable("Auth__Mode", "LocalJwt");
            Environment.SetEnvironmentVariable("Auth__SigningKey", "TEST-SIGNING-KEY-FOR-INTEGRATION-TESTS-32-CHARS!!");
            Environment.SetEnvironmentVariable("Auth__LocalJwt__EnableBootstrapAdmin", "true");
            Environment.SetEnvironmentVariable("Auth__LocalJwt__BootstrapAdminUsername", "admin");
            Environment.SetEnvironmentVariable("Auth__LocalJwt__BootstrapAdminPassword", "testpass123");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Load API key from appsettings.Development.json and set as env var
        // so Program.cs can use it when initializing the AI provider
        try
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Api");
            var devConfigPath = Path.Combine(basePath, "appsettings.Development.json");
            if (File.Exists(devConfigPath))
            {
                var jsonText = File.ReadAllText(devConfigPath);
                var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("AI", out var aiSection) &&
                    aiSection.TryGetProperty("ApiKey", out var apiKeyElement) &&
                    apiKeyElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var apiKey = apiKeyElement.GetString();
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        Environment.SetEnvironmentVariable("METRICS_OPENROUTER_API_KEY", apiKey);
                    }
                }
            }
        }
        catch
        {
            // Silently fail - tests will skip if no API key
        }

        builder.ConfigureAppConfiguration((context, config) =>
        {
            var configOverrides = new Dictionary<string, string?>
            {
                ["Database:Path"] = _dbPath
            };

            // Also add to configuration for any code that reads from IConfiguration
            if (_disableAuth)
            {
                configOverrides["Auth:Mode"] = "Off";
            }
            else
            {
                configOverrides["Auth:Mode"] = "LocalJwt";
                configOverrides["Auth:SigningKey"] = "TEST-SIGNING-KEY-FOR-INTEGRATION-TESTS-32-CHARS!!";
                configOverrides["Auth:LocalJwt:EnableBootstrapAdmin"] = "true";
                configOverrides["Auth:LocalJwt:BootstrapAdminUsername"] = "admin";
                configOverrides["Auth:LocalJwt:BootstrapAdminPassword"] = "testpass123";
            }

            config.AddInMemoryCollection(configOverrides);
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        // Clean up env vars
        Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", null);
        Environment.SetEnvironmentVariable("Auth__Mode", null);
        Environment.SetEnvironmentVariable("Auth__SigningKey", null);
        Environment.SetEnvironmentVariable("Auth__LocalJwt__EnableBootstrapAdmin", null);
        Environment.SetEnvironmentVariable("Auth__LocalJwt__BootstrapAdminUsername", null);
        Environment.SetEnvironmentVariable("Auth__LocalJwt__BootstrapAdminPassword", null);
    }
}
