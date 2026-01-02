param(
  [string]$RepoRoot = (Resolve-Path ".").Path
)

$ErrorActionPreference = "Stop"

$manifestPath = Join-Path $RepoRoot "spec-deck-manifest.json"
if (!(Test-Path $manifestPath)) { throw "spec-deck-manifest.json not found at $manifestPath" }

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$missing = @()

foreach ($f in $manifest.files) {
  $p = Join-Path $RepoRoot $f.path
  if (!(Test-Path $p)) { $missing += $f.path }
}

if ($missing.Count -gt 0) {
  Write-Host "Missing files referenced by manifest:" -ForegroundColor Red
  $missing | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
  exit 1
}

Write-Host "OK: all manifest paths exist." -ForegroundColor Green
exit 0
