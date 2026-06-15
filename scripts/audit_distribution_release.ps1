param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$RequireBuilds
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

Test-ReleaseBlocker "unity\Assets\App\Editor\YuiAvatarSceneSetup.cs" "local-only editor scene setup script must not ship"
Test-ReleaseBlocker "unity\Assets\App\Editor\YuiAvatarSceneSetup.cs.meta" "local-only editor scene setup script metadata must not ship"
Test-ReleaseBlocker ".env" "real secrets must stay local/server-side"
Test-ReleaseBlocker "backend\data\yui.db" "local conversation database must not ship"
Test-ReleaseBlocker "backend\data\yui.db-wal" "local conversation database WAL must not ship"
Test-ReleaseBlocker "backend\data\yui.db-shm" "local conversation database SHM must not ship"
Test-ReleaseBlocker "backend\data\yui_test.db" "local test database must not ship"
Test-ReleaseBlocker "backend\data\audio" "local generated audio cache must not ship"

$privatePatternFile = Join-Path $ProjectRoot "scripts\audit_private_patterns.txt"
if (Test-Path -LiteralPath $privatePatternFile) {
    $lineNumber = 0
    foreach ($rawLine in Get-Content -LiteralPath $privatePatternFile) {
        $lineNumber += 1
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }

        $parts = $line.Split("|", 2)
        $relative = $parts[0].Trim()
        $reason = "local private asset must not ship"
        if ($parts.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($parts[1])) {
            $reason = $parts[1].Trim()
        }
        if ([string]::IsNullOrWhiteSpace($relative)) {
            Write-Host "WARNING: ignoring empty private blocker in scripts\audit_private_patterns.txt:$lineNumber" -ForegroundColor Yellow
            continue
        }
        Test-ReleaseBlocker $relative $reason
    }
}

Test-RequiredPath ".env.example" "first-time contributors need a safe environment template"
Test-RequiredPath "LICENSE" "public repositories need a project license"
Test-RequiredPath "backend\requirements.txt" "public users need backend dependencies for BYOK setup"
Test-RequiredPath "backend\main.py" "public users need the FastAPI backend entrypoint"
Test-RequiredPath "backend\app\main.py" "public users need the FastAPI backend app source"
if ($RequireBuilds) {
    Test-RequiredPath "builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\Yui VRM AI Studio.exe" "public users need the Windows app executable"
    Test-RequiredPath "builds\YuiVRMAIStudio_PublicAlpha_v0.1.0-alpha.1\YuiFilePickerHelper.exe" "Windows standalone image/VRM selection needs the file picker helper beside the app exe"
}
Test-RequiredPath "unity\Assets\UnityChan\Prefabs\unitychan.prefab" "UnityChan default avatar is the release baseline"
Test-RequiredPath "tools\YuiFilePickerHelper" "Windows file picker helper source should be available"
Test-RequiredPath "docs\PUBLIC_BYOK_SETUP.md" "public users need BYOK setup instructions"
Test-RequiredPath "docs\GITHUB_PUBLICATION.md" "release maintainers need publication instructions"

Test-ForbiddenText "unity\Assets" @(
    "Yui AIAvatar",
    "Yui Avatar",
    "demo_kikyo"
) "public Unity assets must not reference private startup avatars"

$projectSettingsRoot = Join-Path $ProjectRoot "unity\ProjectSettings"
if (Test-Path -LiteralPath $projectSettingsRoot) {
    $files = Get-ChildItem -LiteralPath $projectSettingsRoot -Recurse -File -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        if (Select-String -LiteralPath $file.FullName -Pattern "organizationId:[ \t]*[^\r\n \t]+" -Quiet) {
            $relative = [System.IO.Path]::GetRelativePath($ProjectRoot, $file.FullName)
            Write-Host "BLOCKER: $relative - public Unity project settings must not expose personal account identifiers" -ForegroundColor Red
            $script:failed += 1
        }
    }
}

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
