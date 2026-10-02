#!/usr/bin/env python3
"""Serve one Backend on localhost and an explicitly selected VPN address."""
from __future__ import annotations

import argparse
import ipaddress
import json
import os
import socket
import sys


def listen_hosts(host: str, vpn_host: str = "") -> list[str]:
    host = "127.0.0.1" if host == "localhost" else str(ipaddress.ip_address(host))
    hosts = [host]
    if vpn_host:
        vpn = ipaddress.ip_address(vpn_host)
        if vpn.is_unspecified or vpn.is_multicast or vpn.is_loopback:
            raise ValueError("VPN address must be the PC's own non-loopback IP address.")
        # Tailscale uses carrier-grade NAT addresses; LAN VPNs can use RFC1918.
        if not (vpn.is_private or vpn in ipaddress.ip_network("100.64.0.0/10")):
            raise ValueError("Use a private VPN address, not a public Internet address.")
        if host not in {"0.0.0.0", "::", str(vpn)}:
            hosts.append(str(vpn))
    if host not in {"0.0.0.0", "::", "127.0.0.1", "::1"}:
        hosts.insert(0, "127.0.0.1")
    return list(dict.fromkeys(hosts))


def bind_sockets(hosts: list[str], port: int) -> list[socket.socket]:
    sockets = []
    try:
        for host in hosts:
            sock = socket.socket(socket.AF_INET6 if ":" in host else socket.AF_INET, socket.SOCK_STREAM)
            sockets.append(sock)
            if os.name == "nt":
                sock.setsockopt(socket.SOL_SOCKET, socket.SO_EXCLUSIVEADDRUSE, 1)
            else:
                sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            if sock.family == socket.AF_INET6:
                sock.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 1)
            sock.bind((host, port))
            sock.listen(128)
        return sockets
    except BaseException:
        for sock in sockets:
            sock.close()
        raise


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default=os.environ.get("BACKEND_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("BACKEND_PORT", "8000")))
    parser.add_argument("--vpn-host", default=os.environ.get("YUI_BACKEND_VPN_HOST", ""))
    args = parser.parse_args()
    if not 1 <= args.port <= 65535:
        parser.error("Port must be between 1 and 65535.")
    try:
        hosts = listen_hosts(args.host, args.vpn_host)
        sockets = bind_sockets(hosts, args.port)
    except (ValueError, OSError) as exc:
        parser.exit(1, f"Backend could not listen on the selected addresses: {exc}\n")
    os.environ["YUI_BACKEND_LISTEN_HOSTS"] = json.dumps(hosts)
    # Launchers select the Backend directory, including relocated installations.
    sys.path.insert(0, os.getcwd())
    import uvicorn
    try:
        server = uvicorn.Server(uvicorn.Config("main:app", port=args.port, proxy_headers=False, use_colors=False))
        server.run(sockets=sockets)
    finally:
        for sock in sockets:
            sock.close()


if __name__ == "__main__":
    main()
