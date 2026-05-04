# Server Secret and Config Quick Guide

This is the minimum configuration required to make the server start flow and signed join-token flow work on GCP or another Linux VM provider.

## 1. Values That Must Match

These values must agree across the control-plane service, the Linux VM, and the Unity client:

- `JOIN_TICKET_SECRET`
  The same HMAC secret must be set in the orchestration service and on the game server VM.
- `TARGET_INSTANCE_ID` and `GAME_SERVER_TARGET_ID`
  These must refer to the same logical server target. On GCP, using the VM instance name is fine.
- `SERVER_ALLOCATION_MODE`
  Keep this at `single-instance` unless you have also implemented per-lobby target allocation in the control plane. The default single-instance mode preserves the current `soulfuljourneyserver` flow.
- `SERVER_ALLOCATION_STORE`
  Use `firestore` if you want lobby-to-target allocation records to survive across Cloud Run instances. `memory` is only appropriate for local or temporary development.
- `MANAGED_INSTANCE_TEMPLATE`
  Required only for `per-lobby-template`. This should point to the Compute Engine instance template used for disposable lobby VMs.
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
  - `SERVER_ALLOCATION_MODE=single-instance`
  - `SERVER_ALLOCATION_STORE=firestore`
  - `GAME_TRANSPORT_MODE=direct` or `GAME_TRANSPORT_MODE=relay`
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
  - `RELAY_CONNECTION_TYPE=dtls`
  - `LOBBY_ALLOCATION_COLLECTION=serverLobbyAllocations`
  - `MANAGED_INSTANCE_NAME_PREFIX=lobby-`
  - `MANAGED_INSTANCE_DESCRIPTION_PREFIX=Ephemeral dedicated lobby server`
  - `MANAGED_INSTANCE_METADATA_TARGET_ID_KEY=game-server-target-id`
  - `MANAGED_INSTANCE_METADATA_LOBBY_CODE_KEY=game-server-lobby-code`
  - `SERVER_RUNTIME_HEARTBEAT_TTL_SECONDS=20`
  - `SERVER_REGISTRATION_TOKEN=<optional; defaults to JOIN_TICKET_SECRET>`

If you enforce OIDC auth in front of the control plane, also set:

- `OIDC_REQUIRED_ISSUER`
- `OIDC_REQUIRED_AUDIENCE`
- `OIDC_REQUIRED_SCOPE`
- `OIDC_REQUIRED_GROUP`

The repo includes `gcp/cloud_run/cloud_run.env.yaml.example` as a deployment template for these values.

Keep live secrets out of tracked files. For local deployments, copy that template to `gcp/cloud_run/cloud_run.env.local.yaml` and keep the real `JOIN_TICKET_SECRET` and `SERVER_REGISTRATION_TOKEN` there. If you want the repo to stay publish-safe, inject those two values from Secret Manager during `gcloud run deploy --update-secrets` instead of storing them in `cloud_run.env.yaml`.

The new `SERVER_ALLOCATION_MODE` switch is intended to make future per-lobby VM allocation possible without undoing the existing single-instance setup. Leave it at `single-instance` until you are ready to test disposable lobby VMs.

If you are preparing for per-lobby allocation, the control plane can now persist lobby-to-target mappings in Firestore. That storage is keyed by `LOBBY_ALLOCATION_COLLECTION` and is designed to survive Cloud Run instance restarts.

The per-lobby allocator now also knows how to create a managed VM from `MANAGED_INSTANCE_TEMPLATE`. That template should point at a disposable server image or instance template, not `soulfuljourneyserver` itself. The metadata keys let a future VM boot hook rewrite the per-instance target id and lobby code without touching the stable baseline VM.

## 4. Configure the Linux VM Service

Run the installer on the VM with the same secret and the same target id:

```bash
./install_server_service.sh \
  --working-dir /home/ubuntu/game-server \
  --executable /home/ubuntu/game-server/2dgameproject_server \
  --port 7777 \
  --max-players 4 \
  --idle-timeout 300 \
  --transport-mode relay \
  --orchestration-url https://<your-control-plane-host> \
  --join-ticket-secret '<same secret as control plane>' \
  --target-id 'game-server-primary'
```

By default this writes:

- `/etc/unity-game-server.env`
- `/home/<service-user>/.../.unity-game-server-runtime.env` or the `--runtime-env-file` path you supply

That file is read by the `systemd` service and can contain:

```text
JOIN_TICKET_SECRET=<same secret as control plane>
GAME_SERVER_TARGET_ID=game-server-primary
GAME_TRANSPORT_MODE=relay
GAME_SERVER_ORCHESTRATION_URL=https://<your-control-plane-host>
GAME_SERVER_RUNTIME_STATUS_ENDPOINT=/server/runtime
SERVER_IDLE_TIMEOUT_SECONDS=300
AUTO_STOP_VM_ON_IDLE=true
```

The runtime override file is intentionally separate from `/etc/unity-game-server.env`. It is safe for the boot-time GCE metadata hook to rewrite only `GAME_SERVER_TARGET_ID`, `GAME_SERVER_INSTANCE_ID`, and the allocated lobby code there without modifying the protected secret file.

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
3. Your HTTPS service exposes `POST /server/start`, `GET /server/status`, `POST /server/join-token`, and `POST /server/runtime`.
4. Your cloud firewall allows UDP `7777` from your test clients.
5. The Unity scene has the real API base URL configured.

Useful VM commands:

```bash
sudo systemctl status unity-game-server --no-pager
sudo journalctl -u unity-game-server -f
sudo cat /etc/unity-game-server.env
sudo cat /home/<service-user>/.../.unity-game-server-runtime.env
```

If Relay is enabled and the client can start the VM but fails to join, first check whether `/server/status` is returning a non-empty `relayJoinCode`. If not, verify that the secret, target id, orchestration URL, and transport mode all match between the control plane and the VM service.