# ============================================================================
# Docker Cleanup Script
# Stops and removes containers, optionally removes volumes
# ============================================================================

param(
    [switch]$RemoveVolumes = $false,
    [switch]$Confirm = $false
)

$ErrorActionPreference = "Stop"

Write-Host "═" * 80
Write-Host "Docker Cleanup" -ForegroundColor Cyan
Write-Host "═" * 80
Write-Host ""

# Show what will be removed
Write-Host "The following will be removed:" -ForegroundColor Yellow
if ($RemoveVolumes) {
    Write-Host "  • Containers" -ForegroundColor Red
    Write-Host "  • Networks" -ForegroundColor Red
    Write-Host "  • Volumes (including database!)" -ForegroundColor Red
} else {
    Write-Host "  • Containers" -ForegroundColor Yellow
    Write-Host "  • Networks" -ForegroundColor Yellow
    Write-Host "  • Data will be preserved" -ForegroundColor Green
}

Write-Host ""

# Confirm
if (-not $Confirm) {
    $response = Read-Host "Continue? (yes/no)"
    if ($response -ne "yes") {
        Write-Host "Cancelled" -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "Stopping containers..." -ForegroundColor Cyan

try {
    docker compose down $(if ($RemoveVolumes) { "-v" } else { "" })
    Write-Host "✓ Cleanup complete" -ForegroundColor Green
} catch {
    Write-Host "✗ Cleanup failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Status:" -ForegroundColor Yellow
docker compose ps

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
