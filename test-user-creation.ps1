# Test User Creation API Endpoints

Write-Host "Step 1: Getting admin token..."
$tokenResponse = Invoke-WebRequest -Uri http://localhost:8080/api/auth/token `
  -Method POST `
  -Headers @{"Content-Type"="application/json"} `
  -Body '{"username":"admin","password":"ChangeMe123!"}' `
  -UseBasicParsing

$token = ($tokenResponse.Content | ConvertFrom-Json).access_token
Write-Host "[OK] Token obtained"

Write-Host "`nStep 2: Creating new user 'daniel'..."
$createResponse = Invoke-WebRequest -Uri http://localhost:8080/api/admin/auth/users `
  -Method POST `
  -Headers @{
    "Content-Type"="application/json"
    "Authorization"="Bearer $token"
  } `
  -Body @{
    username = "daniel"
    password = "SecurePass123!"
    displayName = "Daniel Silva"
    email = "daniel@example.com"
    roles = @("Metrics.Reader")
  } | ConvertTo-Json | Out-String `
  -UseBasicParsing

$userData = $createResponse.Content | ConvertFrom-Json
Write-Host "[OK] User created with ID: $($userData.id)"
Write-Host "Response:"
$createResponse.Content | ConvertFrom-Json | ConvertTo-Json

Write-Host "`nStep 3: Logging in as new user..."
$loginResponse = Invoke-WebRequest -Uri http://localhost:8080/api/auth/token `
  -Method POST `
  -Headers @{"Content-Type"="application/json"} `
  -Body '{"username":"daniel","password":"SecurePass123!"}' `
  -UseBasicParsing

$newUserToken = ($loginResponse.Content | ConvertFrom-Json).access_token
Write-Host "[OK] Login successful"

Write-Host "`nStep 4: Accessing protected endpoint as new user..."
$meResponse = Invoke-WebRequest -Uri http://localhost:8080/api/auth/me `
  -Headers @{"Authorization"="Bearer $newUserToken"} `
  -UseBasicParsing

Write-Host "[OK] Protected endpoint accessible"
Write-Host "Current user info:"
$meResponse.Content | ConvertFrom-Json | ConvertTo-Json

Write-Host "`nStep 5: Admin updating user roles..."
$updateResponse = Invoke-WebRequest -Uri "http://localhost:8080/api/admin/auth/users/$($userData.id)" `
  -Method PUT `
  -Headers @{
    "Content-Type"="application/json"
    "Authorization"="Bearer $token"
  } `
  -Body '{"roles":["Metrics.Admin","Metrics.Reader"]}' `
  -UseBasicParsing

Write-Host "[OK] User roles updated"
Write-Host "Updated user:"
$updateResponse.Content | ConvertFrom-Json | ConvertTo-Json

Write-Host "`n[DONE] All tests completed successfully!"
