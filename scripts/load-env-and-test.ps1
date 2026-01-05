# Load environment variables from .env file before running tests
# Usage: .\scripts\load-env-and-test.ps1

param(
    [switch]$Build = $false,
    [switch]$IntegrationOnly = $false
)

# Path to .env file
$envFile = Join-Path $PSScriptRoot "..\\.env"

if (Test-Path $envFile) {
    Write-Host "Loading environment variables from .env..." -ForegroundColor Green
    
    # Parse and load .env file
    Get-Content $envFile | ForEach-Object {
        $line = $_.Trim()
        
        # Skip empty lines and comments
        if ($line -and !$line.StartsWith("#")) {
            # Parse KEY=VALUE
            if ($line -match "^([^=]+)=(.*)$") {
                $key = $matches[1].Trim()
                $value = $matches[2].Trim()
                
                # Remove quotes if present
                if ($value.StartsWith('"') -and $value.EndsWith('"')) {
                    $value = $value.Substring(1, $value.Length - 2)
                }
                if ($value.StartsWith("'") -and $value.EndsWith("'")) {
                    $value = $value.Substring(1, $value.Length - 2)
                }
                
                # Set environment variable
                [Environment]::SetEnvironmentVariable($key, $value, [EnvironmentVariableTarget]::Process)
                Write-Host "  Set: $key = $(if ($value.Length -gt 50) { $value.Substring(0, 50) + '...' } else { $value })" -ForegroundColor Cyan
            }
        }
    }
    
    Write-Host "Environment variables loaded successfully`n" -ForegroundColor Green
} else {
    Write-Host "WARNING: .env file not found at: $envFile" -ForegroundColor Yellow
    Write-Host "Some tests may fail if API keys are not set.`n" -ForegroundColor Yellow
}

# Verify critical variables
if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable("METRICS_SECRET_KEY"))) {
    Write-Host "WARNING: METRICS_SECRET_KEY not set!" -ForegroundColor Yellow
}

if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY"))) {
    Write-Host "WARNING: METRICS_OPENROUTER_API_KEY not set! LLM tests will fail." -ForegroundColor Yellow
}

Write-Host "Running tests..." -ForegroundColor Green

# Run build if requested
if ($Build) {
    Write-Host "Building project..." -ForegroundColor Cyan
    dotnet build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}

# Run tests
if ($IntegrationOnly) {
    Write-Host "Running integration tests only..." -ForegroundColor Cyan
    dotnet test --filter "FullyQualifiedName~Integration.Tests" --no-build
} else {
    Write-Host "Running all tests..." -ForegroundColor Cyan
    dotnet test
}

exit $LASTEXITCODE
