$ErrorActionPreference = "Continue"

Write-Host "Checking http://127.0.0.1:8000/health"

$connection = Get-NetTCPConnection -LocalPort 8000 -ErrorAction SilentlyContinue |
    Select-Object -First 1 LocalAddress, LocalPort, State, OwningProcess

if ($null -eq $connection) {
    Write-Host "Port 8000 is not listening."
    exit 1
}

Write-Host "Port 8000 listener:"
$connection | Format-List

try {
    Invoke-RestMethod -Uri "http://127.0.0.1:8000/health" -TimeoutSec 5 |
        ConvertTo-Json -Depth 5
    exit 0
}
catch {
    Write-Host "Health request failed:"
    Write-Host $_.Exception.Message
    exit 1
}
