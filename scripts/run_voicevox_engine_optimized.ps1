param(
    [string]$EngineExe = "",
    [string]$EngineRunPy,
    [string]$VoicevoxDir,
    [int]$Port = 50021,
    [int]$CpuThreads = [Environment]::ProcessorCount,
    [switch]$UseGpu,
    [switch]$EnableCancellableSynthesis,
    [switch]$LoadAllModels
)

$ErrorActionPreference = "Stop"

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

$EngineExe = Resolve-VoicevoxEngineExe -RequestedPath $EngineExe

if (-not [string]::IsNullOrWhiteSpace($EngineExe) -and (Test-Path -LiteralPath $EngineExe)) {
    $command = $EngineExe
    $arguments = @(
        "--host", "127.0.0.1",
        "--port", "$Port",
        "--cpu_num_threads", "$CpuThreads",
        "--output_log_utf8"
    )
} else {
    if ([string]::IsNullOrWhiteSpace($EngineRunPy) -or -not (Test-Path -LiteralPath $EngineRunPy)) {
        throw "VOICEVOX Engine was not found. Install VOICEVOX or set VOICEVOX_ENGINE_EXE to vv-engine\run.exe."
    }

    if ([string]::IsNullOrWhiteSpace($VoicevoxDir) -or -not (Test-Path -LiteralPath $VoicevoxDir)) {
        throw "VoicevoxDir に製品版 VOICEVOX の vv-engine ディレクトリを指定してください。例: -VoicevoxDir C:\path\to\VOICEVOX\vv-engine"
    }

    $command = "python"
    $arguments = @(
        $EngineRunPy,
        "--voicevox_dir=$VoicevoxDir",
        "--host=127.0.0.1",
        "--port=$Port",
        "--cpu_num_threads=$CpuThreads",
        "--output_log_utf8"
    )
}

if ($UseGpu) {
    $arguments += "--use_gpu"
}

if ($EnableCancellableSynthesis) {
    $arguments += "--enable_cancellable_synthesis"
    $arguments += "--init_processes"
    $arguments += "1"
}

if ($LoadAllModels) {
    $arguments += "--load_all_models"
}

Write-Host "Starting VOICEVOX Engine: command=$command port=$Port cpu_threads=$CpuThreads use_gpu=$UseGpu cancellable=$EnableCancellableSynthesis load_all_models=$LoadAllModels"
Write-Host "Some GPU configurations may fail with incompatible CUDA builds. If --use_gpu fails, prefer CPU threads for now."
& $command @arguments
