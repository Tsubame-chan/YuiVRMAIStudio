"""Synthetic-only MCP 2.0 probe for a private DOTS connection.

This process has no access to Yui's database, device tokens, files, or
production Backend. Do not replace it with a personal-data tool until OAuth,
grants, event delivery and per-character scope have been accepted.
"""
from __future__ import annotations

import re

from mcp.server import MCPServer
from mcp.types import ToolAnnotations
from pydantic import BaseModel


class ProbeStatus(BaseModel):
    probe: bool
    protocol: str
    personal_data: bool
    writes: bool
    events: bool


class ProbeEcho(BaseModel):
    marker: str
    received: bool


server = MCPServer(
    "Yui synthetic connection probe",
    instructions="Only synthetic probe markers are available. No personal data or real Yui actions are exposed.",
)


@server.tool(annotations=ToolAnnotations(readOnlyHint=True, openWorldHint=False), structured_output=True)
def yui_probe_status() -> ProbeStatus:
    """Report the synthetic probe's fixed capabilities without reading user data."""
    return ProbeStatus(probe=True, protocol="2026-07-28", personal_data=False,
                       writes=False, events=True)


@server.tool(annotations=ToolAnnotations(readOnlyHint=True, openWorldHint=False), structured_output=True)
def yui_probe_echo(marker: str) -> ProbeEcho:
    """Echo a short synthetic marker to verify a tool request and response."""
    if not re.fullmatch(r"probe-[a-f0-9]{8}", marker):
        raise ValueError("Only synthetic probe markers are accepted")
    return ProbeEcho(marker=marker, received=True)


if __name__ == "__main__":
    server.run("stdio")
