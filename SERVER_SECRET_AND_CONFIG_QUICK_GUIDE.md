# Server Secret and Config Quick Guide

This is the minimum configuration required to make the server start flow and signed join-token flow work on GCP or another Linux VM provider.

## 1. Values That Must Match

These values must agree across the control-plane service, the Linux VM, and the Unity client:

- `JOIN_TICKET_SECRET`
  The same HMAC secret must be set in the orchestration service and on the game server VM.
- `TARGET_INSTANCE_ID` and `GAME_SERVER_TARGET_ID`
  These must refer to the same logical server target. On GCP, using the VM instance name is fine.
- `SERVER_PORT`
  Keep this consistent between the orchestration service, the `systemd` service, and the Unity transport configuration returned by the API.
- `MAX_PLAYERS`
  Keep this at `4` unless you intentionally change the server capacity in code.

## 2. Generate the Join-Ticket Secret

Use a long random value. Do not hardcode a short test string in production.

Windows PowerShell:

```powershell
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

Linux:

```bash
openssl rand -base64 48
```

Store the generated value somewhere secure. You will use the exact same value in both places below.

## 3. Configure the Control Plane Service

Set these environment variables on your orchestration service. If you use the included GCP sample, set them on `gcp/cloud_run/compute_engine_orchestrator.py`.

- Required:
  - `SERVER_PORT=7777`
  - `MAX_PLAYERS=4`
  - `JOIN_TICKET_SECRET=<same secret generated above>`
  - `TARGET_INSTANCE_ID=<logical target id>`
- GCP sample only:
  - `GCP_PROJECT_ID=<your project id>`
  - `GCP_ZONE=<your vm zone>`
  - `GCP_INSTANCE_NAME=<your vm instance name>`
- Recommended:
  - `JOIN_TICKET_TTL_SECONDS=90`
  - `JOIN_TICKET_SCOPE=join`
  - `SERVER_WARMUP_SECONDS=15`
  - `SERVER_ADDRESS_OVERRIDE=<static ip or dns name>`
  - `ALLOW_ANONYMOUS_JOIN_TICKETS=false`

If you enforce OIDC auth in front of the control plane, also set:

- `OIDC_REQUIRED_ISSUER`
- `OIDC_REQUIRED_AUDIENCE`
- `OIDC_REQUIRED_SCOPE`
- `OIDC_REQUIRED_GROUP`

The repo includes `gcp/cloud_run/cloud_run.env.yaml.example` as a deployment template for these values.

## 4. Configure the Linux VM Service

Run the installer on the VM with the same secret and the same target id:

```bash
./install_server_service.sh \
  --working-dir /home/ubuntu/game-server \
  --executable /home/ubuntu/game-server/2dgameproject_server \
  --port 7777 \
  --max-players 4 \
  --idle-timeout 300 \
  --join-ticket-secret '<same secret as control plane>' \
  --target-id 'game-server-primary'
```

By default this writes:

- `/etc/unity-game-server.env`

That file is read by the `systemd` service and can contain:

```text
JOIN_TICKET_SECRET=<same secret as control plane>
GAME_SERVER_TARGET_ID=game-server-primary
SERVER_IDLE_TIMEOUT_SECONDS=300
AUTO_STOP_VM_ON_IDLE=true
```

For compatibility with older builds, the installer also writes `GAME_SERVER_INSTANCE_ID` with the same value.

## 5. Configure the Unity Client

On the `ClientDebugger` component in the scene:

- Set `Enable Remote Server Orchestration = true`
- Set `Enable Join Ticket Auth = true`
- Set `Orchestration Api Base Url = https://<your-control-plane-host>`
- Leave `Authorization Bearer Token` empty in the scene for production builds

For production, assign the bearer token at runtime instead of storing a real token in the inspector. The client now supports:

- `OIDC_BEARER_TOKEN`
- `-oidcBearerToken <token>`
- `OIDC_BEARER_TOKEN_FILE`
- `-oidcBearerTokenFile <path>`
- `Authorization Bearer Token File Path`

The same `ClientDebugger` flow now also requires an explicit lobby code through `Join Lobby` or `Create Lobby` before the join token is requested.

## 6. Quick Validation

Check these before trying a full client flow:

1. The control-plane `JOIN_TICKET_SECRET` matches the value in `/etc/unity-game-server.env`.
2. `TARGET_INSTANCE_ID` matches `GAME_SERVER_TARGET_ID`.
3. Your HTTPS service exposes `POST /server/start`, `GET /server/status`, and `POST /server/join-token`.
4. Your cloud firewall allows UDP `7777` from your test clients.
5. The Unity scene has the real API base URL configured.

Useful VM commands:

```bash
sudo systemctl status unity-game-server --no-pager
sudo journalctl -u unity-game-server -f
sudo cat /etc/unity-game-server.env
```

If the client can start the VM but fails to join, the first thing to check is whether the secret or target id differs between the control plane and the VM service.