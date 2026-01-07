using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Metrics.Api;

namespace Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures API to use a specific SQLite database via METRICS_SQLITE_PATH env var.
/// By default, disables auth for easier testing. Use WithAuth() to enable auth testing.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Metrics.Api.Program>
{
    private readonly string _dbPath;
    private readonly bool _disableAuth;

    public TestWebApplicationFactory(string dbPath, bool disableAuth = true)
    {
        _dbPath = dbPath;
        _disableAuth = disableAuth;
        
        // CRITICAL: Clear any system environment variables FIRST
        // This ensures .env file is always the source of truth and prevents conflicts
        // with stale/old API keys that might be set in the PowerShell session
        Environment.SetEnvironmentVariable("METRICS_OPENROUTER_API_KEY", null);
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        
        // Load .env file FIRST to get METRICS_OPENROUTER_API_KEY and other vars
        LoadEnvFile();
        
        // Set environment variables BEFORE creating the client
        // because Program.cs reads configuration during startup
        Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", _dbPath);
        
        // Set secret key for token encryption
        // Must be 32 bytes base64
        Environment.SetEnvironmentVariable("METRICS_SECRET_KEY", "dGVzdC1zZWNyZXQtZm9yLXRlc3RpbmctMzItY2hhcnM=");  // base64 encoded
        
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

    /// <summary>
    /// Load .env file into environment variables.
    /// This is called before the WebApplicationFactory initializes Program.cs.
    /// </summary>
    private void LoadEnvFile()
    {
        // List of possible paths to .env file (from various working directories)
        var possiblePaths = new[]
        {
            // From bin/Debug/net10.0/ -> solution root
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env"),
            // From solution root (when running from IDE)
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            // From tests/Integration.Tests/ -> solution root
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env"),
            // Absolute path fallback
            @"C:\Projetos\metrics-simple\.env"
        };
        
        string? envPath = null;
        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                envPath = fullPath;
                break;
            }
        }
        
        if (envPath != null && File.Exists(envPath))
        {
            try
            {
                foreach (var line in File.ReadLines(envPath))
                {
                    var trimmed = line.Trim();
                    
                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                        continue;
                    
                    // Parse KEY=VALUE
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        
                        // Remove quotes if present
                        if (value.StartsWith('"') && value.EndsWith('"'))
                            value = value.Substring(1, value.Length - 2);
                        if (value.StartsWith("'") && value.EndsWith("'"))
                            value = value.Substring(1, value.Length - 2);
                        
                        // ALWAYS set from .env (we cleared env vars before LoadEnvFile)
                        // This ensures .env is always the source of truth
                        Environment.SetEnvironmentVariable(key, value);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to load .env file: {ex.Message}");
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Load API key from appsettings.Development.json and/or environment variable
        // Environment variable takes precedence (should be loaded from .env via load-env-and-test.ps1)
        // For LLM tests: use real OpenRouter API (not mock)

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
        Environment.SetEnvironmentVariable("METRICS_SECRET_KEY", null);
        Environment.SetEnvironmentVariable("METRICS_OPENROUTER_API_KEY", null);
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        Environment.SetEnvironmentVariable("Auth__Mode", null);
        Environment.SetEnvironmentVariable("Auth__SigningKey", null);
        Environment.SetEnvironmentVariable("Auth__LocalJwt__EnableBootstrapAdmin", null);
        Environment.SetEnvironmentVariable("Auth__LocalJwt__BootstrapAdminUsername", null);
        Environment.SetEnvironmentVariable("Auth__LocalJwt__BootstrapAdminPassword", null);
    }
}
