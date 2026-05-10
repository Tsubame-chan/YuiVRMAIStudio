# Deployment Notes

This folder contains the first server-side deployment sketch for Yui.

## Local Docker Smoke

From the repository root:

```powershell
docker compose -f deploy/docker-compose.server.yml up --build
```

Then check:

```powershell
Invoke-RestMethod http://127.0.0.1:8000/health
Invoke-RestMethod http://127.0.0.1:50021/version
```

## Server Use

For a real server:

1. Copy `.env.example` to `.env` on the server.
2. Fill only server-side secrets in `.env`.
3. Run Docker Compose.
4. Put the backend behind HTTPS.
5. Keep VOICEVOX private to the Docker network or firewall.

Do not put OpenAI keys in the Unity app.
