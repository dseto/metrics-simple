# ============================================================================
# Docker Health Check Script
# Monitors health of running containers
# ============================================================================

param(
    [switch]$Watch = $false,
    [int]$RefreshInterval = 5
)

$ErrorActionPreference = "Continue"

function Get-ContainerStatus {
    try {
        $containers = docker compose ps --format "json" | ConvertFrom-Json
        return $containers
    } catch {
        Write-Host "Error getting container status: $_" -ForegroundColor Red
        return $null
    }
}

function Get-ApiHealth {
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8080/api/health" -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

function Show-Status {
    Clear-Host
    Write-Host "═" * 80
    Write-Host "Docker Containers Health Status" -ForegroundColor Cyan
    Write-Host "═" * 80
    Write-Host ""

    $containers = Get-ContainerStatus

    if ($null -eq $containers) {
        Write-Host "No containers running" -ForegroundColor Yellow
        return
    }

    # Ensure it's an array
    if (-not ($containers -is [System.Array])) {
        $containers = @($containers)
    }

    # Display containers table
    Write-Host "Container Status:" -ForegroundColor Yellow
    Write-Host ""
    
    foreach ($container in $containers) {
        $status = $container.State
        $statusColor = if ($status -eq "running") { "Green" } else { "Red" }
        $statusSymbol = if ($status -eq "running") { "✓" } else { "✗" }
        
        Write-Host ("{0} {1,-20} {2,-30} {3}" -f $statusSymbol, $container.Names, $status, $container.Ports) -ForegroundColor $statusColor
    }

    Write-Host ""
    Write-Host "API Health:" -ForegroundColor Yellow
    
    $apiHealthy = Get-ApiHealth
    if ($apiHealthy) {
        Write-Host "✓ http://localhost:8080/api/health - OK (HTTP 200)" -ForegroundColor Green
        Write-Host "  • Swagger UI: http://localhost:8080/swagger" -ForegroundColor Green
    } else {
        Write-Host "✗ http://localhost:8080/api/health - NOT RESPONDING" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Docker Healthcheck Status:" -ForegroundColor Yellow
    
    $apiHealthcheck = docker ps --filter "name=csharp-api" --format "{{.State}}" 2>$null
    $sqliteStatus = docker ps --filter "name=sqlite" --format "{{.State}}" 2>$null
    
    if ($apiHealthcheck -eq "running") {
        $health = docker inspect csharp-api --format "{{json .State.Health.Status}}" 2>$null | ConvertFrom-Json
        $healthColor = if ($health -eq "healthy") { "Green" } else { "Yellow" }
        Write-Host "  • csharp-api: $health" -ForegroundColor $healthColor
    }
    
    if ($sqliteStatus -eq "running") {
        Write-Host "  • sqlite: running" -ForegroundColor Green
    }

    Write-Host ""
    Write-Host "Last Update: $(Get-Date -Format 'HH:mm:ss')" -ForegroundColor Gray
    
    if ($Watch) {
        Write-Host "Refreshing in $RefreshInterval seconds (Ctrl+C to stop)..." -ForegroundColor Gray
    }
}

# Main loop
if ($Watch) {
    while ($true) {
        Show-Status
        Start-Sleep -Seconds $RefreshInterval
    }
} else {
    Show-Status
}
