# ============================================================================
# Docker Compose Up Script for Metrics Simple
# Builds, starts containers, and performs health checks
# ============================================================================

param(
    [switch]$Rebuild = $false,
    [switch]$NoWait = $false,
    [int]$HealthCheckTimeout = 60,
    [switch]$Logs = $false
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host ("=" * 80)
Write-Host "Metrics Simple - Docker Compose Startup" -ForegroundColor Cyan
Write-Host ("=" * 80)

# ============================================================================
# 1. Check Docker Desktop is running
# ============================================================================
Write-Host "`n[1/5] Checking Docker Desktop..." -ForegroundColor Yellow

try {
    $dockerVersion = docker version --format "{{.Server.Version}}" 2>$null
    if (-not $dockerVersion) {
        throw "Docker is not responding"
    }
    Write-Host "[OK] Docker is running (v$dockerVersion)" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Docker Desktop is not running or not installed" -ForegroundColor Red
    Write-Host "  Please start Docker Desktop and try again" -ForegroundColor Yellow
    exit 1
}

# ============================================================================
# 2. Check Docker Compose
# ============================================================================
Write-Host "`n[2/5] Checking Docker Compose..." -ForegroundColor Yellow

try {
    $composeVersion = docker compose version --format "{{.Version}}" 2>$null
    if (-not $composeVersion) {
        throw "Docker Compose is not available"
    }
    Write-Host "[OK] Docker Compose is available (v$composeVersion)" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Docker Compose is not available" -ForegroundColor Red
    exit 1
}

# ============================================================================
# 3. Check/Build Dockerfile
# ============================================================================
Write-Host "`n[3/5] Checking Dockerfiles..." -ForegroundColor Yellow

$apiDockerfile = Join-Path $projectRoot "src\Api\Dockerfile"
$runnerDockerfile = Join-Path $projectRoot "src\Runner\Dockerfile"

if (-not (Test-Path $apiDockerfile)) {
    Write-Host "[FAIL] API Dockerfile not found at $apiDockerfile" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] API Dockerfile found" -ForegroundColor Green

if (-not (Test-Path $runnerDockerfile)) {
    Write-Host "[FAIL] Runner Dockerfile not found at $runnerDockerfile" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Runner Dockerfile found" -ForegroundColor Green

# ============================================================================
# 4. Build and Start Containers
# ============================================================================
Write-Host "`n[4/5] Building and starting containers..." -ForegroundColor Yellow

Push-Location $projectRoot
try {
    # Build containers
    if ($Rebuild) {
        Write-Host "Building containers (--build flag enabled)..." -ForegroundColor Cyan
        docker compose build --no-cache
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose build failed"
        }
        Write-Host "[OK] Containers built successfully" -ForegroundColor Green
    } else {
        Write-Host "Starting containers (use -Rebuild to rebuild)..." -ForegroundColor Cyan
        docker compose build 2>&1 | Where-Object { $_ -match "DONE|Starting" } | ForEach-Object { Write-Host "  $_" }
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose build failed"
        }
        Write-Host "[OK] Containers ready" -ForegroundColor Green
    }

    # Start containers
    Write-Host "Starting services..." -ForegroundColor Cyan
    docker compose up -d
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed"
    }
    Write-Host "[OK] Containers started" -ForegroundColor Green
} finally {
    Pop-Location
}

# ============================================================================
# 5. Health Check
# ============================================================================
Write-Host "`n[5/5] Performing health checks..." -ForegroundColor Yellow

if ($NoWait) {
    Write-Host "[SKIP] Skipping health checks (--NoWait flag enabled)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host ("=" * 80)
    Write-Host "Containers started successfully!" -ForegroundColor Green
    Write-Host ("=" * 80)
    Write-Host ""
    Write-Host "API available at: http://localhost:8080" -ForegroundColor Cyan
    Write-Host "Swagger UI: http://localhost:8080/swagger" -ForegroundColor Cyan
    Write-Host ""
    exit 0
}

$startTime = Get-Date
$healthy = $false
$apiHealthy = $false
$sqliteReady = $false

do {
    $elapsed = (Get-Date) - $startTime
    $remainingSeconds = $HealthCheckTimeout - [int]$elapsed.TotalSeconds

    # Check API health
    if (-not $apiHealthy) {
        try {
            $response = Invoke-WebRequest -Uri "http://localhost:8080/api/health" -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                Write-Host "[OK] API is healthy (port 8080)" -ForegroundColor Green
                $apiHealthy = $true
            }
        } catch {
            $status = "$([int]$elapsed.TotalSeconds)s - Waiting for API..."
            Write-Host "`r  $status" -NoNewline -ForegroundColor Yellow
        }
    }

    # Check SQLite container
    if (-not $sqliteReady) {
        try {
            $container = docker ps --filter "name=sqlite" --format "{{.Status}}" 2>$null
            if ($container -match "Up") {
                Write-Host "[OK] SQLite database is ready" -ForegroundColor Green
                $sqliteReady = $true
            }
        } catch {
            # Silently fail
        }
    }

    # Both services ready?
    if ($apiHealthy -and $sqliteReady) {
        $healthy = $true
        break
    }

    # Timeout?
    if ($elapsed.TotalSeconds -gt $HealthCheckTimeout) {
        Write-Host ""
        Write-Host "[FAIL] Health check timeout after ${HealthCheckTimeout}s" -ForegroundColor Red
        break
    }

    # Wait before retry
    Start-Sleep -Milliseconds 500
} while (-not $healthy)

Write-Host ""

# ============================================================================
# Summary
# ============================================================================
Write-Host "═" * 80

if ($healthy) {
    Write-Host "All services are running and healthy!" -ForegroundColor Green
    Write-Host ("=" * 80)
    Write-Host ""
    Write-Host "API Status:" -ForegroundColor Cyan
    Write-Host "  - API: http://localhost:8080" -ForegroundColor Green
    Write-Host "  - Swagger UI: http://localhost:8080/swagger" -ForegroundColor Green
    Write-Host "  - Health Check: http://localhost:8080/api/health" -ForegroundColor Green
    Write-Host ""
    
    if ($Logs) {
        Write-Host "Showing live logs (Ctrl+C to stop):" -ForegroundColor Yellow
        Write-Host ""
        docker compose logs -f
    } else {
        Write-Host "Useful commands:" -ForegroundColor Cyan
        Write-Host "  - View logs: docker compose logs -f" -ForegroundColor Gray
        Write-Host "  - View specific service: docker compose logs -f csharp-api" -ForegroundColor Gray
        Write-Host "  - Stop containers: docker compose down" -ForegroundColor Gray
        Write-Host ""
    }
} else {
    Write-Host "[FAIL] Health check failed!" -ForegroundColor Red
    Write-Host ("=" * 80)
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  - Check logs: docker compose logs" -ForegroundColor Gray
    Write-Host "  - Stop containers: docker compose down" -ForegroundColor Gray
    Write-Host "  - Rebuild: .\scripts\docker-up.ps1 -Rebuild" -ForegroundColor Gray
    Write-Host ""
    
    # Show logs on error
    Write-Host "Recent logs:" -ForegroundColor Yellow
    docker compose logs --tail 20
    exit 1
}
