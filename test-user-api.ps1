$ErrorActionPreference = "Stop"

Write-Host "Test: Creating New User via API"
Write-Host "================================`n"

try {
    Write-Host "1. Getting admin token..."
    $tokenResp = Invoke-WebRequest -Uri http://localhost:8080/api/auth/token `
      -Method POST `
      -Headers @{"Content-Type"="application/json"} `
      -Body '{"username":"admin","password":"ChangeMe123!"}' `
      -UseBasicParsing
    $token = ($tokenResp.Content | ConvertFrom-Json).access_token
    Write-Host "   [OK] Token: $($token.Substring(0, 20))...`n"

    Write-Host "2. Creating user 'carol'..."
    $createBody = @{
        username = "carol"
        password = "CarolPass123!"
        displayName = "Carol Johnson"
        email = "carol@example.com"
        roles = @("Metrics.Reader")
    } | ConvertTo-Json
    
    $createResp = Invoke-WebRequest -Uri http://localhost:8080/api/admin/auth/users `
      -Method POST `
      -Headers @{"Content-Type"="application/json"; "Authorization"="Bearer $token"} `
      -Body $createBody `
      -UseBasicParsing
    
    $userData = $createResp.Content | ConvertFrom-Json
    Write-Host "   [OK] User created`n"
    Write-Host "   Response: $($userData | ConvertTo-Json -Depth 2)`n"
    
    $userId = $userData.id

    Write-Host "3. Login with new user..."
    $loginResp = Invoke-WebRequest -Uri http://localhost:8080/api/auth/token `
      -Method POST `
      -Headers @{"Content-Type"="application/json"} `
      -Body '{"username":"carol","password":"CarolPass123!"}' `
      -UseBasicParsing
    $newToken = ($loginResp.Content | ConvertFrom-Json).access_token
    Write-Host "   [OK] Login successful`n"

    Write-Host "4. Get current user info..."
    $meResp = Invoke-WebRequest -Uri http://localhost:8080/api/auth/me `
      -Headers @{"Authorization"="Bearer $newToken"} `
      -UseBasicParsing
    $meInfo = $meResp.Content | ConvertFrom-Json
    Write-Host "   [OK] Current user: $($meInfo.sub) with roles: $($meInfo.roles -join ', ')`n"

    Write-Host "5. Change password (admin action)..."
    $passBody = @{ newPassword = "NewCarolPass456!" } | ConvertTo-Json
    $passResp = Invoke-WebRequest -Uri "http://localhost:8080/api/admin/auth/users/$userId/password" `
      -Method PUT `
      -Headers @{"Content-Type"="application/json"; "Authorization"="Bearer $token"} `
      -Body $passBody `
      -UseBasicParsing
    Write-Host "   [OK] Password changed`n"

    Write-Host "6. Verify login with new password..."
    $login2Resp = Invoke-WebRequest -Uri http://localhost:8080/api/auth/token `
      -Method POST `
      -Headers @{"Content-Type"="application/json"} `
      -Body '{"username":"carol","password":"NewCarolPass456!"}' `
      -UseBasicParsing
    Write-Host "   [OK] Login with new password successful`n"

    Write-Host "7. Admin updating user roles..."
    $roleBody = @{ roles = @("Metrics.Admin", "Metrics.Reader") } | ConvertTo-Json
    $roleResp = Invoke-WebRequest -Uri "http://localhost:8080/api/admin/auth/users/$userId" `
      -Method PUT `
      -Headers @{"Content-Type"="application/json"; "Authorization"="Bearer $token"} `
      -Body $roleBody `
      -UseBasicParsing
    $updatedUser = $roleResp.Content | ConvertFrom-Json
    Write-Host "   [OK] Roles updated to: $($updatedUser.roles -join ', ')`n"

    Write-Host "================================"
    Write-Host "[SUCCESS] All tests passed!"
}
catch {
    Write-Host "`n[ERROR] $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $streamReader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        $errorBody = $streamReader.ReadToEnd()
        Write-Host "Response: $errorBody" -ForegroundColor Red
    }
    exit 1
}
