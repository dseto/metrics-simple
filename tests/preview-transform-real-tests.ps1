#!/usr/bin/env pwsh
<#
.SYNOPSIS
Real-world DSL generation and preview/transform tests using HGBrasil Weather API
.DESCRIPTION
Tests the POST /api/v1/preview/transform endpoint with realistic data from an external API.
This validates DSL generation robustness and path analysis improvements.
#>

param(
    [string]$ApiUrl = "http://localhost:5000",
    [bool]$Verbose = $true
)

$ErrorActionPreference = "Stop"

# Helper functions
function Log {
    param([string]$Message, [string]$Level = "INFO")
    $timestamp = Get-Date -Format "HH:mm:ss"
    Write-Host "[$timestamp] [$Level] $Message"
}

function Log-Success {
    param([string]$Message)
    Log $Message "✓"
}

function Log-Error {
    param([string]$Message)
    Log $Message "✗"
}

function Log-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "═" * 80
    Write-Host $Title.ToUpper()
    Write-Host "═" * 80
}

# Real sample data from HGBrasil API
$hgbrasilSample = @{
    by = "city_name"
    valid_key = $true
    results = @{
        temp = 16
        date = "05/01/2026"
        time = "19:01"
        condition_code = "28"
        description = "Tempo nublado"
        currently = "dia"
        woeid = 455822
        city = "Curitiba, PR"
        humidity = 83
        cloudiness = 75.0
        rain = 0.0
        wind_speedy = "5.66 km/h"
        wind_direction = 100
        wind_cardinal = "L"
        sunrise = "05:32 am"
        sunset = "07:12 pm"
        moon_phase = "full"
        condition_slug = "cloud"
        city_name = "Curitiba"
        timezone = "-03:00"
        forecast = @(
            @{
                date = "05/01"
                weekday = "Seg"
                max = 25
                min = 13
                humidity = 53
                cloudiness = 65.0
                rain = 0.1
                rain_probability = 20
                description = "Chuvas esparsas"
                condition = "rain"
            },
            @{
                date = "06/01"
                weekday = "Ter"
                max = 23
                min = 13
                humidity = 64
                cloudiness = 93.0
                rain = 0.24
                rain_probability = 48
                description = "Chuvas esparsas"
                condition = "rain"
            }
        )
    }
} | ConvertTo-Json -Depth 10

# Test cases
$testCases = @(
    @{
        Name = "TC-001: Extract Current Temperature (Simple Path)"
        Goal = "Extract current temperature from results.temp"
        SampleInput = $hgbrasilSample
        ExpectedDSL = 'results.temp'  # Simple number
        Description = "Tests basic path extraction without nested objects"
    },
    @{
        Name = "TC-002: Current Conditions Object"
        Goal = "Create object with current weather conditions"
        SampleInput = $hgbrasilSample
        ExpectedDSL = '{ "temp": results.temp, "humidity": results.humidity, "city": results.city_name, "condition": results.description }'
        Description = "Tests object construction with multiple fields"
    },
    @{
        Name = "TC-003: Average Temperature from Forecast (Critical Path Test)"
        Goal = "Calculate average of min and max temperatures in forecast"
        SampleInput = $hgbrasilSample
        ExpectedDSL = '{ "avg_forecast_temp": $average(results.forecast.((max + min) / 2)) }'
        Description = "Tests nested array iteration, arithmetic on collections, and proper parenthesization"
    },
    @{
        Name = "TC-004: Forecast Summary"
        Goal = "Create summary of forecast days"
        SampleInput = $hgbrasilSample
        ExpectedDSL = 'results.forecast.{ "date": date, "high": max, "low": min, "condition": condition, "rain_chance": rain_probability }'
        Description = "Tests array mapping and field selection from nested arrays"
    },
    @{
        Name = "TC-005: Rain Analysis"
        Goal = "Analyze rain probability across forecast days"
        SampleInput = $hgbrasilSample
        ExpectedDSL = '{ "max_rain_chance": $max(results.forecast.rain_probability), "avg_rain": $average(results.forecast.rain), "rainy_days": $count(results.forecast[rain_probability > 0]) }'
        Description = "Tests aggregation functions with filtering"
    },
    @{
        Name = "TC-006: Wind Information"
        Goal = "Extract and format wind data"
        SampleInput = $hgbrasilSample
        ExpectedDSL = '{ "wind_speed": results.wind_speedy, "direction": results.wind_cardinal, "raw_direction_degrees": results.wind_direction }'
        Description = "Tests string extraction and multiple types"
    },
    @{
        Name = "TC-007: Forecast Statistics"
        Goal = "Calculate statistics from forecast"
        SampleInput = $hgbrasilSample
        ExpectedDSL = '{ "days": $count(results.forecast), "highest_temp": $max(results.forecast.max), "lowest_temp": $min(results.forecast.min), "avg_humidity": $average(results.forecast.humidity) }'
        Description = "Tests multiple aggregation functions"
    },
    @{
        Name = "TC-008: Detailed Day Report"
        Goal = "Create detailed report for first forecast day"
        SampleInput = $hgbrasilSample
        ExpectedDSL = 'results.forecast[0].{ "date": full_date, "weekday": weekday, "description": description, "temp_range": (max & "°C to " & min & "°C"), "humidity": humidity & "%", "rain_prob": rain_probability & "%" }'
        Description = "Tests first element access, string concatenation"
    }
)

# Main test execution
Log-Section "Starting Real-World DSL Generation Tests"
Log "API URL: $ApiUrl"
Log "Sample Data Source: HGBrasil Weather API (Curitiba, PR)"
Log "Total Test Cases: $($testCases.Count)"
Log ""

$results = @{
    Total = 0
    Passed = 0
    Failed = 0
    Errors = @()
}

foreach ($tc in $testCases) {
    $results.Total++
    
    Log-Section $tc.Name
    Log "Goal: $($tc.Goal)"
    Log "Description: $($tc.Description)"
    Log ""
    
    # Step 1: Generate DSL using AI
    Log "Step 1: Generating DSL with AI..."
    $generatePayload = @{
        goalText = $tc.Goal
        sampleInput = $hgbrasilSample | ConvertFrom-Json
        dslProfile = "jsonata"
        constraints = @{
            maxColumns = 50
            allowTransforms = $true
        }
    } | ConvertTo-Json -Depth 10
    
    try {
        $generateResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/ai/dsl/generate" `
            -Method POST `
            -ContentType "application/json" `
            -Body $generatePayload `
            -ErrorAction Stop
        
        $generateResult = $generateResponse.Content | ConvertFrom-Json
        
        if ($generateResponse.StatusCode -eq 200) {
            $generatedDSL = $generateResult.dsl.text
            Log-Success "DSL Generated Successfully"
            Log "Generated: $generatedDSL"
            Log "Expected:  $($tc.ExpectedDSL)"
            Log ""
        }
        else {
            throw "Unexpected status code: $($generateResponse.StatusCode)"
        }
    }
    catch {
        Log-Error "Failed to generate DSL: $_"
        $results.Failed++
        $results.Errors += @{ TestCase = $tc.Name; Stage = "Generation"; Error = $_.Exception.Message }
        continue
    }
    
    # Step 2: Validate with preview/transform
    Log "Step 2: Validating DSL with preview/transform..."
    $previewPayload = @{
        sampleInput = $hgbrasilSample | ConvertFrom-Json
        dsl = @{
            profile = "jsonata"
            text = $generatedDSL
        }
        outputSchema = $generateResult.outputSchema | ConvertFrom-Json
    } | ConvertTo-Json -Depth 10
    
    try {
        $previewResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/preview/transform" `
            -Method POST `
            -ContentType "application/json" `
            -Body $previewPayload `
            -ErrorAction Stop
        
        if ($previewResponse.StatusCode -eq 200) {
            $previewResult = $previewResponse.Content | ConvertFrom-Json
            
            if ($previewResult.isValid) {
                Log-Success "Preview validation passed"
                Log "Output:"
                $previewResult.previewOutput | ConvertTo-Json | Write-Host
                Log ""
                $results.Passed++
            }
            else {
                Log-Error "Preview validation failed"
                Log "Errors: $($previewResult.errors -join ', ')"
                Log ""
                $results.Failed++
                $results.Errors += @{ TestCase = $tc.Name; Stage = "Preview"; Error = $previewResult.errors -join ', ' }
            }
        }
        else {
            throw "Unexpected status code: $($previewResponse.StatusCode)"
        }
    }
    catch {
        Log-Error "Failed preview/transform: $_"
        $results.Failed++
        $results.Errors += @{ TestCase = $tc.Name; Stage = "Preview"; Error = $_.Exception.Message }
    }
}

# Summary
Log-Section "Test Execution Summary"
Log "Total: $($results.Total)"
Log-Success "Passed: $($results.Passed)"
Log-Error "Failed: $($results.Failed)"

if ($results.Failed -gt 0) {
    Log ""
    Log "Failed Test Details:"
    $results.Errors | ForEach-Object {
        Write-Host "  • $($_.TestCase) [$($_.Stage)]: $($_.Error)"
    }
}

Log ""
$successRate = [math]::Round(($results.Passed / $results.Total) * 100, 2)
Log "Success Rate: $successRate%"

exit $(if ($results.Failed -eq 0) { 0 } else { 1 })
