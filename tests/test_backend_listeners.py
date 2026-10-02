import importlib.util
from pathlib import Path
import socket

import pytest

spec = importlib.util.spec_from_file_location("backend_listener", Path(__file__).resolve().parents[1]/"scripts/run_backend.py")
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


def test_vpn_and_local_management_share_one_server_without_wildcard_binding():
    assert runner.listen_hosts("127.0.0.1", "100.100.20.30") == ["127.0.0.1", "100.100.20.30"]
    assert runner.listen_hosts("100.100.20.30") == ["127.0.0.1", "100.100.20.30"]
    assert runner.listen_hosts("localhost") == ["127.0.0.1"]
    assert runner.listen_hosts("0.0.0.0", "100.100.20.30") == ["0.0.0.0"]
    for invalid in ["0.0.0.0", "127.0.0.1", "224.0.0.1", "8.8.8.8"]:
        with pytest.raises(ValueError): runner.listen_hosts("127.0.0.1", invalid)


def test_partial_bind_failure_closes_all_sockets():
    with socket.socket() as available:
        available.bind(("127.0.0.1", 0))
        port = available.getsockname()[1]
    with pytest.raises(OSError): runner.bind_sockets(["127.0.0.1", "127.0.0.1"], port)
    with socket.socket() as local:
        local.bind(("127.0.0.1", port))
