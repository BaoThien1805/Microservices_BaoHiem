"""HTTP smoke checks with temporary processes/config; no external services required.
Run from any directory after `dotnet build TH6.slnf`.
"""
from __future__ import annotations

import base64
import hashlib
import hmac
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def free_port() -> int:
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]


def request(url: str, data: dict | None = None, token: str | None = None) -> tuple[int, str]:
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    payload = json.dumps(data).encode() if data is not None else None
    req = urllib.request.Request(url, data=payload, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=3) as response:
            return response.status, response.read().decode()
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def main() -> None:
    processes: list[subprocess.Popen] = []
    logs = []
    with tempfile.TemporaryDirectory(prefix="th6-smoke-") as directory:
        temp = Path(directory)
        ports = {name: free_port() for name in ("AuthService", "ChatService", "DashboardService", "AgentPortalApiGateway")}
        env = os.environ.copy()
        env.update({
            "ASPNETCORE_ENVIRONMENT": "Development",
            "AppSettings__Secret": secrets.token_urlsafe(48),
            "Eureka__Client__ShouldRegisterWithEureka": "false",
            "Eureka__Client__ShouldFetchRegistry": "false",
            "RabbitMqOptions__Enabled": "false",
            "SeedData__Enabled": "false",
        })
        try:
            for name, port in ports.items():
                project = ROOT / name
                content = project
                if name == "AgentPortalApiGateway":
                    content = temp / name
                    content.mkdir()
                    shutil.copy2(project / "appsettings.json", content)
                    config = json.loads((project / "ocelot.json").read_text())
                    for route in config["Routes"]:
                        service = route.pop("ServiceName")
                        route["DownstreamHostAndPorts"] = [{"Host": "localhost", "Port": ports.get(service, ports["AuthService"])}]
                    # Isolate HTTP routing/authentication from Eureka availability.
                    config["GlobalConfiguration"].pop("ServiceDiscoveryProvider", None)
                    config["GlobalConfiguration"]["UseServiceDiscovery"] = False
                    (content / "ocelot.json").write_text(json.dumps(config))
                child_env = env | {"ASPNETCORE_URLS": f"http://localhost:{port}"}
                log = open(temp / (name + ".log"), "w+", encoding="utf-8")
                logs.append(log)
                processes.append(subprocess.Popen(
                    ["dotnet", str(project / "bin/Debug/net8.0" / (name + ".dll")), "--contentRoot", str(content)],
                    cwd=content, env=child_env, stdout=log, stderr=subprocess.STDOUT,
                    creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0,
                ))
            auth = f"http://localhost:{ports['AuthService']}"
            chat = f"http://localhost:{ports['ChatService']}"
            dashboard = f"http://localhost:{ports['DashboardService']}"
            gateway = f"http://localhost:{ports['AgentPortalApiGateway']}"
            deadline = time.monotonic() + 40
            for url in [auth + "/health", chat + "/health", dashboard + "/health", gateway + "/api/User"]:
                while True:
                    try:
                        request(url)
                        break
                    except (urllib.error.URLError, TimeoutError, ConnectionError):
                        if time.monotonic() >= deadline:
                            raise RuntimeError("Service startup timed out: " + url)
                        time.sleep(0.2)
            assert request(auth + "/api/User", {"login": "missing", "password": "secret"})[0] == 401
            assert request(auth + "/api/User", {"login": "jimmy.solid", "password": "wrong"})[0] == 401
            assert request(auth + "/api/User", {"login": "", "password": ""})[0] == 400
            status, body = request(gateway + "/api/User", {"login": "jimmy.solid", "password": "secret"})
            assert status == 200, (status, body)
            token = json.loads(body)["token"]
            assert request(auth + "/api/User")[0] == 401
            status, body = request(auth + "/api/User", token=token)
            assert status == 200 and "password" not in body.lower(), (status, body)
            assert request(chat + "/agentsChat/negotiate?negotiateVersion=1", {})[0] == 401
            assert request(chat + "/agentsChat/negotiate?negotiateVersion=1", {}, token)[0] == 200
            assert request(chat + "/api/Notification", {"message": "TH6 smoke"}, token)[0] == 200
            assert request(chat + "/api/Notification", {"message": ""}, token)[0] == 400
            assert request(gateway + "/Products/TRI")[0] == 401
            assert request(gateway + "/Products/TRI", token=token)[0] == 404
            parts = token.split(".")
            parts[2] = ("A" if parts[2][0] != "A" else "B") + parts[2][1:]
            assert request(gateway + "/Products/TRI", token=".".join(parts))[0] == 401
            def encode(value: dict) -> str:
                return base64.urlsafe_b64encode(json.dumps(value).encode()).rstrip(b"=").decode()
            unsigned = encode({"alg": "HS256", "typ": "JWT"}) + "." + encode({"sub": "support", "userType": "SUPPORT", "exp": int(time.time()) + 60})
            signature = base64.urlsafe_b64encode(hmac.new(env["AppSettings__Secret"].encode(), unsigned.encode(), hashlib.sha256).digest()).rstrip(b"=").decode()
            assert request(gateway + "/Products/TRI", token=unsigned + "." + signature)[0] == 403
            assert request(gateway + "/api/Dashboard/total-sales", {})[0] == 401
            assert request(dashboard + "/api/Dashboard/total-sales", {"salesDateFrom": "2026-10-25", "salesDateTo": "2026-10-24"})[0] == 400
            print(json.dumps({"status": "passed", "checks": 16, "scope": "Auth, JWT, gateway routing, SignalR negotiation, notifications, Dashboard validation"}))
        except Exception:
            for log in logs:
                log.flush()
                log.seek(0)
                print(log.read()[-6000:])
            raise
        finally:
            for process in reversed(processes):
                process.terminate()
            for process in processes:
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
            for log in logs:
                log.close()


if __name__ == "__main__":
    main()
