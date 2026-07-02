param(
    [int]$BackendPort = 8000,
    [int]$VoicevoxPort = 50021,
    [int]$IrodoriPort = 0
)

$ErrorActionPreference = "Continue"

$repoRoot = Split-Path -Parent $PSScriptRoot

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
    $enabled = $env:IRODORI_ENABLE
    if ($enabled -match "^(0|false)$") {
        return $false
    }
    if ($enabled -match "^(1|true)$") {
        return $true
    }

    return ($env:HTTP_TTS_PROVIDER_ID -like "*irodori*")
}

function Stop-ProcessOnPort {
    param(
        [int]$Port,
        [string]$Name
    )

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($null -eq $connections) {
        Write-Host "$Name is not listening on port $Port."
        return
    }

    $processIds = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($processId in $processIds) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($null -eq $process) {
            continue
        }

        Write-Host "Stopping $Name pid=$processId process=$($process.ProcessName)"
        Stop-Process -Id $processId -Force
    }
}

Import-DotEnv -Path (Join-Path $repoRoot ".env")

Stop-ProcessOnPort -Port $BackendPort -Name "Backend"
Stop-ProcessOnPort -Port $VoicevoxPort -Name "VOICEVOX Engine"
if ($IrodoriPort -le 0 -and (Test-IrodoriConfigured) -and -not [string]::IsNullOrWhiteSpace($env:HTTP_TTS_BASE_URL)) {
    $IrodoriPort = Get-UrlPort -Url $env:HTTP_TTS_BASE_URL
}
if ($IrodoriPort -gt 0) {
    Stop-ProcessOnPort -Port $IrodoriPort -Name "Irodori TTS"
}
