param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

Write-Host "Yui VRM AI Studio distribution release audit"
Write-Host "Project: $ProjectRoot"
Write-Host ""

$failed = 0

function Test-ReleaseBlocker {
    param(
        [string]$RelativePath,
        [string]$Reason
    )

    $path = Join-Path $ProjectRoot $RelativePath
    if (Test-Path -LiteralPath $path) {
        Write-Host "BLOCKER: $RelativePath - $Reason" -ForegroundColor Red
        $script:failed += 1
    }
}

function Test-RequiredPath {
    param(
        [string]$RelativePath,
        [string]$Reason
    )

    $path = Join-Path $ProjectRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Host "MISSING: $RelativePath - $Reason" -ForegroundColor Red
        $script:failed += 1
    }
}

function Test-ForbiddenText {
    param(
        [string]$RelativeRoot,
        [string[]]$Patterns,
        [string]$Reason
    )

    $root = Join-Path $ProjectRoot $RelativeRoot
    if (-not (Test-Path -LiteralPath $root)) {
        return
    }

    $files = Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in ".unity", ".prefab", ".asset", ".controller", ".overrideController" }

    foreach ($file in $files) {
        foreach ($pattern in $Patterns) {
            if (Select-String -LiteralPath $file.FullName -Pattern $pattern -SimpleMatch -Quiet) {
                $relative = [System.IO.Path]::GetRelativePath($ProjectRoot, $file.FullName)
                Write-Host "BLOCKER: $relative - $Reason ($pattern)" -ForegroundColor Red
                $script:failed += 1
                break
            }
        }
    }
}

function Test-SecretPattern {
    param(
        [string[]]$Patterns
    )

    $excludedDirs = @(
        "\.git\",
        "\.venv\",
        "\Library\",
        "\Temp\",
        "\Logs\",
        "\logs\",
        "\builds\",
        "\downloads\"
    )

    $files = Get-ChildItem -LiteralPath $ProjectRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $fullName = $_.FullName
            foreach ($excluded in $excludedDirs) {
                if ($fullName.IndexOf($excluded, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    return $false
                }
            }

            return $_.Extension -in ".cs", ".py", ".ps1", ".bat", ".md", ".json", ".yaml", ".yml", ".txt", ".env"
        }

    foreach ($file in $files) {
        foreach ($pattern in $Patterns) {
            if (Select-String -LiteralPath $file.FullName -Pattern $pattern -Quiet) {
                $relative = [System.IO.Path]::GetRelativePath($ProjectRoot, $file.FullName)
                Write-Host "BLOCKER: $relative - possible API key or token-like secret" -ForegroundColor Red
                $script:failed += 1
                break
            }
        }
    }
}

Test-ReleaseBlocker "unity\Assets\Kikyo" "private Kikyo avatar assets must not ship"
Test-ReleaseBlocker "unity\Assets\YuiBakedKikyo" "private baked Yui/Kikyo avatar assets must not ship"
Test-ReleaseBlocker "unity\Assets\Kikyo_Neon.unity" "private Kikyo source scene must not ship"
Test-ReleaseBlocker "unity\Assets\ZZZ_GeneratedAssets" "generated private avatar assets must not ship"
Test-ReleaseBlocker "unity\Assets\Zaelah\Kikyo_MilkMaid" "private Kikyo outfit assets must not ship"
Test-ReleaseBlocker "unity\Assets\Pampoa\JKSchoolUniform" "private Kikyo outfit assets must not ship"
Test-ReleaseBlocker "unity\Assets\Three Dots And a Dash\Kikyo_Casual" "private Kikyo outfit assets must not ship"
Test-ReleaseBlocker "unity\Assets\BERO\Open back sweater for Kikyo" "private Kikyo outfit assets must not ship"
Test-ReleaseBlocker "unity\Assets\funwari_twosideup.ver1.01" "private avatar hair/accessory assets must not ship"
Test-ReleaseBlocker "downloads\KikyoForYui.unitypackage" "private avatar package must not ship"
Test-ReleaseBlocker "downloads\YuiKikyoBakedAvatar.unitypackage" "private baked avatar package must not ship"
Test-ReleaseBlocker "downloads\yui_kikyo_bake.log" "private avatar build log must not ship"
Test-ReleaseBlocker "unity\Assets\App\Editor\YuiAvatarSceneSetup.cs" "private editor scene setup script contains local paths and private avatar workflow details"
Test-ReleaseBlocker "unity\Assets\App\Editor\YuiAvatarSceneSetup.cs.meta" "private editor scene setup script metadata must not ship"
Test-ReleaseBlocker ".env" "real secrets must stay local/server-side"
Test-ReleaseBlocker "backend\data\yui.db" "local conversation database must not ship"
Test-ReleaseBlocker "backend\data\yui.db-wal" "local conversation database WAL must not ship"
Test-ReleaseBlocker "backend\data\yui.db-shm" "local conversation database SHM must not ship"
Test-ReleaseBlocker "backend\data\yui_test.db" "local test database must not ship"
Test-ReleaseBlocker "backend\data\audio" "local generated audio cache must not ship"

Test-RequiredPath ".env.example" "first-time contributors need a safe environment template"
Test-RequiredPath "LICENSE" "public repositories need a project license"
Test-RequiredPath "backend\requirements.txt" "public users need backend dependencies for BYOK setup"
Test-RequiredPath "backend\main.py" "public users need the FastAPI backend entrypoint"
Test-RequiredPath "backend\app\main.py" "public users need the FastAPI backend app source"
Test-RequiredPath "builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe" "public users need the Windows app executable"
Test-RequiredPath "builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\YuiFilePickerHelper.exe" "Windows standalone image/VRM selection needs the file picker helper beside the app exe"
Test-RequiredPath "unity\Assets\UnityChan\Prefabs\unitychan.prefab" "UnityChan default avatar is the release baseline"
Test-RequiredPath "tools\YuiFilePickerHelper" "Windows file picker helper source should be available"
Test-RequiredPath "docs\PUBLIC_BYOK_SETUP.md" "public users need BYOK setup instructions"
Test-RequiredPath "docs\GITHUB_PUBLICATION.md" "release maintainers need publication instructions"

Test-ForbiddenText "unity\Assets" @(
    "Yui AIAvatar",
    "Yui Avatar",
    "Yui Kikyo Avatar",
    "demo_kikyo"
) "public Unity assets must not reference private Yui/Kikyo startup avatars"

Test-ForbiddenText "unity\ProjectSettings" @(
    "oga211070111",
    "gmail-com"
) "public Unity project settings must not expose personal account identifiers"

Test-SecretPattern @(
    "sk-[A-Za-z0-9_-]{20,}",
    "sk-proj-[A-Za-z0-9_-]{20,}",
    "AIza[0-9A-Za-z_-]{20,}"
)

if ($failed -gt 0) {
    Write-Host ""
    Write-Error "Distribution release audit failed with $failed issue(s)."
    exit 1
}

Write-Host "Distribution release audit passed."
