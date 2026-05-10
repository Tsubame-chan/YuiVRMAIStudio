$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $repoRoot "backend"
$python = Join-Path $backendDir ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $python)) {
    throw "Backend Python virtual environment not found: $python"
}

Set-Location $backendDir
Write-Host "Starting Yui backend at http://127.0.0.1:8000"
& $python -m uvicorn main:app --host 127.0.0.1 --port 8000 --no-use-colors
