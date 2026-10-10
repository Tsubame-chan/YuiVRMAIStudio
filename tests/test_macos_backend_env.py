import os
from pathlib import Path
import subprocess

import pytest

pytestmark = pytest.mark.skipif(os.name == "nt", reason="macOS shell launcher tests require POSIX")


@pytest.mark.parametrize("script", ["start_local_services_macos.sh", "start_local_services_detached_macos.sh"])
@pytest.mark.parametrize("value", ['"fake-key"', "'fake-key'", "fake-key\r"])
def test_launcher_env_quotes_and_process_precedence(tmp_path, script, value):
    source = (Path(__file__).resolve().parents[1] / "scripts" / script).read_text()
    loader = source[source.index("load_env_file() {"):source.index("\nload_env_file\n")]
    (tmp_path / ".env").write_text("OPENAI_API_KEY=" + value + "\n")
    env = os.environ.copy()
    env.pop("OPENAI_API_KEY", None)
    env["REPO_ROOT"] = str(tmp_path)
    command = loader + '\nload_env_file\nprintf "%s" "$OPENAI_API_KEY"\n'
    assert subprocess.check_output(["bash", "-c", command], env=env, text=True) == "fake-key"
    env["OPENAI_API_KEY"] = "process-key"
    assert subprocess.check_output(["bash", "-c", command], env=env, text=True) == "process-key"


@pytest.mark.parametrize("script", ["start_local_services_macos.sh", "start_local_services_detached_macos.sh"])
def test_launcher_never_executes_env_values(tmp_path, script):
    source = (Path(__file__).resolve().parents[1] / "scripts" / script).read_text()
    loader = source[source.index("load_env_file() {"):source.index("\nload_env_file\n")]
    value = "$(echo should-not-execute)"
    (tmp_path / ".env").write_text('YUI_ENV_TEST="' + value + '"\n')
    env = dict(os.environ, REPO_ROOT=str(tmp_path))
    env.pop("YUI_ENV_TEST", None)
    command = loader + '\nload_env_file\nprintf "%s" "$YUI_ENV_TEST"\n'
    assert subprocess.check_output(["bash", "-c", command], env=env, text=True) == value


@pytest.mark.parametrize("script", ["start_local_services_macos.sh", "start_local_services_detached_macos.sh"])
def test_external_irodori_does_not_inherit_bundled_python_home(tmp_path, script):
    source = (Path(__file__).resolve().parents[1] / "scripts" / script).read_text()
    function = source[source.index("start_irodori_if_configured() {"):source.index("\nresolve_voicevox_engine()")]
    executable = tmp_path / "fake-python"
    executable.write_text('#!/bin/sh\nprintf "%s,%s" "${PYTHONHOME-unset}" "${PYTHONPATH-unset}" > "$TTS_ENV_RESULT"\n')
    executable.chmod(0o700)
    result = tmp_path / "environment.txt"
    env = dict(os.environ, PYTHONHOME="/wrong/bundled/python", PYTHONPATH="/wrong/modules",
        IRODORI_MLX_DIR=str(tmp_path), FAKE_PYTHON=str(executable), LOG_DIR=str(tmp_path),
        RUNTIME_DIR=str(tmp_path), RUN_ID="test", TTS_ENV_RESULT=str(result), IRODORI_START_COMMAND="")
    prelude = '''
is_irodori_configured() { return 0; }
http_ok() { return 1; }
join_url() { printf unused; }
url_host() { printf 127.0.0.1; }
url_port() { printf 41080; }
resolve_irodori_mlx_python() { printf '%s' "$FAKE_PYTHON"; }
record_owned_pid() { :; }
wait_http_ok() { for attempt in $(seq 1 100); do [ -s "$TTS_ENV_RESULT" ] && return 0; sleep 0.01; done; return 1; }
'''
    subprocess.run(["bash", "-c", prelude + function + "\nstart_irodori_if_configured\n"], env=env, check=True, capture_output=True)
    assert result.read_text() == "unset,unset"
