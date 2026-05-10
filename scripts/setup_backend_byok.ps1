param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$OpenAIApiKey = "",
    [switch]$SkipInstall
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host "[Yui setup] $Message"
}

function Resolve-PythonCommand {
    $py = Get-Command py -ErrorAction SilentlyContinue
    if ($py) {
        return @($py.Source, "-3.12")
    }

    $python = Get-Command python -ErrorAction SilentlyContinue
    if ($python) {
        return @($python.Source)
    }

    throw "Python was not found. Install Python 3.12+ from python.org, then run this script again."
}

function Set-EnvValue {
    param(
        [string]$Path,
        [string]$Key,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    $escaped = [Regex]::Escape($Key)
    $lines = Get-Content -LiteralPath $Path
    $updated = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^$escaped=") {
            $lines[$i] = "$Key=$Value"
            $updated = $true
            break
        }
    }

    if (-not $updated) {
        $lines += "$Key=$Value"
    }

    Set-Content -LiteralPath $Path -Value $lines -Encoding UTF8
}

$backendDir = Join-Path $ProjectRoot "backend"
$venvPython = Join-Path $backendDir ".venv\Scripts\python.exe"
$envExample = Join-Path $ProjectRoot ".env.example"
$envPath = Join-Path $ProjectRoot ".env"
$requirements = Join-Path $backendDir "requirements.txt"

if (-not (Test-Path -LiteralPath $backendDir)) {
    throw "Backend directory was not found: $backendDir"
}

if (-not (Test-Path -LiteralPath $envExample)) {
    throw ".env.example was not found: $envExample"
}

if (-not (Test-Path -LiteralPath $envPath)) {
    Copy-Item -LiteralPath $envExample -Destination $envPath
    Write-Step "Created .env from .env.example"
}
else {
    Write-Step ".env already exists; keeping existing values"
}

if (-not [string]::IsNullOrWhiteSpace($OpenAIApiKey)) {
    Set-EnvValue -Path $envPath -Key "OPENAI_API_KEY" -Value $OpenAIApiKey
    Write-Step "Saved OPENAI_API_KEY to .env"
}

if (-not (Test-Path -LiteralPath $venvPython)) {
    $pythonCommand = Resolve-PythonCommand
    Write-Step "Creating backend virtual environment"
    $pythonArgs = @()
    if ($pythonCommand.Count -gt 1) {
        $pythonArgs = $pythonCommand[1..($pythonCommand.Count - 1)]
    }

    & $pythonCommand[0] @pythonArgs -m venv (Join-Path $backendDir ".venv")
}
else {
    Write-Step "Backend virtual environment already exists"
}

if (-not $SkipInstall) {
    Write-Step "Installing backend dependencies"
    & $venvPython -m pip install --upgrade pip
    & $venvPython -m pip install -r $requirements
}

Write-Host ""
Write-Step "Setup complete"
Write-Host "Next:"
Write-Host "  1. Edit .env and set OPENAI_API_KEY if you did not pass -OpenAIApiKey."
Write-Host "  2. Install VOICEVOX Engine if you want local Japanese voice playback."
Write-Host "  3. Start services with Start_Yui_Backend_And_VOICEVOX.bat."
