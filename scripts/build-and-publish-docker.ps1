# Build, Publish and Deploy to Docker Desktop
# Full automation script for metrics-simple project
# Usage: .\scripts\build-and-publish-docker.ps1

param(
    [switch]$SkipTests,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
$ComposeFile = Join-Path $ProjectRoot "compose.yaml"

# Color codes
$ColorSuccess = "Green"
$ColorError = "Red"
$ColorWarning = "Yellow"
$ColorInfo = "Cyan"

# Helper functions
function Write-Status {
    param([string]$Message, [string]$Status)
    
    $timestamp = Get-Date -Format "HH:mm:ss"
    $icon = switch ($Status) {
        "OK" { "[OK]" }
        "FAIL" { "[FAIL]" }
        "SKIP" { "[SKIP]" }
        "WAIT" { "[WAIT]" }
        default { "[*]" }
    }
    
    $color = switch ($Status) {
        "OK" { $ColorSuccess }
        "FAIL" { $ColorError }
        "SKIP" { $ColorWarning }
        default { $ColorInfo }
    }
    
    Write-Host "$timestamp $icon $Message" -ForegroundColor $color
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor $ColorInfo
    Write-Host $Title -ForegroundColor $ColorInfo
    Write-Host ("=" * 70) -ForegroundColor $ColorInfo
}

# Main flow
try {
    Write-Section "METRICS-SIMPLE: Docker Build & Publish"
    
    # Step 1: Verify project root
    Write-Status "Verifying project structure..." "WAIT"
    if (-not (Test-Path $ComposeFile)) {
        throw "compose.yaml not found at $ProjectRoot"
    }
    Write-Status "Project root: $ProjectRoot" "OK"
    
    # Step 2: Build solution
    if ($NoBuild) {
        Write-Status "Skipping dotnet build (--NoBuild)" "SKIP"
    } else {
        Write-Status "Building solution (Release)..." "WAIT"
        Push-Location $ProjectRoot
        try {
            $buildOutput = & dotnet build "Metrics.Simple.SpecDriven.sln" -c Release 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host $buildOutput
                throw "dotnet build failed with exit code $LASTEXITCODE"
            }
            Write-Status "Solution built successfully" "OK"
        } finally {
            Pop-Location
        }
    }
    
    # Step 3: Run tests (optional)
    if (-not $SkipTests) {
        Write-Status "Running unit tests..." "WAIT"
        Push-Location $ProjectRoot
        try {
            $testOutput = & dotnet test "Metrics.Simple.SpecDriven.sln" --no-build 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host $testOutput
                Write-Status "Tests failed (see above)" "FAIL"
                throw "dotnet test failed"
            }
            Write-Status "All tests passed" "OK"
        } finally {
            Pop-Location
        }
    } else {
        Write-Status "Skipping tests (--SkipTests)" "SKIP"
    }
    
    # Step 4: Stop and remove existing containers
    Write-Status "Stopping existing containers..." "WAIT"
    & docker compose -f $ComposeFile down
    if ($LASTEXITCODE -ne 0) {
        Write-Status "Warning: docker compose down returned non-zero exit code" "WARN"
        # Non-fatal, continue anyway
    } else {
        Write-Status "Containers stopped and removed" "OK"
    }
    
    # Step 5: Build Docker images
    Write-Status "Building Docker images..." "WAIT"
    & docker compose -f $ComposeFile build
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose build failed with exit code $LASTEXITCODE"
    }
    Write-Status "Docker images built successfully" "OK"
    
    # Step 6: Start containers
    Write-Status "Starting containers (detached mode)..." "WAIT"
    & docker compose -f $ComposeFile up -d
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed with exit code $LASTEXITCODE"
    }
    Write-Status "Containers started" "OK"
    
    # Step 7: Wait for API to be ready
    Write-Status "Waiting for API to be ready..." "WAIT"
    $maxRetries = 30
    $retryCount = 0
    $apiReady = $false
    
    while ($retryCount -lt $maxRetries -and -not $apiReady) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:8080/api/health" `
                                        -Method GET `
                                        -UseBasicParsing `
                                        -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) {
                $apiReady = $true
            }
        } catch {
            # API not ready yet
        }
        
        if (-not $apiReady) {
            $retryCount++
            if ($retryCount -lt $maxRetries) {
                Write-Host "  - Attempt $retryCount/$maxRetries..." -ForegroundColor Gray
                Start-Sleep -Seconds 1
            }
        }
    }
    
    if ($apiReady) {
        Write-Status "API is responding on http://localhost:8080" "OK"
    } else {
        Write-Status "API failed to start after $maxRetries seconds" "FAIL"
        Write-Host ""
        Write-Status "Checking container status..." "WAIT"
        & docker compose -f $ComposeFile ps
        Write-Host ""
        Write-Status "API logs (last 30 lines):" "WAIT"
        & docker compose -f $ComposeFile logs csharp-api --tail 30
        throw "API health check failed"
    }
    
    # Step 8: Verify critical containers running (API and SQLite)
    # Note: csharp-runner is a CLI tool, not a service, so it may exit after startup
    Write-Status "Verifying critical services..." "WAIT"
    $psOutput = & docker compose -f $ComposeFile ps --format json | ConvertFrom-Json
    
    # Only check essential services: csharp-api and sqlite
    $criticalServices = @($psOutput | Where-Object { $_.Service -in @("csharp-api", "sqlite") })
    $runningCount = @($criticalServices | Where-Object { $_.State -eq "running" }).Count
    
    if ($runningCount -eq 2) {
        Write-Status "Critical services running: $runningCount/2 (csharp-api, sqlite)" "OK"
    } else {
        Write-Status "Critical services not running: $runningCount/2" "FAIL"
        & docker compose -f $ComposeFile ps
        throw "Critical services (API, SQLite) are not running"
    }
    
    # Step 9: Display summary
    Write-Section "Build & Publish Complete"
    Write-Host ""
    Write-Host "Docker Images:" -ForegroundColor $ColorInfo
    & docker images --filter "reference=metrics-simple-*" --format "table {{.Repository}}:{{.Tag}}`t{{.Size}}`t{{.ID}}"
    Write-Host ""
    Write-Host "Running Services:" -ForegroundColor $ColorInfo
    & docker compose -f $ComposeFile ps --format "table {{.Service}}`t{{.Status}}`t{{.Ports}}"
    Write-Host ""
    Write-Host "API Endpoints:" -ForegroundColor $ColorInfo
    Write-Host "  - Health:   http://localhost:8080/api/health" -ForegroundColor Green
    Write-Host "  - OpenAPI:  http://localhost:8080/swagger/index.html" -ForegroundColor Green
    Write-Host "  - API v1:   http://localhost:8080/api/v1/*" -ForegroundColor Green
    Write-Host ""
    Write-Status "All steps completed successfully!" "OK"
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Status "ERROR: $($_.Exception.Message)" "FAIL"
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor $ColorWarning
    Write-Host "  1. Check Docker Desktop is running"
    Write-Host "  2. Verify .env file exists and contains valid credentials"
    Write-Host "  3. Check logs: docker compose -f compose.yaml logs"
    Write-Host "  4. Run individual commands manually to diagnose"
    Write-Host ""
    exit 1
}
