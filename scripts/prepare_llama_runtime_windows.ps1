param([ValidateSet('CPU','Vulkan','CUDA')][string]$Backend = 'CPU')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$revision = '14310190cd0a0c831fe949ac8cbedd8abdc90695' # official b11617
$sourcePath = Join-Path $projectRoot 'builds/llama-runtime-windows/source'
$buildPath = Join-Path $projectRoot "builds/llama-runtime-windows/$Backend"
$pluginPath = Join-Path $projectRoot 'unity/Assets/Plugins/x86_64'
function Run-Native([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed ($LASTEXITCODE)" }
}
if (!(Test-Path "$sourcePath/.git")) {
    Run-Native git @('clone','--filter=blob:none','--no-checkout','https://github.com/ggml-org/llama.cpp.git',$sourcePath)
} elseif ((& git -C $sourcePath status --porcelain)) {
    throw 'Keep the generated llama.cpp checkout clean before preparing a runtime'
}
Run-Native git @('-C',$sourcePath,'fetch','--depth','1','origin',$revision)
Run-Native git @('-C',$sourcePath,'checkout','--detach',$revision)
$resolved = (& git -C $sourcePath rev-parse HEAD).Trim()
if ($resolved -ne $revision) { throw 'Unexpected llama.cpp source revision' }
$vulkan = if ($Backend -eq 'Vulkan') { 'ON' } else { 'OFF' }
$cuda = if ($Backend -eq 'CUDA') { 'ON' } else { 'OFF' }
Run-Native cmake @('-S',"$projectRoot/tools/llama-runtime/native",'-B',$buildPath,'-A','x64',"-DLLAMA_SOURCE_DIR=$sourcePath","-DGGML_VULKAN=$vulkan","-DGGML_CUDA=$cuda",'-DGGML_OPENMP=OFF')
Run-Native cmake @('--build',$buildPath,'--config','Release','--parallel','4')
$libraries = @(Get-ChildItem $buildPath -Recurse -Filter '*.dll' | Where-Object { $_.Directory.Name -eq 'Release' -and $_.Name -match '^(YuiLlama|llama|ggml(?:-[a-z0-9_-]+)?)\.dll$' })
foreach ($required in @('YuiLlama.dll','llama.dll','ggml.dll','ggml-base.dll','ggml-cpu.dll')) {
    if (!($libraries | Where-Object Name -eq $required)) { throw "Missing runtime library: $required" }
}
New-Item -ItemType Directory -Force $pluginPath | Out-Null
foreach ($library in $libraries) {
    $destination = Join-Path $pluginPath $library.Name
    Copy-Item $library.FullName $destination -Force
    if (!(Test-Path "$destination.meta")) {
        $guid = [guid]::NewGuid().ToString('N')
        @"
fileFormatVersion: 2
guid: $guid
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  isPreloaded: 0
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        CPU: x86_64
        OS: Windows
  - first:
      Standalone: Win64
    second:
      enabled: 1
      settings:
        CPU: x86_64
"@ | Set-Content "$destination.meta" -Encoding utf8
    }
}
$manifest = @{ revision=$revision; tag='b11617'; backend=$Backend; verified_on_windows=$false; libraries=@($libraries | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower()} }) }
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $pluginPath 'yui-llama-runtime.json') -Encoding utf8
Write-Host 'Prepared Windows runtime. Test native inference, cancellation, model switching and dependencies on Windows before distribution.'
