param([string]$PackRoot = (Split-Path $PSScriptRoot -Parent), [string]$BackendRoot = '')
$ErrorActionPreference = 'Stop'
$PackRoot = (Resolve-Path -LiteralPath $PackRoot).Path
if (-not (Get-Command nvidia-smi.exe -ErrorAction SilentlyContinue)) {
    throw 'This INT8 V4.1 package requires an NVIDIA GPU and current CUDA-capable driver. VOICEVOX remains available without this package.'
}
if (-not (Get-Command git.exe -ErrorAction SilentlyContinue)) {
    throw 'Install Git for Windows from https://git-scm.com/download/win and run this installer again.'
}
$manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $PackRoot 'files.json') | ConvertFrom-Json
foreach ($file in $manifest.files) {
    $path = Join-Path $PackRoot $file.path
    if (-not (Test-Path -LiteralPath $path) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256) {
        throw "Package integrity check failed: $($file.path)"
    }
}
$server = Join-Path $PackRoot 'server'
$uv = Join-Path $PackRoot 'runtime/uv.exe'
Push-Location $server
try {
    & $uv sync --frozen --extra cu128 --python 3.12
    if ($LASTEXITCODE -ne 0) { throw 'Python/CUDA dependency installation failed. No Backend settings were changed.' }
} finally { Pop-Location }
if (-not $BackendRoot) {
    $parent = Split-Path $PackRoot -Parent
    if (Test-Path (Join-Path $parent 'backend/app/main.py')) { $BackendRoot = $parent }
    elseif (Test-Path (Join-Path $parent 'YuiBackend/backend/app/main.py')) { $BackendRoot = Join-Path $parent 'YuiBackend' }
    else { $BackendRoot = Read-Host 'Enter the existing YuiBackend folder path' }
}
$BackendRoot = (Resolve-Path -LiteralPath $BackendRoot).Path
if (-not (Test-Path (Join-Path $BackendRoot 'backend/app/main.py'))) { throw 'The selected folder is not a Yui Backend.' }
$envFile = Join-Path $BackendRoot '.env'
$lines = @()
if (Test-Path -LiteralPath $envFile) {
    Copy-Item -LiteralPath $envFile -Destination ($envFile + '.irodori-backup-' + (Get-Date -Format 'yyyyMMddHHmmss'))
    $lines = @(Get-Content -LiteralPath $envFile -Encoding UTF8)
}
$settings = [ordered]@{
    IRODORI_ENABLE='1'; IRODORI_BASE_URL='http://127.0.0.1:8088'
    IRODORI_START_COMMAND=('powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + (Join-Path $PackRoot 'scripts/start_irodori_v4_windows.ps1') + '" -PackRoot "' + $PackRoot + '"')
    HTTP_TTS_BASE_URL='http://127.0.0.1:8088'; HTTP_TTS_ENDPOINT='/v1/audio/speech'; HTTP_TTS_HEALTH_ENDPOINT='/health'
    HTTP_TTS_PROVIDER_ID='irodori-server'; HTTP_TTS_PAYLOAD_FORMAT='irodori_openai_speech'; HTTP_TTS_MODEL='irodori-tts'
    HTTP_TTS_VOICE='bright_natural'; HTTP_TTS_FORMAT='wav'; HTTP_TTS_IRODORI_NUM_STEPS='40'
    HTTP_TTS_IRODORI_CHUNKING_ENABLED='true'; HTTP_TTS_IRODORI_CHUNK_MIN_CHARS='1'; HTTP_TTS_IRODORI_FIRST_SENTENCE_CHUNK_MIN_CHARS='1'
}
foreach ($key in $settings.Keys) {
    $lines = @($lines | Where-Object { $_ -notmatch ('^\s*' + [regex]::Escape($key) + '\s*=') })
    $lines += "$key=$($settings[$key])"
}
[IO.File]::WriteAllLines($envFile, $lines, (New-Object Text.UTF8Encoding($false)))
Write-Host 'V4.1 installed. Restart Yui Backend. VOICEVOX remains the default; choose the Irodori profiles to use these voices.'
Write-Host 'Existing Console overrides take priority over .env; update those settings in the Console if necessary.'
