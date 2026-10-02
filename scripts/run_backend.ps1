$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $repoRoot "backend"
$python = Join-Path $backendDir ".venv\Scripts\python.exe"

if (-not (Test-Path -LiteralPath $python)) {
    throw "Backend Python virtual environment not found: $python"
}

Set-Location $backendDir
$backendHost = if ($env:BACKEND_HOST) { $env:BACKEND_HOST } else { "127.0.0.1" }
$backendPort = if ($env:BACKEND_PORT) { [int]$env:BACKEND_PORT } else { 8000 }
Write-Host "Starting Yui backend at http://$backendHost`:$backendPort"
Write-Host "Backend Console: http://127.0.0.1:$backendPort/admin/"
$process = Start-Process -FilePath $python -ArgumentList @("-m", "uvicorn", "main:app", "--host", $backendHost, "--port", $backendPort, "--no-use-colors", "--no-proxy-headers") -WorkingDirectory $backendDir -NoNewWindow -PassThru
& $python (Join-Path $PSScriptRoot "service_ownership.py") record --directory (Join-Path $repoRoot "runtime/owned-services") --name "backend-$backendPort" --pid $process.Id
if ($LASTEXITCODE -ne 0) { throw "Could not register the started Backend process." }
$process.WaitForExit()
exit $process.ExitCode
