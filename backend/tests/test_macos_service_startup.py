"""The real launcher must expose the API while an optional voice is still starting."""
import os
from pathlib import Path
import shutil
import signal
import socket
import subprocess
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import pytest


@pytest.mark.skipif(sys.platform != "darwin", reason="macOS launcher")
def test_backend_starts_before_delayed_voice_and_survives_closed_parent_pipes(tmp_path):
    root = Path(__file__).resolve().parents[2]
    scripts = tmp_path / "scripts"
    scripts.mkdir()
    shutil.copy(root / "scripts/start_local_services_detached_macos.sh", scripts)
    (scripts / "aivis_model_sync_macos.sh").write_text("prepare_aivis_addon_runtime() { :; }\n")
    class Ready(BaseHTTPRequestHandler):
        def do_GET(self):
            self.send_response(200); self.end_headers(); self.wfile.write(b'"ready"')
        def log_message(self, *args):
            pass
    existing_voice = ThreadingHTTPServer(("127.0.0.1", 0), Ready)
    threading.Thread(target=existing_voice.serve_forever, daemon=True).start()
    def unused_port():
        with socket.socket() as s:
            s.bind(("127.0.0.1", 0)); return s.getsockname()[1]
    backend_port, voice_port = unused_port(), unused_port()
    ready_marker = tmp_path / "api-started"
    voice_marker = tmp_path / "voice-started"
    fake_python = tmp_path / "backend/.venv/bin/python"
    fake_python.parent.mkdir(parents=True)
    stub = tmp_path / "fake_backend.py"
    stub.write_text(f'''
import os, time
from pathlib import Path
from http.server import BaseHTTPRequestHandler, HTTPServer
port = int(os.environ["BACKEND_PORT"])
class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200); self.end_headers(); self.wfile.write(b'{{"status":"ok","database":"ok"}}')
    def log_message(self,*args): pass
Path({str(ready_marker)!r}).write_text(str(time.monotonic()))
HTTPServer(("127.0.0.1",port), Handler).serve_forever()
''')
    fake_python.write_text(f'#!/bin/bash\nexec "{sys.executable}" "{stub}" "$@"\n')
    fake_python.chmod(0o755)
    # This command records whether text chat was already online before waiting.
    voice_command = tmp_path / "voice.sh"
    voice_command.write_text(f'''#!/bin/bash
[[ -f '{ready_marker}' ]] || exit 12
printf ready > '{voice_marker}'
sleep 2
exec '{sys.executable}' -m http.server {voice_port} --bind 127.0.0.1
''')
    voice_command.chmod(0o755)
    ownership = tmp_path / "owned-pids.txt"
    env = {"PATH": os.environ["PATH"], "HOME": str(tmp_path),
           "BACKEND_PORT": str(backend_port), "VOICEVOX_PORT": str(existing_voice.server_port),
           "VOICEVOX_CPU_THREADS": "1", "AIVIS_ENABLE": "0", "IRODORI_ENABLE": "1",
           "IRODORI_BASE_URL": f"http://127.0.0.1:{voice_port}", "HTTP_TTS_HEALTH_ENDPOINT": "/",
           "IRODORI_START_COMMAND": str(voice_command), "YUI_BACKEND_OWNERSHIP_FILE": str(ownership)}
    launcher = subprocess.Popen(["/bin/bash", str(scripts / "start_local_services_detached_macos.sh")],
                                env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    # Same lifecycle as Unity disposing redirected streams after its startup window.
    launcher.stdout.close(); launcher.stderr.close()
    try:
        assert launcher.wait(timeout=15) == 0
        assert ready_marker.exists() and voice_marker.exists()
        assert list((tmp_path / "logs").glob("launcher-*.log"))
    finally:
        if launcher.poll() is None:
            launcher.terminate(); launcher.wait(timeout=3)
        if ownership.exists():
            for line in ownership.read_text().splitlines():
                try: os.kill(int(line.split()[1]), signal.SIGTERM)
                except ProcessLookupError: pass
        existing_voice.shutdown(); existing_voice.server_close()
