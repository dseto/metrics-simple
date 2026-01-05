#!/usr/bin/env pwsh
# Setup Process 324134 with Version via curl

$BaseUrl = "http://localhost:8080/api/v1"
$AuthUrl = "http://localhost:8080/api/auth"
$ConnectorId = "test-connector-$(Get-Random -Minimum 1000 -Maximum 9999)"
$ProcessId = "324134"
$Version = 1

Write-Host ""
Write-Host "=== Setup Process 324134 ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Login
Write-Host "[1/4] Logging in..." -ForegroundColor Yellow
$loginJson = '{"username":"admin","password":"ChangeMe123!"}'
$loginResp = curl.exe -s -X POST -H "Content-Type: application/json" -d $loginJson "$AuthUrl/token" -w "`n%{http_code}"

$respLines = @($loginResp | Select-String "." -AllMatches).Line
$statusCode = @($loginResp)[-1]

if ($statusCode -ne "200") {
    Write-Host "ERROR: Login failed ($statusCode)" -ForegroundColor Red
    Write-Host $loginResp
    exit 1
}

# Parse token from response (last line is status code, previous lines are JSON)
$jsonLines = @($loginResp[0..($loginResp.Count-2)])
$jsonStr = $jsonLines -join "`n"
$tokenObj = $jsonStr | ConvertFrom-Json
$token = $tokenObj.accessToken

Write-Host "✓ Logged in successfully" -ForegroundColor Green
Write-Host ""

# Step 2: Create Connector
Write-Host "[2/4] Creating Connector..." -ForegroundColor Yellow
$connJson = @{
    id = $ConnectorId
    name = "Test Connector"
    baseUrl = "https://jsonplaceholder.typicode.com"
    authRef = "token"
    timeoutSeconds = 30
} | ConvertTo-Json

$connResp = curl.exe -s -X POST -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d $connJson "$BaseUrl/connectors" -w "`n%{http_code}"
$statusCode = @($connResp)[-1]

if ($statusCode -eq "201") {
    Write-Host "✓ Connector created: $ConnectorId" -ForegroundColor Green
}
else {
    Write-Host "! Connector status: $statusCode" -ForegroundColor Yellow
}

Write-Host ""

# Step 3: Create Process
Write-Host "[3/4] Creating Process..." -ForegroundColor Yellow
$procJson = @{
    id = $ProcessId
    name = "Test Process 324134"
    status = "Active"
    connectorId = $ConnectorId
    outputDestinations = @()
} | ConvertTo-Json

$procResp = curl.exe -s -X POST -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d $procJson "$BaseUrl/processes" -w "`n%{http_code}"
$statusCode = @($procResp)[-1]

if ($statusCode -eq "201") {
    Write-Host "✓ Process created: $ProcessId" -ForegroundColor Green
}
else {
    Write-Host "! Process status: $statusCode" -ForegroundColor Yellow
}

Write-Host ""

# Step 4: Create Version
Write-Host "[4/4] Creating Version..." -ForegroundColor Yellow
$verJson = @{
    version = $Version
    enabled = $true
    sourceRequest = @{
        method = "GET"
        path = "/posts/1"
        headers = @{ Accept = "application/json" }
        queryParams = @{}
    }
    dsl = @{
        profile = "jsonata"
        text = '{ id: id, title: title, body: body }'
    }
    outputSchema = @{
        type = "object"
        properties = @{
            id = @{ type = "number" }
            title = @{ type = "string" }
            body = @{ type = "string" }
        }
    }
    sampleInput = @{
        id = 1
        title = "Test"
        body = "Content"
    }
} | ConvertTo-Json -Depth 10

$verResp = curl.exe -s -X POST -H "Content-Type: application/json" -H "Authorization: Bearer $token" -d $verJson "$BaseUrl/processes/$ProcessId/versions" -w "`n%{http_code}"
$statusCode = @($verResp)[-1]

if ($statusCode -eq "201") {
    Write-Host "✓ Version created: $Version" -ForegroundColor Green
}
else {
    Write-Host "! Version status: $statusCode" -ForegroundColor Yellow
    Write-Host $verResp
}

Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Green
Write-Host "Process ID: $ProcessId" -ForegroundColor Gray
Write-Host "Version: $Version" -ForegroundColor Gray
Write-Host "Connector: $ConnectorId" -ForegroundColor Gray
Write-Host ""
