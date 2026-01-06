param(
    [string]$ApiUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

function Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

function Log-Success { Log $args[0] "PASS" }
function Log-Error { Log $args[0] "FAIL" }
function Log-Section {
    Write-Host ""
    Write-Host ("=" * 80)
    Write-Host $args[0].ToUpper()
    Write-Host ("=" * 80)
}

Log-Section "Real API Tests - HGBrasil Weather"
Log "API URL: $ApiUrl"
Log ""

# Get auth token
Log "Getting auth token..."
$authPayload = @{
    username = "admin"
    password = "ChangeMe123!"
} | ConvertTo-Json

$authResp = Invoke-WebRequest -Uri "$ApiUrl/api/auth/token" `
    -Method POST `
    -ContentType "application/json" `
    -Body $authPayload `
    -ErrorAction Stop

$authResult = $authResp.Content | ConvertFrom-Json
$token = $authResult.token
Log "Token obtained"
Log ""

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Test data from real HGBrasil API
$weatherJson = @'
{
  "by":"city_name",
  "valid_key":true,
  "results":{
    "temp":16,
    "date":"05/01/2026",
    "time":"19:01",
    "condition_code":"28",
    "description":"Tempo nublado",
    "currently":"dia",
    "woeid":455822,
    "city":"Curitiba, PR",
    "img_id":"28",
    "humidity":83,
    "cloudiness":75.0,
    "rain":0.0,
    "wind_speedy":"5.66 km/h",
    "wind_direction":100,
    "wind_cardinal":"L",
    "sunrise":"05:32 am",
    "sunset":"07:12 pm",
    "moon_phase":"full",
    "condition_slug":"cloud",
    "city_name":"Curitiba",
    "timezone":"-03:00",
    "forecast":[
      {
        "date":"05/01",
        "full_date":"05/01/2026",
        "weekday":"Seg",
        "max":25,
        "min":13,
        "humidity":53,
        "cloudiness":65.0,
        "rain":0.1,
        "rain_probability":20,
        "wind_speedy":"5.08 km/h",
        "sunrise":"05:32 am",
        "sunset":"07:12 pm",
        "moon_phase":"full",
        "description":"Chuvas esparsas",
        "condition":"rain"
      },
      {
        "date":"06/01",
        "full_date":"06/01/2026",
        "weekday":"Ter",
        "max":23,
        "min":13,
        "humidity":64,
        "cloudiness":93.0,
        "rain":0.24,
        "rain_probability":48,
        "wind_speedy":"3.81 km/h",
        "sunrise":"05:33 am",
        "sunset":"07:12 pm",
        "moon_phase":"waning_gibbous",
        "description":"Chuvas esparsas",
        "condition":"rain"
      }
    ],
    "cref":"f6770a"
  },
  "execution_time":0.0,
  "from_cache":true
}
'@

$testCases = @(
    @{
        Name = "TC-001: Current Temperature"
        Goal = "Extract current temperature from results.temp"
        SampleJson = $weatherJson
    },
    @{
        Name = "TC-002: Weather Summary"
        Goal = "Create object with temperature, humidity, city and condition"
        SampleJson = $weatherJson
    },
    @{
        Name = "TC-003: Forecast Average Temperature"
        Goal = "Calculate average of min and max temperatures from forecast array"
        SampleJson = $weatherJson
    },
    @{
        Name = "TC-004: Forecast Details"
        Goal = "Map forecast data showing date, high, low, and rain probability"
        SampleJson = $weatherJson
    },
    @{
        Name = "TC-005: Rain Statistics"
        Goal = "Calculate max rain probability and average rain from forecast"
        SampleJson = $weatherJson
    }
)

$passed = 0
$failed = 0

foreach ($tc in $testCases) {
    Log-Section $tc.Name
    Log "Goal: $($tc.Goal)"
    Log ""
    
    $sampleInput = $tc.SampleJson | ConvertFrom-Json
    
    Log "Step 1: Generate DSL..."
    $genPayload = @{
        goalText = $tc.Goal
        sampleInput = $sampleInput
        dslProfile = "jsonata"
        constraints = @{
            maxColumns = 50
            allowTransforms = $true
        }
    } | ConvertTo-Json -Depth 10
    
    try {
        $genResp = Invoke-WebRequest -Uri "$ApiUrl/api/v1/ai/dsl/generate" `
            -Method POST `
            -Headers $headers `
            -Body $genPayload `
            -ErrorAction Stop
        
        $genResult = $genResp.Content | ConvertFrom-Json
        $dsl = $genResult.dsl.text
        
        Log-Success "DSL generated"
        Log "DSL: $dsl"
        Log ""
        
        Log "Step 2: Validate with preview/transform..."
        $prevPayload = @{
            sampleInput = $sampleInput
            dsl = @{
                profile = "jsonata"
                text = $dsl
            }
            outputSchema = $genResult.outputSchema | ConvertFrom-Json
        } | ConvertTo-Json -Depth 10
        
        $prevResp = Invoke-WebRequest -Uri "$ApiUrl/api/v1/preview/transform" `
            -Method POST `
            -Headers $headers `
            -Body $prevPayload `
            -ErrorAction Stop
        
        $prevResult = $prevResp.Content | ConvertFrom-Json
        
        if ($prevResult.isValid) {
            Log-Success "Validation passed"
            Log "Output: $($prevResult.previewOutput | ConvertTo-Json -Compress)"
            $passed++
        }
        else {
            Log-Error "Validation failed: $($prevResult.errors -join ', ')"
            $failed++
        }
    }
    catch {
        Log-Error "Error: $($_.Exception.Message)"
        $failed++
    }
    
    Log ""
}

Log-Section "Test Summary"
$total = $passed + $failed
$pct = [math]::Round(($passed / $total) * 100, 2)
Log "Total: $total"
Log "Passed: $passed"
Log "Failed: $failed"
Log "Success Rate: $pct%"

if ($failed -eq 0) {
    Log-Success "All tests passed!"
    exit 0
}
else {
    Log-Error "Some tests failed"
    exit 1
}
