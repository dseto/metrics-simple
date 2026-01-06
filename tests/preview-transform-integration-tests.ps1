#!/usr/bin/env pwsh
<#
.SYNOPSIS
Integration tests for POST /api/v1/preview/transform endpoint using real HGBrasil Weather API data
.DESCRIPTION
Creates Connectors, Processes, and ProcessVersions with real DSLs and tests the preview/transform endpoint
#>

# Configuration
$apiBase = "http://localhost:5000/api"
$adminUser = "admin"
$adminPass = "ChangeMe123!"

# Colors for output
$colors = @{
    Success = "Green"
    Error   = "Red"
    Info    = "Cyan"
    Warn    = "Yellow"
}

function Write-Status($msg, $color = "Info") {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $msg" -ForegroundColor $colors[$color]
}

function Get-BearerToken {
    Write-Status "Step 1: Obtaining authentication token..."
    
    $body = @{
        username = $adminUser
        password = $adminPass
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$apiBase/auth/token" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body
        
        Write-Status "Token obtained successfully" "Success"
        # Response contains 'access_token' field
        return $response.access_token
    } catch {
        Write-Status "Failed to obtain token: $_" "Error"
        throw
    }
}

function Create-Connector($token, $connectorId, $displayName, $baseUrl, $defaultTokenHeader) {
    Write-Status "Creating Connector: $connectorId..."
    
    $body = @{
        id = $connectorId
        displayName = $displayName
        baseUrl = $baseUrl
        requestDefaults = @{
            headers = @{
                $defaultTokenHeader = "key=f110205d"
            }
        }
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$apiBase/v1/connectors" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -Headers @{Authorization = "Bearer $token"}
        
        Write-Status "Connector created: $($response.id)" "Success"
        return $response
    } catch {
        Write-Status "Failed to create connector: $_" "Error"
        throw
    }
}

function Create-Process($token, $processId, $connectorId, $displayName) {
    Write-Status "Creating Process: $processId..."
    
    $body = @{
        id = $processId
        connectorId = $connectorId
        displayName = $displayName
        description = "Test process for $displayName"
    } | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$apiBase/v1/processes" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -Headers @{Authorization = "Bearer $token"}
        
        Write-Status "Process created: $($response.id)" "Success"
        return $response
    } catch {
        Write-Status "Failed to create process: $_" "Error"
        throw
    }
}

function Create-ProcessVersion($token, $processId, $versionNum, $endpoint, $dsl, $outputSchema) {
    Write-Status "Creating ProcessVersion v$versionNum for $processId..."
    
    $body = @{
        version = $versionNum
        enabled = $true
        sourceRequest = @{
            method = "GET"
            path = $endpoint
        }
        dsl = @{
            profile = "jsonata"
            text = $dsl
        }
        outputSchema = $outputSchema
    } | ConvertTo-Json -Depth 10
    
    try {
        $response = Invoke-RestMethod -Uri "$apiBase/v1/processes/$processId/versions" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -Headers @{Authorization = "Bearer $token"}
        
        Write-Status "ProcessVersion v$versionNum created" "Success"
        return $response
    } catch {
        Write-Status "Failed to create process version: $_" "Error"
        throw
    }
}

function Test-PreviewTransform($token, $sampleInput, $dslProfile, $dslText, $outputSchema, $testName) {
    Write-Status "Testing preview/transform: $testName..."
    
    $body = @{
        sampleInput = $sampleInput
        dsl = @{
            profile = $dslProfile
            text = $dslText
        }
        outputSchema = $outputSchema
    } | ConvertTo-Json -Depth 10
    
    try {
        $response = Invoke-RestMethod -Uri "$apiBase/v1/preview/transform" `
            -Method POST `
            -ContentType "application/json" `
            -Body $body `
            -Headers @{Authorization = "Bearer $token"}
        
        Write-Status "Preview succeeded: $testName" "Success"
        Write-Host "Output: $($response.previewOutput | ConvertTo-Json)" -ForegroundColor Cyan
        return $response
    } catch {
        Write-Status "Preview failed: $testName - $_" "Error"
        throw
    }
}

# ============================================================================
# MAIN TEST EXECUTION
# ============================================================================

Write-Status "========== Preview/Transform Integration Tests ==========" "Info"

try {
    # Step 1: Get Token
    $token = Get-BearerToken
    Write-Host ""
    
    # Step 2: Create HGBrasil Connector
    $connector = Create-Connector $token `
        "hgbrasil-weather-api" `
        "HGBrasil Weather API" `
        "https://api.hgbrasil.com" `
        "key"
    Write-Host ""
    
    # Step 3: Create Process
    $process = Create-Process $token `
        "weather-curitiba" `
        "hgbrasil-weather-api" `
        "Weather Data - Curitiba"
    Write-Host ""
    
    # Real HGBrasil API response sample
    $hgbrasil_sample = @{
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
            img_id = "28"
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
                    full_date = "05/01/2026"
                    weekday = "Seg"
                    max = 25
                    min = 13
                    humidity = 53
                    cloudiness = 65.0
                    rain = 0.1
                    rain_probability = 20
                    wind_speedy = "5.08 km/h"
                    sunrise = "05:32 am"
                    sunset = "07:12 pm"
                    moon_phase = "full"
                    description = "Chuvas esparsas"
                    condition = "rain"
                },
                @{
                    date = "06/01"
                    full_date = "06/01/2026"
                    weekday = "Ter"
                    max = 23
                    min = 13
                    humidity = 64
                    cloudiness = 93.0
                    rain = 0.24
                    rain_probability = 48
                    wind_speedy = "3.81 km/h"
                    sunrise = "05:33 am"
                    sunset = "07:12 pm"
                    moon_phase = "waning_gibbous"
                    description = "Chuvas esparsas"
                    condition = "rain"
                }
            )
            cref = "f6770a"
        }
        execution_time = 0.0
        from_cache = $true
    }
    
    # Output schema for weather summary
    $outputSchema = @{
        type = "object"
        properties = @{
            city = @{type = "string"}
            current_temp = @{type = "number"}
            humidity = @{type = "number"}
            forecast_count = @{type = "integer"}
            avg_high_temp = @{type = "number"}
            condition = @{type = "string"}
        }
        required = @("city", "current_temp")
    }
    
    # Test 1: Simple city and current temperature extraction
    Write-Status "Test 1: Extract city name and current temperature" "Info"
    Test-PreviewTransform $token `
        $hgbrasil_sample `
        "jsonata" `
        '{"city": results.city_name, "current_temp": results.temp}' `
        $outputSchema `
        "Basic city and temp extraction"
    Write-Host ""
    
    # Test 2: Weather summary with forecast info
    Write-Status "Test 2: Weather summary with forecast count" "Info"
    Test-PreviewTransform $token `
        $hgbrasil_sample `
        "jsonata" `
        '{"city": results.city_name, "current_temp": results.temp, "humidity": results.humidity, "forecast_count": $count(results.forecast), "condition": results.description}' `
        $outputSchema `
        "Weather summary with forecast"
    Write-Host ""
    
    # Test 3: Forecast temperatures average
    Write-Status "Test 3: Calculate average forecast temperatures" "Info"
    Test-PreviewTransform $token `
        $hgbrasil_sample `
        "jsonata" `
        '{"city": results.city_name, "current_temp": results.temp, "avg_high_temp": $average(results.forecast.max), "forecast_count": $count(results.forecast)}' `
        $outputSchema `
        "Forecast average calculation"
    Write-Host ""
    
    # Test 4: Complex transformation - average of (max+min)/2
    Write-Status "Test 4: Calculate mean temperature for each forecast day" "Info"
    Test-PreviewTransform $token `
        $hgbrasil_sample `
        "jsonata" `
        '{
            "city": results.city_name,
            "current_temp": results.temp,
            "avg_mean_temp": $average(results.forecast.((max + min) / 2)),
            "forecast_details": results.forecast.{"date": date, "mean_temp": (max + min) / 2, "condition": description}
        }' `
        @{
            type = "object"
            properties = @{
                city = @{type = "string"}
                current_temp = @{type = "number"}
                avg_mean_temp = @{type = "number"}
                forecast_details = @{
                    type = "array"
                    items = @{
                        type = "object"
                        properties = @{
                            date = @{type = "string"}
                            mean_temp = @{type = "number"}
                            condition = @{type = "string"}
                        }
                    }
                }
            }
        } `
        "Mean temperature with forecast details"
    Write-Host ""
    
    # Test 5: Filter and transform - only rainy days
    Write-Status "Test 5: Filter rainy forecast days" "Info"
    Test-PreviewTransform $token `
        $hgbrasil_sample `
        "jsonata" `
        '{
            "city": results.city_name,
            "rainy_days": results.forecast[condition="rain"].{"date": date, "rain_mm": rain, "probability": rain_probability}
        }' `
        @{
            type = "object"
            properties = @{
                city = @{type = "string"}
                rainy_days = @{
                    type = "array"
                    items = @{
                        type = "object"
                        properties = @{
                            date = @{type = "string"}
                            rain_mm = @{type = "number"}
                            probability = @{type = "integer"}
                        }
                    }
                }
            }
        } `
        "Rainy days filter"
    Write-Host ""
    
    Write-Status "========== ALL TESTS COMPLETED SUCCESSFULLY ==========" "Success"
    
} catch {
    Write-Status "Test suite failed: $_" "Error"
    exit 1
}
