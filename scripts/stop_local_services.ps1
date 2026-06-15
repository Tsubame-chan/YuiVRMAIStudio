param(
    [int]$BackendPort = 8000,
    [int]$VoicevoxPort = 50021
)

$ErrorActionPreference = "Continue"

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

Stop-ProcessOnPort -Port $BackendPort -Name "Backend"
Stop-ProcessOnPort -Port $VoicevoxPort -Name "VOICEVOX Engine"
