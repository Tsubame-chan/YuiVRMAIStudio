import asyncio
import os
import socket
import subprocess
import sys
import time
from pathlib import Path

import pytest

mcp = pytest.importorskip("mcp")
from mcp import Client, StdioServerParameters
from tools.dots_probe_mcp import server


def test_synthetic_mcp_2026_protocol_and_tool_roundtrip():
    async def exercise():
        async with Client(server, mode="2026-07-28") as client:
            tools = await client.list_tools()
            assert {tool.name for tool in tools.tools} == {"yui_probe_status", "yui_probe_echo"}
            status = await client.call_tool("yui_probe_status", {})
            assert status.structured_content["personal_data"] is False
            assert status.structured_content["events"] is True
            echo = await client.call_tool("yui_probe_echo", {"marker": "probe-0123abcd"})
            assert echo.structured_content["marker"] == "probe-0123abcd"
            invalid = await client.call_tool("yui_probe_echo", {"marker": "private conversation"})
            assert invalid.is_error
    asyncio.run(exercise())


def test_synthetic_mcp_stdio_transport_roundtrip():
    async def exercise():
        script = Path(__file__).resolve().parents[1] / "tools" / "dots_probe_mcp.py"
        params = StdioServerParameters(command=sys.executable, args=[str(script)])
        async with Client(params, mode="2026-07-28") as client:
            result = await client.call_tool("yui_probe_echo", {"marker": "probe-deadbeef"})
            assert result.structured_content == {"marker": "probe-deadbeef", "received": True}
    asyncio.run(exercise())


def test_synthetic_mcp_http_transport_uses_modern_protocol(tmp_path):
    # This is the transport used by tunnel-client for the dot trial. The
    # client must exercise discovery, not only the legacy initialize path.
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]
    backend = Path(__file__).resolve().parents[1]
    process = subprocess.Popen(
        [sys.executable, "-m", "tools.dots_probe_http"], cwd=backend,
        env={**os.environ, "YUI_DOTS_PROBE_PORT": str(port),
             "YUI_DOTS_PROBE_EVENTS_DB": str(tmp_path / "probe-events.sqlite3")},
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
    )
    try:
        for _ in range(100):
            if process.poll() is not None:
                pytest.fail("Synthetic HTTP MCP server exited before listening")
            with socket.socket() as probe:
                probe.settimeout(0.1)
                if probe.connect_ex(("127.0.0.1", port)) == 0:
                    break
            time.sleep(0.05)
        else:
            pytest.fail("Synthetic HTTP MCP server did not listen")

        async def exercise():
            async with Client(f"http://127.0.0.1:{port}/mcp", mode="2026-07-28") as client:
                status = await client.call_tool("yui_probe_status", {})
                echo = await client.call_tool("yui_probe_echo", {"marker": "probe-cafebabe"})
                assert status.structured_content["personal_data"] is False
                assert echo.structured_content == {"marker": "probe-cafebabe", "received": True}

        asyncio.run(exercise())
    finally:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
