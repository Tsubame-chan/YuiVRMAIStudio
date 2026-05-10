param(
    [string]$BackendHost = "127.0.0.1",
    [int]$BackendPort = 8000,
    [string]$VoicevoxHost = "127.0.0.1",
    [int]$VoicevoxPort = 50021,
    [string]$VoicevoxEngineExe = "",
    [int]$VoicevoxCpuThreads = [Environment]::ProcessorCount,
    [int]$StartupTimeoutSeconds = 90,
    [switch]$NoWait
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path $repoRoot "backend"
$backendPython = Join-Path $backendDir ".venv\Scripts\python.exe"
$logDir = Join-Path $repoRoot "logs"
$runtimeDir = Join-Path $repoRoot "runtime"
$voicevoxLocalAppData = Join-Path $runtimeDir "voicevox-localappdata"
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$powershellExe = (Get-Process -Id $PID).Path

function Write-Step {
    param([string]$Message)
    Write-Host "[Yui services] $Message"
}

function Test-HttpOk {
    param(
        [string]$Url,
        [int]$TimeoutSec = 2
    )

    try {
        Invoke-RestMethod -Uri $Url -TimeoutSec $TimeoutSec | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Wait-HttpOk {
    param(
        [string]$Name,
        [string]$Url,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-HttpOk -Url $Url -TimeoutSec 2) {
            Write-Step "$Name is ready: $Url"
            return $true
        }

        Start-Sleep -Seconds 1
    }

    Write-Warning "$Name did not become ready within $TimeoutSeconds seconds: $Url"
    return $false
}

function ConvertTo-QuotedArgument {
    param([string]$Value)
    return '"' + $Value.Replace('"', '\"') + '"'
}

function Resolve-VoicevoxEngineExe {
    param([string]$RequestedPath)

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $candidates += $RequestedPath
    }

    if (-not [string]::IsNullOrWhiteSpace($env:VOICEVOX_ENGINE_EXE)) {
        $candidates += $env:VOICEVOX_ENGINE_EXE
    }

    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $candidates += (Join-Path $env:LOCALAPPDATA "Programs\VOICEVOX\vv-engine\run.exe")
    }

    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates += (Join-Path $env:ProgramFiles "VOICEVOX\vv-engine\run.exe")
    }

    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw @"
VOICEVOX Engine run.exe was not found.

Install VOICEVOX first, then either:
  - install it to the default Windows location:
    %LOCALAPPDATA%\Programs\VOICEVOX\vv-engine\run.exe
  - or set VOICEVOX_ENGINE_EXE to the full path of vv-engine\run.exe
  - or pass -VoicevoxEngineExe "C:\path\to\VOICEVOX\vv-engine\run.exe"

VOICEVOX is a prerequisite and is not bundled with this project.
"@
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $voicevoxLocalAppData | Out-Null

$voicevoxBaseUrl = "http://$VoicevoxHost`:$VoicevoxPort"
$backendBaseUrl = "http://$BackendHost`:$BackendPort"

Write-Step "Repository: $repoRoot"
Write-Step "Logs: $logDir"

if (Test-HttpOk -Url "$voicevoxBaseUrl/version") {
    Write-Step "VOICEVOX Engine is already running: $voicevoxBaseUrl"
}
else {
    $resolvedVoicevoxEngineExe = Resolve-VoicevoxEngineExe -RequestedPath $VoicevoxEngineExe
    $voicevoxScript = Join-Path $PSScriptRoot "run_voicevox_engine_optimized.ps1"
    $voicevoxOut = Join-Path $logDir "voicevox-service-$runId.out.log"
    $voicevoxErr = Join-Path $logDir "voicevox-service-$runId.err.log"
    Write-Step "Starting optimized VOICEVOX Engine on $voicevoxBaseUrl"
    Write-Step "VOICEVOX logs: $voicevoxOut / $voicevoxErr"

    $voicevoxArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (ConvertTo-QuotedArgument $voicevoxScript),
        "-EngineExe", (ConvertTo-QuotedArgument $resolvedVoicevoxEngineExe),
        "-Port", "$VoicevoxPort",
        "-CpuThreads", "$VoicevoxCpuThreads",
        "-EnableCancellableSynthesis"
    ) -join " "

    $oldLocalAppData = $env:LOCALAPPDATA
    try {
        $env:LOCALAPPDATA = $voicevoxLocalAppData
        $voicevoxProcess = Start-Process -FilePath $powershellExe `
            -ArgumentList $voicevoxArgs `
            -WorkingDirectory $repoRoot `
            -WindowStyle Hidden `
            -RedirectStandardOutput $voicevoxOut `
            -RedirectStandardError $voicevoxErr `
            -PassThru
    }
    finally {
        $env:LOCALAPPDATA = $oldLocalAppData
    }

    Write-Step "VOICEVOX launcher pid: $($voicevoxProcess.Id)"
    Wait-HttpOk -Name "VOICEVOX Engine" -Url "$voicevoxBaseUrl/version" -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
}

if (Test-HttpOk -Url "$backendBaseUrl/health") {
    Write-Step "Backend is already running: $backendBaseUrl"
    Write-Warning "This launcher will reuse the existing backend on this port. If you are testing a freshly extracted copy and old conversations appear, stop the old service first with Stop_Yui_Backend_And_VOICEVOX.bat, then start again from the new folder."
}
else {
    if (-not (Test-Path -LiteralPath $backendPython)) {
        throw "Backend Python virtual environment was not found: $backendPython"
    }

    $backendOut = Join-Path $logDir "backend-service-$runId.out.log"
    $backendErr = Join-Path $logDir "backend-service-$runId.err.log"
    Write-Step "Starting backend on $backendBaseUrl"
    Write-Step "Backend logs: $backendOut / $backendErr"

    $backendProcess = Start-Process -FilePath $backendPython `
        -ArgumentList @("-m", "uvicorn", "main:app", "--host", $BackendHost, "--port", "$BackendPort", "--no-use-colors") `
        -WorkingDirectory $backendDir `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendOut `
        -RedirectStandardError $backendErr `
        -PassThru

    Write-Step "Backend pid: $($backendProcess.Id)"
    Wait-HttpOk -Name "Backend" -Url "$backendBaseUrl/health" -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
}

Write-Host ""
Write-Step "Startup check:"
Write-Host "  VOICEVOX: $voicevoxBaseUrl/version"
Write-Host "  Backend : $backendBaseUrl/health"
Write-Host ""
Write-Host "Open the Unity editor or Windows app now."
Write-Host "Keep this window open while using Yui."
Write-Host "When you are done, press Enter here to stop both Backend and VOICEVOX."

if (-not $NoWait) {
    Write-Host ""
    Read-Host "Press Enter to stop Yui local services"
    Write-Host ""
    & (Join-Path $PSScriptRoot "stop_local_services.ps1") -BackendPort $BackendPort -VoicevoxPort $VoicevoxPort
    Write-Host ""
    Write-Step "Yui local services stopped."
    Read-Host "Press Enter to close this window"
}
