# Get token
Write-Host "Getting auth token..."
$authPayload = @{ username = "admin"; password = "ChangeMe123!" } | ConvertTo-Json
$authResp = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/token" -Method POST -ContentType "application/json" -Body $authPayload -UseBasicParsing
$token = ($authResp.Content | ConvertFrom-Json).access_token

# Sample data
$sampleData = @'
{
  "results": {
    "temp": 16,
    "humidity": 83,
    "city_name": "Curitiba",
    "forecast": [
      { "max": 25, "min": 13, "rain_probability": 20 },
      { "max": 23, "min": 13, "rain_probability": 48 }
    ]
  }
}
'@

# Test 1: Simple temp extraction
Write-Host ""
Write-Host "TEST 1: Extract Current Temperature"
$genPayload = @{
    goalText = "Extract current temperature from results.temp"
    sampleInput = $sampleData | ConvertFrom-Json
    dslProfile = "jsonata"
    constraints = @{ maxColumns = 50; allowTransforms = $true }
} | ConvertTo-Json -Depth 10

$genResp = Invoke-WebRequest -Uri "http://localhost:5000/api/v1/ai/dsl/generate" -Method POST -Headers @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" } -Body $genPayload -UseBasicParsing
$genResult = $genResp.Content | ConvertFrom-Json
$dsl = $genResult.dsl.text
Write-Host "Generated DSL: $dsl"

# Preview
$prevPayload = @{
    sampleInput = $sampleData | ConvertFrom-Json
    dsl = @{ profile = "jsonata"; text = $dsl }
    outputSchema = $genResult.outputSchema | ConvertFrom-Json
} | ConvertTo-Json -Depth 10

$prevResp = Invoke-WebRequest -Uri "http://localhost:5000/api/v1/preview/transform" -Method POST -Headers @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" } -Body $prevPayload -UseBasicParsing
$prevResult = $prevResp.Content | ConvertFrom-Json
Write-Host "Valid: $($prevResult.isValid)"
Write-Host "Output: $($prevResult.previewOutput | ConvertTo-Json -Compress)"

# Test 2: Critical - Forecast average
Write-Host ""
Write-Host "TEST 2: Forecast Average Temperature (CRITICAL)"
$genPayload2 = @{
    goalText = "Calculate average of min and max temperatures from forecast array"
    sampleInput = $sampleData | ConvertFrom-Json
    dslProfile = "jsonata"
    constraints = @{ maxColumns = 50; allowTransforms = $true }
} | ConvertTo-Json -Depth 10

$genResp2 = Invoke-WebRequest -Uri "http://localhost:5000/api/v1/ai/dsl/generate" -Method POST -Headers @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" } -Body $genPayload2 -UseBasicParsing
$genResult2 = $genResp2.Content | ConvertFrom-Json
$dsl2 = $genResult2.dsl.text
Write-Host "Generated DSL: $dsl2"

$prevPayload2 = @{
    sampleInput = $sampleData | ConvertFrom-Json
    dsl = @{ profile = "jsonata"; text = $dsl2 }
    outputSchema = $genResult2.outputSchema | ConvertFrom-Json
} | ConvertTo-Json -Depth 10

$prevResp2 = Invoke-WebRequest -Uri "http://localhost:5000/api/v1/preview/transform" -Method POST -Headers @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" } -Body $prevPayload2 -UseBasicParsing
$prevResult2 = $prevResp2.Content | ConvertFrom-Json
Write-Host "Valid: $($prevResult2.isValid)"
Write-Host "Output: $($prevResult2.previewOutput | ConvertTo-Json -Compress)"

Write-Host ""
Write-Host "Tests completed!"
