"""Exercise managed-Python recovery with real Windows processes, without downloads."""
import json
import os
from pathlib import Path
import shutil
import subprocess

import pytest

pytestmark = pytest.mark.skipif(os.name != "nt", reason="Windows PowerShell launcher")
ROOT = Path(__file__).resolve().parents[1]


@pytest.fixture(scope="module")
def stub_executable(tmp_path_factory):
    root = tmp_path_factory.mktemp("irodori-process-stub")
    source = root / "stub.cs"
    source.write_text('''
using System;
using System.IO;
class Stub {
    static int Main(string[] args) {
        if (args.Length > 0 && args[0] == "-c")
            return Environment.GetEnvironmentVariable("YUI_FAKE_BAD_VERSION") == "1" ? 1 : 0;
        File.AppendAllText(Environment.GetEnvironmentVariable("YUI_FAKE_LOG"), string.Join(" ", args) + "\\n");
        if (args.Length > 1 && args[1] == "find") {
            var existing = Environment.GetEnvironmentVariable("YUI_FAKE_EXISTING");
            if (string.IsNullOrEmpty(existing)) return 1;
            Console.WriteLine(existing); return 0;
        }
        if (args.Length > 1 && args[1] == "install") {
            Console.Error.WriteLine("Synthetic junction creation failure after extraction"); return 1;
        }
        if (args.Length > 1 && args[1] == "dir") {
            Console.WriteLine(Environment.GetEnvironmentVariable("YUI_FAKE_PYTHON_ROOT")); return 0;
        }
        return 2;
    }
}
''', encoding="utf-8")
    executable = root / "stub.exe"
    env = dict(os.environ, YUI_FAKE_SOURCE=str(source), YUI_FAKE_OUTPUT=str(executable))
    subprocess.run(["powershell.exe", "-NoProfile", "-Command",
                    "Add-Type -Path $env:YUI_FAKE_SOURCE -OutputAssembly $env:YUI_FAKE_OUTPUT -OutputType ConsoleApplication"],
                   env=env, check=True, capture_output=True)
    return executable


@pytest.mark.parametrize("scenario", ["existing", "extracted-after-junction-failure", "wrong-version", "wrong-existing-version"])
def test_managed_python_resolver_handles_junction_failure(tmp_path, stub_executable, scenario):
    python_root = tmp_path / "日本語 空白"
    candidate = python_root / "cpython-3.11.17-windows-x86_64-none" / "python.exe"
    candidate.parent.mkdir(parents=True)
    shutil.copy2(stub_executable, candidate)
    source = (ROOT / "scripts/setup_irodori_v4_windows.ps1").read_text(encoding="utf-8")
    resolver = source[source.index("function Resolve-IrodoriPython {"):source.index("\nPush-Location $server")]
    harness = tmp_path / "check.ps1"
    harness.write_text(
        "$ErrorActionPreference='Stop'\n[Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)\n"
        "$uv=$env:YUI_FAKE_UV\n" + resolver +
        "\ntry { $result=Resolve-IrodoriPython; Write-Output (ConvertTo-Json -Compress @{path=$result}); exit 0 } "
        "catch { Write-Output $_.Exception.Message; exit 1 }\n", encoding="utf-8-sig")
    log = tmp_path / "uv.log"
    env = dict(os.environ, YUI_FAKE_UV=str(stub_executable), YUI_FAKE_LOG=str(log),
               YUI_FAKE_PYTHON_ROOT=str(python_root),
               YUI_FAKE_EXISTING=str(candidate) if scenario in {"existing", "wrong-existing-version"} else "",
               YUI_FAKE_BAD_VERSION="1" if scenario in {"wrong-version", "wrong-existing-version"} else "0")
    result = subprocess.run(["powershell.exe", "-NoProfile", "-File", str(harness)],
                            env=env, capture_output=True, text=True, encoding="utf-8")
    if scenario in {"wrong-version", "wrong-existing-version"}:
        assert result.returncode == 1
        assert "No Backend settings were changed" in result.stdout
    else:
        assert result.returncode == 0, result.stdout + result.stderr
        assert Path(json.loads(result.stdout.strip().splitlines()[-1])["path"]) == candidate
    calls = log.read_text().splitlines()
    if scenario == "existing":
        assert calls == ["python find --managed-python 3.11"]
    else:
        assert calls == ["python find --managed-python 3.11",
                         "python install 3.11 --no-bin --no-registry", "python dir"]
