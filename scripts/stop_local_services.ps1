param([int]$BackendPort = 8000, [int]$VoicevoxPort = 50021, [int]$IrodoriPort = 0)
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$python = Join-Path $repoRoot "backend\.venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $python)) { throw "Backend Python was not found. Stop services in their own apps." }
& $python (Join-Path $PSScriptRoot "service_ownership.py") stop-all --directory (Join-Path $repoRoot "runtime\owned-services")
if ($LASTEXITCODE -ne 0) { throw "Could not read process ownership. Other processes were retained." }
Write-Host "Processes started outside this installation were retained."
