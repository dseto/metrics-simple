<#
PowerShell script to call the local LLM HTTP API.
Usage: Run in PowerShell: `.	ests\test_local_llm.ps1`
#>

$uri = 'http://localhost:11434/v1/chat/completions'

$body = @{
    model = 'deepseek-r1'
    messages = @(
        @{ role = 'system'; content = 'Você é um assistente de teste que responde de forma concisa.' }
        @{ role = 'user';   content = 'Qual a capital do Brasil?' }
    )
    temperature = 0.3
}

$json = $body | ConvertTo-Json -Depth 5

try {
    $response = Invoke-RestMethod -Uri $uri -Method Post -ContentType 'application/json' -Body $json -ErrorAction Stop
    $response | ConvertTo-Json -Depth 5
}
catch {
    Write-Error "Request failed: $($_.Exception.Message)"
    exit 1
}