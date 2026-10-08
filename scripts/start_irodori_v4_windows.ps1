param([string]$PackRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$PackRoot = (Resolve-Path -LiteralPath $PackRoot).Path
$uv = Join-Path $PackRoot 'runtime/uv.exe'
$server = Join-Path $PackRoot 'server'
if (-not (Test-Path (Join-Path $server '.venv/Scripts/python.exe'))) {
    throw 'Run Install_Irodori_V4.bat first.'
}
$env:IRODORI_CHECKPOINT = Join-Path $PackRoot 'model/int8-weight-only/model.safetensors'
$env:IRODORI_CODEC_REPO = Join-Path $PackRoot 'codec/weights.pth'
$env:IRODORI_MODEL_PRECISION = 'bf16'
$env:IRODORI_MODEL_DEVICE = 'cuda'
$env:IRODORI_CODEC_DEVICE = 'cuda'
$env:IRODORI_VOICES_DIR = Join-Path $PackRoot 'voices'
$env:IRODORI_DEFAULT_VOICE = 'bright_natural'
$env:IRODORI_DEFAULT_NUM_STEPS = '40'
$env:IRODORI_DEFAULT_CHUNKING_ENABLED = 'true'
$env:IRODORI_DEFAULT_CHUNK_MIN_CHARS = '1'
$env:IRODORI_DEFAULT_FIRST_SENTENCE_CHUNK_MIN_CHARS = '1'
Push-Location $server
try {
    & $uv run --no-sync python -m irodori_openai_tts --host 127.0.0.1 --port 8088
    if ($LASTEXITCODE -ne 0) { throw "Irodori exited with code $LASTEXITCODE" }
} finally { Pop-Location }
