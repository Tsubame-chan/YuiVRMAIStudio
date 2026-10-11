"""Opt-in acceptance against the actual relocated macOS distribution Python."""
import json
import os
from pathlib import Path
import subprocess
import sys

import pytest

from app.core.irodori_acquisition import macos_venv_command


def test_relocated_python_repairs_partial_addon_environment(tmp_path, monkeypatch):
    python = os.environ.get('YUI_PORTABLE_MAC_PYTHON')
    if sys.platform != 'darwin' or not python:
        pytest.skip('Set YUI_PORTABLE_MAC_PYTHON to a reviewed portable bundle interpreter.')
    python = str(Path(python).resolve(strict=True))
    env_dir = tmp_path / '日本語 空白' / '.venv'
    (env_dir / 'bin').mkdir(parents=True)
    # The failed relative-home path left this dangling link in the real bundle.
    (env_dir / 'bin/python').symlink_to('bin/python')
    monkeypatch.setattr('app.core.irodori_acquisition.sys.executable', python)
    env = {k:v for k,v in os.environ.items() if k not in {'PYTHONHOME','PYTHONPATH','VIRTUAL_ENV'} and not k.endswith('API_KEY')}
    subprocess.run(list(map(str, macos_venv_command(env_dir))), cwd=tmp_path, env=env,
                   check=True, capture_output=True, timeout=60)
    result = subprocess.run([str(env_dir/'bin/python'), '-c',
        'import sys,pip,json;print(json.dumps([sys.prefix,sys.base_prefix,pip.__version__]))'],
        cwd=tmp_path, env=env, check=True, capture_output=True, text=True, timeout=20)
    prefix, base, pip_version = json.loads(result.stdout)
    assert Path(prefix) == env_dir
    assert prefix != base
    assert pip_version
    assert (env_dir/'bin/python').resolve() == Path(python)
