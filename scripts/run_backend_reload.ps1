$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $repoRoot "backend"
$python = Join-Path $backendDir ".venv\Scripts\python.exe"
$logDir = Join-Path $repoRoot "logs"
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$logFile = Join-Path $logDir "backend-reload-$runId.log"
$outLogFile = Join-Path $logDir "backend-reload-$runId.out.log"
$errLogFile = Join-Path $logDir "backend-reload-$runId.err.log"

if (-not (Test-Path -LiteralPath $python)) {
    Write-Error "Python virtual environment not found: $python"
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
Set-Location $backendDir
Write-Host "Starting Yui backend with reload at http://127.0.0.1:8000"
Write-Host "Backend directory: $backendDir"
Write-Host "Log file: $logFile"
Write-Host "Stdout log: $outLogFile"
Write-Host "Stderr log: $errLogFile"

Set-Content -LiteralPath $logFile -Value "==== Backend reload start $(Get-Date -Format o) ===="
$process = Start-Process -FilePath $python `
    -ArgumentList @("-m", "uvicorn", "main:app", "--reload", "--host", "127.0.0.1", "--port", "8000", "--no-use-colors") `
    -WorkingDirectory $backendDir `
    -NoNewWindow `
    -RedirectStandardOutput $outLogFile `
    -RedirectStandardError $errLogFile `
    -PassThru

Write-Host "Backend process id: $($process.Id)"
while (-not $process.HasExited) {
    Write-Host "Backend running pid=$($process.Id) $(Get-Date -Format HH:mm:ss)"
    Start-Sleep -Seconds 10
}

$exitCode = $process.ExitCode
Add-Content -LiteralPath $logFile -Value "==== Backend reload exited code $exitCode $(Get-Date -Format o) ===="
Write-Host "Backend exited with code $exitCode"
exit $exitCode
