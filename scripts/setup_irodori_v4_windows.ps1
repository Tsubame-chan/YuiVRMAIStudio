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
function Resolve-IrodoriPython {
    # Native discovery/install errors are checked below; an incomplete junction
    # must not terminate PowerShell 5.1 before the executable fallback runs.
    $ErrorActionPreference = 'Continue'
    $candidate = & $uv python find --managed-python 3.11 2>$null
    if ($LASTEXITCODE -eq 0 -and $candidate) { return [string]$candidate }
    & $uv python install 3.11 --no-bin --no-registry | Out-Host
    # uv can finish extracting Python but fail to create its Windows minor-version
    # junction. Use the verified executable, without changing any global links.
    $pythonRoot = & $uv python dir
    if ($LASTEXITCODE -ne 0) { throw 'Could not locate the managed Python installation.' }
    foreach ($directory in @(Get-ChildItem -LiteralPath ([string]$pythonRoot) -Directory -Filter 'cpython-3.11.*-windows-x86_64-none' | Sort-Object Name -Descending)) {
        $candidate = Join-Path $directory.FullName 'python.exe'
        if (-not (Test-Path -LiteralPath $candidate)) { continue }
        & $candidate -c 'import sys; sys.exit(0 if sys.version_info[:2] == (3, 11) else 1)' 2>$null
        if ($LASTEXITCODE -eq 0) { return $candidate }
    }
    throw 'Python 3.11 installation failed. No Backend settings were changed.'
}
Push-Location $server
try {
    # The pinned upstream runtime requires sentencepiece 0.1.99. Its Windows
    # wheel supports Python 3.11, not 3.12; avoid a failing native source build.
    $python = Resolve-IrodoriPython
    & $uv sync --frozen --extra cu128 --python $python
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
