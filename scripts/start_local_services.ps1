param(
    [string]$BackendHost = "127.0.0.1",
    [int]$BackendPort = 8000,
    [string]$VoicevoxHost = "127.0.0.1",
    [int]$VoicevoxPort = 50021,
    [string]$VoicevoxEngineExe = "",
    [int]$VoicevoxCpuThreads = [Environment]::ProcessorCount,
    [string]$IrodoriBaseUrl = "",
    [string]$IrodoriHealthEndpoint = "",
    [string]$IrodoriStartCommand = "",
    [int]$StartupTimeoutSeconds = 90,
    [switch]$SkipIrodori,
    [switch]$SkipVoicevox,
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

function Import-DotEnv {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    foreach ($line in Get-Content -LiteralPath $Path) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith("#") -or -not $line.Contains("=")) {
            continue
        }

        $key, $value = $line.Split("=", 2)
        if ($key -notmatch "^[A-Za-z_][A-Za-z0-9_]*$") {
            continue
        }

        if ([string]::IsNullOrEmpty([Environment]::GetEnvironmentVariable($key, "Process"))) {
            [Environment]::SetEnvironmentVariable($key, $value, "Process")
        }
    }
}

function Write-Step {
    param([string]$Message)
    Write-Host "[Yui services] $Message"
}

function Record-OwnedPid {
    param(
        [string]$Name,
        [int]$ProcessId
    )

    if ([string]::IsNullOrWhiteSpace($env:YUI_BACKEND_OWNERSHIP_FILE)) {
        return
    }

    $ownerDir = Split-Path -Parent $env:YUI_BACKEND_OWNERSHIP_FILE
    if (-not [string]::IsNullOrWhiteSpace($ownerDir)) {
        New-Item -ItemType Directory -Force -Path $ownerDir | Out-Null
    }

    Add-Content -LiteralPath $env:YUI_BACKEND_OWNERSHIP_FILE -Value "$Name $ProcessId"
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

function Join-BaseUrl {
    param(
        [string]$BaseUrl,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $BaseUrl
    }

    if ($Path.StartsWith("http://", [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith("https://", [StringComparison]::OrdinalIgnoreCase)) {
        return $Path
    }

    return $BaseUrl.TrimEnd("/") + "/" + $Path.TrimStart("/")
}

function Get-UrlPort {
    param([string]$Url)

    try {
        $uri = [Uri]$Url
        if ($uri.Port -gt 0) {
            return $uri.Port
        }
    }
    catch {
    }

    return 0
}

function Test-IrodoriConfigured {
    if ($SkipIrodori) {
        return $false
    }

    $enabled = $env:IRODORI_ENABLE
    if ($enabled -match "^(0|false)$") {
        return $false
    }
    if ($enabled -match "^(1|true)$") {
        return $true
    }

    return ($env:HTTP_TTS_PROVIDER_ID -like "*irodori*")
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

    return ""
}

function Ensure-BackendPython {
    if (Test-Path -LiteralPath $backendPython) {
        return
    }

    $setupScript = Join-Path $PSScriptRoot "setup_backend_byok.ps1"
    if (-not (Test-Path -LiteralPath $setupScript)) {
        throw "Backend Python virtual environment was not found and setup script is missing: $setupScript"
    }

    Write-Step "Backend virtual environment is missing. Running first-time backend setup."
    & $powershellExe -NoProfile -ExecutionPolicy Bypass -File $setupScript -ProjectRoot $repoRoot
    if (-not (Test-Path -LiteralPath $backendPython)) {
        throw "Backend Python virtual environment was not created: $backendPython"
    }
}

function Start-IrodoriIfConfigured {
    if (-not (Test-IrodoriConfigured)) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($script:IrodoriBaseUrl)) {
        $script:IrodoriBaseUrl = if ([string]::IsNullOrWhiteSpace($env:IRODORI_BASE_URL)) { $env:HTTP_TTS_BASE_URL } else { $env:IRODORI_BASE_URL }
    }
    if ([string]::IsNullOrWhiteSpace($script:IrodoriBaseUrl)) {
        $script:IrodoriBaseUrl = "http://127.0.0.1:8088"
    }

    if ([string]::IsNullOrWhiteSpace($script:IrodoriHealthEndpoint)) {
        $script:IrodoriHealthEndpoint = if ([string]::IsNullOrWhiteSpace($env:HTTP_TTS_HEALTH_ENDPOINT)) { "/health" } else { $env:HTTP_TTS_HEALTH_ENDPOINT }
    }

    $healthUrl = Join-BaseUrl -BaseUrl $script:IrodoriBaseUrl -Path $script:IrodoriHealthEndpoint
    if (Test-HttpOk -Url $healthUrl) {
        Write-Step "Irodori TTS is already running: $healthUrl"
        return
    }

    if ([string]::IsNullOrWhiteSpace($script:IrodoriStartCommand)) {
        $script:IrodoriStartCommand = $env:IRODORI_START_COMMAND
    }

    if ([string]::IsNullOrWhiteSpace($script:IrodoriStartCommand)) {
        Write-Warning "Irodori TTS is configured but IRODORI_START_COMMAND is not set. Start Irodori-TTS-Server separately, or set IRODORI_ENABLE=0 to hide this warning."
        return
    }

    $irodoriOut = Join-Path $logDir "irodori-service-$runId.out.log"
    $irodoriErr = Join-Path $logDir "irodori-service-$runId.err.log"
    Write-Step "Starting Irodori TTS with IRODORI_START_COMMAND"
    Write-Step "Irodori logs: $irodoriOut / $irodoriErr"

    $irodoriProcess = Start-Process -FilePath "cmd.exe" `
        -ArgumentList @("/c", $script:IrodoriStartCommand) `
        -WorkingDirectory $repoRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $irodoriOut `
        -RedirectStandardError $irodoriErr `
        -PassThru

    Write-Step "Irodori launcher pid: $($irodoriProcess.Id)"
    Wait-HttpOk -Name "Irodori TTS" -Url $healthUrl -TimeoutSeconds 180 | Out-Null
}

Import-DotEnv -Path (Join-Path $repoRoot ".env")
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
New-Item -ItemType Directory -Force -Path $voicevoxLocalAppData | Out-Null

$voicevoxBaseUrl = "http://$VoicevoxHost`:$VoicevoxPort"
$backendBaseUrl = "http://$BackendHost`:$BackendPort"

Write-Step "Repository: $repoRoot"
Write-Step "Logs: $logDir"

if ($SkipVoicevox) {
    Write-Step "Skipping VOICEVOX startup."
}
elseif (Test-HttpOk -Url "$voicevoxBaseUrl/version") {
    Write-Step "VOICEVOX Engine is already running: $voicevoxBaseUrl"
}
else {
    $resolvedVoicevoxEngineExe = Resolve-VoicevoxEngineExe -RequestedPath $VoicevoxEngineExe
    if ([string]::IsNullOrWhiteSpace($resolvedVoicevoxEngineExe)) {
        Write-Warning "VOICEVOX Engine was not found. Text chat and backend features can still work, but backend VOICEVOX speech needs VOICEVOX installed."
    }
    else {
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
    Record-OwnedPid -Name "voicevox" -ProcessId $voicevoxProcess.Id
    Wait-HttpOk -Name "VOICEVOX Engine" -Url "$voicevoxBaseUrl/version" -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
    }
}

Start-IrodoriIfConfigured

if (Test-HttpOk -Url "$backendBaseUrl/health") {
    Write-Step "Backend is already running: $backendBaseUrl"
}
else {
    Ensure-BackendPython

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
    Record-OwnedPid -Name "backend" -ProcessId $backendProcess.Id
    Wait-HttpOk -Name "Backend" -Url "$backendBaseUrl/health" -TimeoutSeconds $StartupTimeoutSeconds | Out-Null
}

Write-Host ""
Write-Step "Startup check:"
Write-Host "  VOICEVOX: $voicevoxBaseUrl/version"
if (Test-IrodoriConfigured) {
    $displayIrodoriBaseUrl = if ([string]::IsNullOrWhiteSpace($script:IrodoriBaseUrl)) { $env:HTTP_TTS_BASE_URL } else { $script:IrodoriBaseUrl }
    $displayIrodoriHealthEndpoint = if ([string]::IsNullOrWhiteSpace($script:IrodoriHealthEndpoint)) { $env:HTTP_TTS_HEALTH_ENDPOINT } else { $script:IrodoriHealthEndpoint }
    if ([string]::IsNullOrWhiteSpace($displayIrodoriBaseUrl)) { $displayIrodoriBaseUrl = "http://127.0.0.1:8088" }
    if ([string]::IsNullOrWhiteSpace($displayIrodoriHealthEndpoint)) { $displayIrodoriHealthEndpoint = "/health" }
    Write-Host "  Irodori : $(Join-BaseUrl -BaseUrl $displayIrodoriBaseUrl -Path $displayIrodoriHealthEndpoint)"
}
Write-Host "  Backend : $backendBaseUrl/health"
Write-Host ""
Write-Host "Open the Unity editor or Windows app now."
Write-Host "Keep this window open while using Yui."
Write-Host "When you are done, press Enter here to stop local Yui services."

if (-not $NoWait) {
    Write-Host ""
    Read-Host "Press Enter to stop Yui local services"
    Write-Host ""
    $irodoriStopPort = 0
    if (Test-IrodoriConfigured) {
        $displayIrodoriBaseUrl = if ([string]::IsNullOrWhiteSpace($script:IrodoriBaseUrl)) { $env:HTTP_TTS_BASE_URL } else { $script:IrodoriBaseUrl }
        if (-not [string]::IsNullOrWhiteSpace($displayIrodoriBaseUrl)) {
            $irodoriStopPort = Get-UrlPort -Url $displayIrodoriBaseUrl
        }
    }
    & (Join-Path $PSScriptRoot "stop_local_services.ps1") -BackendPort $BackendPort -VoicevoxPort $VoicevoxPort -IrodoriPort $irodoriStopPort
    Write-Host ""
    Write-Step "Yui local services stopped."
    Read-Host "Press Enter to close this window"
}
