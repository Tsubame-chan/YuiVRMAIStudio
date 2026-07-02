param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch]$RequireBuilds,
    [ValidateSet("windows", "macos", "all")]
    [string]$Platform = "windows"
)

$ErrorActionPreference = "Stop"
$organizationIdField = "organization" + "Id"
$organizationIdPattern = $organizationIdField + ":[ \t]*[^\r\n \t]+"
$privateHashRules = @(
    @{ Length = 19; Hash = "d95848222a750995a3485b808064371c9779f01bcd803ff26531c7b0464dba6c" },
    @{ Length = 23; Hash = "6dcdb6e23d3ed173a43584f9b36922a27d97cc73df78a475ef11107a7e8e55f5" },
    @{ Length = 27; Hash = "66b983055669aae45058c0aa3fa80586525597dac5c9e7cbf5e47f3ee5d836f1" },
    @{ Length = 31; Hash = "ac70a6ba9d67ef18274d439db24d69821c436d66aecc021ed6d07a3fc248497c" },
    @{ Length = 29; Hash = "bbcd23d48dc5bc2f3dac1873853c11e94956817e68f1540af799202165d73687" },
    @{ Length = 34; Hash = "41022c0f04733657da9064590ed5a4c4668308b7578b38a877c26c5232c89817" },
    @{ Length = 33; Hash = "6fcc4aaf908ec51b3cb84a9fe205bbb87ab09e2916ad4ff0216b661a09c61dee" },
    @{ Length = 37; Hash = "22f1fd0a0b7e798bde34d64c208a73d93679b078a230b75cd8969bd88da1578a" },
    @{ Length = 35; Hash = "e614380e761f2d6222fbefd3ced96506aea7a1567f6067ea2902ac1ba6b2789c" },
    @{ Length = 24; Hash = "0f0f1373e7e54612db4fc5534ab795cf0c6b2c57207a6d5ed4a572aea931b464" },
    @{ Length = 39; Hash = "6d8580c7d924427d3f27a1d802027e78b3ca4c558f6b5d9e40d59903e3fe4718" },
    @{ Length = 36; Hash = "cd538432301f7256f68396c64e8a70e2b5511b1cb6ec229830b5673af5da52fb" },
    @{ Length = 24; Hash = "cbc0137d25d671277434c97dbfee806503f52ae41c81011dc0ee03979ee2fe1f" },
    @{ Length = 40; Hash = "b2f73d8e37cc8114fd7ecded3f819db330176c96bc8ed118dae1e91c91ac0ed6" },
    @{ Length = 48; Hash = "c9270edcedfdb85c8d5bf7586865338d7d4acf1c1823bf434625d067451120cf" },
    @{ Length = 39; Hash = "1f827faa5b87bed4f53c6e74cc036b7c28ca4eb4c9116de4b918e72e7fb43171" },
    @{ Length = 52; Hash = "3ea5cf741698cd926239f7c18b7973463da65ea3cf2cc71c80fa54228bcdc0d0" },
    @{ Length = 46; Hash = "b65c21232c131b75dfc81d67a603f187a46fa8b5d1906f901dabb6123f9ec9bb" },
    @{ Length = 22; Hash = "68ee7b640928cb5e9759fb5227fe96978620b9d306e859113098c9c909f4e432" },
    @{ Length = 50; Hash = "7db9e56ddf9f927d3a58329f86b4dd4e68716c60fcfaaadda9dc6d1c1ef48ff1" },
    @{ Length = 39; Hash = "8661aad5531fd4edf6152b442dd98122155b7a1793fd4897683b21156d27268e" },
    @{ Length = 43; Hash = "3739cde997e7110bd308839442e7966d562322df98ff5539a6bcaf3f5ddd8d4a" },
    @{ Length = 19; Hash = "7d5df3d4b3074d5c7b87e470309beef48980d9ad5c6971dcd0d51af6b2cf6e58" },
    @{ Length = 23; Hash = "fe496011fb358bcc42bb75547de9ed64087b7cbb10f10b67470f268fbc068b3c" },
    @{ Length = 24; Hash = "0293f9ba1af220bf1eb69e09f65a7a70cc8c3cf5043989dec7ccc87722a2c049" },
    @{ Length = 28; Hash = "39ca684961067043d07489b8f03de6b8dc9415630bb0c1ab67857bd2a6a6ff45" },
    @{ Length = 52; Hash = "48794c39732c9d1186d95b0e794c1de008047dcf3789d32c73f680f422cd57d4" },
    @{ Length = 57; Hash = "9710151c6643ef1240ec5282f11471815276eed23b52e83acd40a90c6caf9504" },
    @{ Length = 57; Hash = "b9c837032efd88ae0d08386d660c746dea4610cb9c8e73d8ba5d8d8203faf7bb" },
    @{ Length = 62; Hash = "dcc8455c5b77b88e5d3b86847b42e50392c9ded4fe21f70d8abc6196881de675" },
    @{ Length = 43; Hash = "333ef3c82d02a744ea00de90d8cd36a57a6b3eaf4b88802b5d6bfbc9abf9c40c" },
    @{ Length = 48; Hash = "8a84e02151c3ef8bac441536bef266e47f195c4801fc21e005f1aac5055a1530" },
    @{ Length = 5; Hash = "2b97a7913b83568a8d2a38be3a93261589fb5b82d0b3388e34d8cf94d2f1c1b0" },
    @{ Length = 13; Hash = "6a55eb4e250400eceea24188f64f15f005228ee24be16e26f877b415a6b3eafd" },
    @{ Length = 8; Hash = "37c6f3d3a11e196d2dec8937799d419191b610dbfb6b0f136e388c5615ef24da" },
    @{ Length = 6; Hash = "8cfe859bbf65bec24c1de9d48df82fc7d78a4657162eb765ecea78cb4d032154" },
    @{ Length = 6; Hash = "c4b54c6506f05565d71f6bba04fc3e43685ba05e1a0ea16ee4b077f785e0c8d7" },
    @{ Length = 15; Hash = "50f1a38f5e3ef71b369b33619ca4b555467611b27b01c77728a1c480a6be5cf6" },
    @{ Length = 21; Hash = "904edd0d0f476076037f4acf964ffc281a25a882e1d8ffcc933aa05485ea9a9d" },
    @{ Length = 4; Hash = "d3b842089a9abe6a3bcbdda47267f2e6da81c9720b486a545678bf0c16eb2af9" },
    @{ Length = 17; Hash = "bffb81551a240086fb8afbb39bf3a598887393c63de2a916a509dea1a9798264" },
    @{ Length = 5; Hash = "c4471e49f778f22b2a4b4dd96c463a3a2dd9e62462cd7ba062d23ed45d257de3" },
    @{ Length = 10; Hash = "c4c0168cb513cf68e310fd6ad0b01cf35e8f16ace6fbb235f295d13245c9f042" },
    @{ Length = 14; Hash = "deaf6e4915a0fe1e6f6286d70ee577f8478509e1cd4b72ce9ba52ca14e8b8395" },
    @{ Length = 12; Hash = "0563ae94557b154c65358347ad2df7b55d1eea0a3d8251ff66691249bb393198" },
    @{ Length = 10; Hash = "a10e6d3adb8d4bb96ce2a043b16e241af6774ebf5f57414a3f761d583ad4eced" },
    @{ Length = 12; Hash = "4598fac51f97c1c97660ed6e3be7530eada7ae04fb87c9aeb15565c620750dcd" },
    @{ Length = 15; Hash = "d299c82b9008b9d7e314b113a15f986ccd1c9f99b23779ca503b33e2fcde8172" }
)

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

function Test-PrivateHash {
    param([string]$Value)

    $normalized = $Value.Replace("\", "/").ToLowerInvariant()
    $candidates = [System.Collections.Generic.HashSet[string]]::new()
    if ($normalized.Length -le 256) {
        [void]$candidates.Add($normalized.Trim())
    }
    foreach ($line in ($normalized -split "[\r\n]+")) {
        $stripped = $line.Trim().Trim('"', ',', ';')
        if (-not [string]::IsNullOrWhiteSpace($stripped)) {
            [void]$candidates.Add($stripped)
        }
        foreach ($match in [System.Text.RegularExpressions.Regex]::Matches($stripped, "[\w./ \-\u3040-\u30ff\u3400-\u9fff]+")) {
            $token = $match.Value.Trim().Trim('"', ',', ';')
            if ([string]::IsNullOrWhiteSpace($token)) {
                continue
            }
            [void]$candidates.Add($token)
            foreach ($part in $token.Split("/")) {
                if (-not [string]::IsNullOrWhiteSpace($part)) {
                    [void]$candidates.Add($part)
                }
            }
        }
    }

    foreach ($candidate in $candidates) {
        foreach ($rule in $privateHashRules) {
            if ($candidate.Length -ne [int]$rule.Length) {
                continue
            }
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($candidate)
                $hashBytes = $sha.ComputeHash($bytes)
                $hash = [System.BitConverter]::ToString($hashBytes).Replace("-", "").ToLowerInvariant()
            }
            finally {
                $sha.Dispose()
            }
            if ($hash -eq $rule.Hash) {
                return $true
            }
        }
    }
    return $false
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
    $excludedFiles = @(
        "scripts\audit_distribution_release.py",
        "scripts\audit_distribution_release.ps1",
        "scripts\publication_guard.py"
    )

    $files = Get-ChildItem -LiteralPath $ProjectRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $fullName = $_.FullName
            $relativeName = [System.IO.Path]::GetRelativePath($ProjectRoot, $fullName)
            foreach ($excludedFile in $excludedFiles) {
                if ($relativeName.Equals($excludedFile, [System.StringComparison]::OrdinalIgnoreCase)) {
                    return $false
                }
            }
            foreach ($excluded in $excludedDirs) {
                if ($fullName.IndexOf($excluded, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    return $false
                }
            }

            return $_.Extension -in ".cs", ".py", ".ps1", ".bat", ".md", ".json", ".yaml", ".yml", ".txt", ".env"
        }

    foreach ($file in $files) {
        $relativeForHash = [System.IO.Path]::GetRelativePath($ProjectRoot, $file.FullName)
        if ((Test-PrivateHash $relativeForHash) -or (Test-PrivateHash (Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue))) {
            Write-Host "BLOCKER: $relativeForHash - local-only avatar identifier must not ship" -ForegroundColor Red
            $script:failed += 1
            continue
        }
        foreach ($pattern in $Patterns) {
            if (Select-String -LiteralPath $file.FullName -Pattern $pattern -Quiet) {
                Write-Host "BLOCKER: $relativeForHash - possible API key or token-like secret" -ForegroundColor Red
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
Test-ReleaseBlocker "scripts\audit_private_patterns.txt" "local private path list must not ship"
Test-ReleaseBlocker "scripts\publication_guard.local.txt" "local publication guard notes must not ship"
Test-ReleaseBlocker "scripts\public_templates" "local public-scene templates must not ship"
Test-ReleaseBlocker "scripts\cleanup_local_artifacts_macos.sh" "local cleanup script with owner paths must not ship"
Test-ReleaseBlocker "scripts\cleanup_local_artifacts.ps1" "local cleanup script with owner paths must not ship"

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
    if ($Platform -eq "windows" -or $Platform -eq "all") {
        $windowsExpanded =
            (Test-Path -LiteralPath (Join-Path $ProjectRoot "builds\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1\Yui VRM AI Studio.exe")) -and
            (Test-Path -LiteralPath (Join-Path $ProjectRoot "builds\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1\YuiFilePickerHelper.exe"))
        $windowsArchive = Test-Path -LiteralPath (Join-Path $ProjectRoot "builds\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1_windows.zip")
        if (-not ($windowsExpanded -or $windowsArchive)) {
            Test-RequiredPath "builds\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1\Yui VRM AI Studio.exe" "public users need the Windows app executable"
            Test-RequiredPath "builds\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1\YuiFilePickerHelper.exe" "Windows standalone image/VRM selection needs the file picker helper beside the app exe"
            Test-RequiredPath "builds\YuiVRMAIStudio_WindowsPublicBeta_v0.2.0-beta.1_windows.zip" "public users need the downloadable Windows public beta archive"
        }
    }
    if ($Platform -eq "macos" -or $Platform -eq "all") {
        $macExpanded = Test-Path -LiteralPath (Join-Path $ProjectRoot "builds\YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1\Yui VRM AI Studio.app")
        $macArchive = Test-Path -LiteralPath (Join-Path $ProjectRoot "builds\YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1_macos.zip")
        if (-not ($macExpanded -or $macArchive)) {
            Test-RequiredPath "builds\YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1\Yui VRM AI Studio.app" "public users need the macOS app bundle"
            Test-RequiredPath "builds\YuiVRMAIStudio_MacOSPublicBeta_v0.2.0-beta.1_macos.zip" "public users need the downloadable macOS public beta archive"
        }
    }
}
Test-RequiredPath "unity\Assets\UnityChan\Prefabs\unitychan.prefab" "UnityChan default avatar is the release baseline"
Test-RequiredPath "tools\YuiFilePickerHelper" "Windows file picker helper source should be available"
Test-RequiredPath "docs\SETUP_GUIDE.md" "Windows users need a first-run and backend setup guide"
Test-RequiredPath "docs\MAC_PUBLIC_BETA.md" "macOS users need a first-run and backend setup guide"
Test-RequiredPath "docs\LOCAL_AI_ASSETS.md" "source builders need local AI/TTS asset instructions"

$unityAssetsRoot = Join-Path $ProjectRoot "unity\Assets"
if (Test-Path -LiteralPath $unityAssetsRoot) {
    $unityTextFiles = Get-ChildItem -LiteralPath $unityAssetsRoot -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -in ".unity", ".prefab", ".asset", ".controller", ".overrideController" }
    foreach ($file in $unityTextFiles) {
        $relative = [System.IO.Path]::GetRelativePath($ProjectRoot, $file.FullName)
        $text = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction SilentlyContinue
        if ((Test-PrivateHash $relative) -or (Test-PrivateHash $text)) {
            Write-Host "BLOCKER: $relative - public Unity assets must not reference local-only startup avatars" -ForegroundColor Red
            $script:failed += 1
        }
    }
}

$projectSettingsRoot = Join-Path $ProjectRoot "unity\ProjectSettings"
if (Test-Path -LiteralPath $projectSettingsRoot) {
    $files = Get-ChildItem -LiteralPath $projectSettingsRoot -Recurse -File -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        if (Select-String -LiteralPath $file.FullName -Pattern $organizationIdPattern -Quiet) {
            $relative = [System.IO.Path]::GetRelativePath($ProjectRoot, $file.FullName)
            Write-Host "BLOCKER: $relative - public Unity project settings must not expose private account identifiers" -ForegroundColor Red
            $script:failed += 1
        }
    }
}

Test-SecretPattern @(
    "sk-[A-Za-z0-9_-]{20,}",
    "sk-proj-[A-Za-z0-9_-]{20,}",
    "AIza[0-9A-Za-z_-]{20,}",
    "YUI_PROFILE_PERSONAL",
    "PersonalAlpha",
    "Yui VRM AI Studio Personal",
    "jp\.tsubamechan\.yuivrm\.personal"
)

if ($failed -gt 0) {
    Write-Host ""
    Write-Error "Distribution release audit failed with $failed issue(s)."
    exit 1
}

Write-Host "Distribution release audit passed."
