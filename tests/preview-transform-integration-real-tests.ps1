#!/usr/bin/env pwsh
<#
.SYNOPSIS
Integration tests for Connector/Process lifecycle with real HGBrasil Weather API
.DESCRIPTION
Tests complete workflow: create connector, create process version, generate DSL, 
preview, and validate with real external API data.
#>

param(
    [string]$ApiUrl = "http://localhost:5000",
    [string]$ConnectorId = "hgbrasil-weather-test-$(Get-Date -Format 'yyyyMMddHHmmss')"
)

$ErrorActionPreference = "Stop"

function Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

function Log-Success { param([string]$M) Log $M "✓" }
function Log-Error { param([string]$M) Log $M "✗" }
function Log-Section { param([string]$T) Write-Host "`n$(("═") * 80)`n$($T.ToUpper())`n$(("═") * 80)" }

Log-Section "Step 1: Create Connector"

# Create connector for HGBrasil Weather API
$connectorPayload = @{
    id = $ConnectorId
    displayName = "HGBrasil Weather"
    description = "Real weather API integration test"
    sourceConnectorType = "HttpRequest"
    requestDefaults = @{
        method = "GET"
        baseUrl = "https://api.hgbrasil.com"
        headers = @{
            "Accept" = "application/json"
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $connectorResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/connectors" `
        -Method POST `
        -ContentType "application/json" `
        -Body $connectorPayload `
        -ErrorAction Stop
    
    $connector = $connectorResponse.Content | ConvertFrom-Json
    Log-Success "Connector created: $ConnectorId"
    Log "Status: $($connectorResponse.StatusCode)"
}
catch {
    Log-Error "Failed to create connector: $_"
    exit 1
}

Log-Section "Step 2: Create Process Version"

# Create process with specific request config
$processPayload = @{
    connectorId = $ConnectorId
    displayName = "Weather Analysis - Test"
    description = "Analyze current and forecast weather data"
    sourceRequest = @{
        method = "GET"
        path = "/weather"
        query = @{
            "city_name" = "Curitiba,PR"
            "key" = "f110205d"
        }
        headers = @{}
    }
    dsl = @{
        profile = "jsonata"
        text = 'results.temp'  # Will be replaced after generation
    }
    outputSchema = @{
        "type" = "object"
        "properties" = @{
            "temperature" = @{ "type" = "number" }
        }
    }
} | ConvertTo-Json -Depth 10

try {
    $processResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/processes" `
        -Method POST `
        -ContentType "application/json" `
        -Body $processPayload `
        -ErrorAction Stop
    
    $process = $processResponse.Content | ConvertFrom-Json
    $processId = $process.id
    Log-Success "Process created: $processId"
}
catch {
    Log-Error "Failed to create process: $_"
    exit 1
}

Log-Section "Step 3: Fetch Real Sample Data"

# Call the real API to get sample data
try {
    $weatherResponse = Invoke-WebRequest -Uri "https://api.hgbrasil.com/weather?city_name=Curitiba%2CPR&key=f110205d" `
        -Method GET `
        -ErrorAction Stop
    
    $weatherData = $weatherResponse.Content | ConvertFrom-Json
    Log-Success "Real API data fetched successfully"
    Log "Response from HGBrasil: Status=$(if($weatherData.valid_key) { 'VALID' } else { 'INVALID' })"
    Log "Current temp: $($weatherData.results.temp)°C"
    Log "Forecast days: $($weatherData.results.forecast.Count)"
}
catch {
    Log-Error "Failed to fetch real API data: $_"
    exit 1
}

Log-Section "Step 4: Test Case 1 - Simple Temperature Extraction"

$testCases = @(
    @{
        Name = "Extract Current Temperature"
        Goal = "Get current temperature from real HGBrasil API response"
        ExpectedDSL = 'results.temp'
        SampleInput = $weatherData | ConvertTo-Json -Depth 10
    },
    @{
        Name = "Weather Summary"
        Goal = "Create object with temperature, humidity, condition and city"
        ExpectedDSL = '{ "temperature": results.temp, "humidity": results.humidity, "description": results.description, "city": results.city_name }'
        SampleInput = $weatherData | ConvertTo-Json -Depth 10
    },
    @{
        Name = "Forecast Average Temperature"
        Goal = "Calculate average temperature from forecast (min+max)/2 for each day"
        ExpectedDSL = '{ "avg_forecast_temp": $average(results.forecast.((max + min) / 2)) }'
        SampleInput = $weatherData | ConvertTo-Json -Depth 10
    },
    @{
        Name = "Detailed Forecast"
        Goal = "Map forecast data with date, conditions, and temperature range"
        ExpectedDSL = 'results.forecast.{ "date": date, "max": max, "min": min, "condition": condition, "humidity": humidity }'
        SampleInput = $weatherData | ConvertTo-Json -Depth 10
    },
    @{
        Name = "Rain Statistics"
        Goal = "Calculate max rain probability and days with rain in forecast"
        ExpectedDSL = '{ "max_rain_prob": $max(results.forecast.rain_probability), "avg_rain_mm": $average(results.forecast.rain), "rainy_days": $count(results.forecast[rain_probability > 0]) }'
        SampleInput = $weatherData | ConvertTo-Json -Depth 10
    }
)

$testResults = @{ Passed = 0; Failed = 0 }

foreach ($test in $testCases) {
    Log ""
    Log-Section "Test: $($test.Name)"
    Log "Goal: $($test.Goal)"
    
    # Generate DSL
    Log "Generating DSL..."
    $genPayload = @{
        goalText = $test.Goal
        sampleInput = $test.SampleInput | ConvertFrom-Json
        dslProfile = "jsonata"
        constraints = @{ maxColumns = 50; allowTransforms = $true }
    } | ConvertTo-Json -Depth 10
    
    try {
        $genResp = Invoke-WebRequest -Uri "$ApiUrl/api/v1/ai/dsl/generate" `
            -Method POST `
            -ContentType "application/json" `
            -Body $genPayload `
            -ErrorAction Stop
        
        $genResult = $genResp.Content | ConvertFrom-Json
        $generatedDSL = $genResult.dsl.text
        Log-Success "DSL Generated: $generatedDSL"
        
        # Preview/Transform
        Log "Running preview/transform..."
        $prevPayload = @{
            sampleInput = $test.SampleInput | ConvertFrom-Json
            dsl = @{ profile = "jsonata"; text = $generatedDSL }
            outputSchema = $genResult.outputSchema | ConvertFrom-Json
        } | ConvertTo-Json -Depth 10
        
        $prevResp = Invoke-WebRequest -Uri "$ApiUrl/api/v1/preview/transform" `
            -Method POST `
            -ContentType "application/json" `
            -Body $prevPayload `
            -ErrorAction Stop
        
        $prevResult = $prevResp.Content | ConvertFrom-Json
        
        if ($prevResult.isValid) {
            Log-Success "Transformation successful"
            Log "Output:"
            $prevResult.previewOutput | ConvertTo-Json -Compress | Write-Host
            $testResults.Passed++
        }
        else {
            Log-Error "Transformation validation failed"
            Log "Errors: $($prevResult.errors -join ', ')"
            $testResults.Failed++
        }
    }
    catch {
        Log-Error "Test failed: $_"
        $testResults.Failed++
    }
}

Log-Section "Test Summary"
Log-Success "Passed: $($testResults.Passed)/$($testCases.Count)"
if ($testResults.Failed -gt 0) {
    Log-Error "Failed: $($testResults.Failed)/$($testCases.Count)"
}

$rate = [math]::Round(($testResults.Passed / $testCases.Count) * 100, 2)
Log "Success Rate: $rate%"

Log ""
Log "Test completed. Connector: $ConnectorId"
Log "To clean up, delete connector: DELETE $ApiUrl/api/v1/connectors/$ConnectorId"

exit $(if ($testResults.Failed -eq 0) { 0 } else { 1 })
