# GCP Compute Engine Autostart and 4-Player Guide

## Recommended Architecture

Do not embed long-lived cloud credentials inside the Unity client.

Use this flow instead:

1. The Unity client calls an HTTPS endpoint when the player clicks Connect, or automatically on launch.
2. A Cloud Run service starts or inspects a Compute Engine VM through the Compute Engine API.
3. The VM boots and `systemd` starts the Unity dedicated server automatically.
4. The client polls the orchestration API until the VM is ready, then connects to the returned address and port.
5. Before `StartClient()`, the client requests a signed join token and places it into NGO connection data.

This repo now includes the Unity-side orchestration hook, a cloud-neutral Linux service installer, and a GCP sample backend.

Supporting files:

- `install_server_service.sh`
- `server_run.sh`
- `sync_gce_instance_metadata.sh`
- `gcp/cloud_run/compute_engine_orchestrator.py`
- `gcp/cloud_run/cloud_run.env.yaml.example`
- `gcp/firebase/firebase.auth.json`
- `SERVER_SECRET_AND_CONFIG_QUICK_GUIDE.md`
- `OIDC_AND_JOIN_TOKEN_GUIDE.md`

## Step 1: Prepare the Compute Engine VM

Use one Linux VM for the dedicated server.

Recommended baseline for 4 players:

- Comfortable baseline: `e2-standard-2`
- Lower-cost test tier: `e2-medium`
- If you must stay smaller than that, increase the server warmup delay and expect slower cold starts

Firewall rules:

- UDP `7777` from your test client networks
- TCP `22` from your admin IP

Strongly recommended:

- Reserve a static external IP
- Use a stable DNS record if you do not want clients to see raw IPs

Example deployment folder:

```text
/home/<linux-user>/game-server/
  2dgameproject_server
  2dgameproject_server_Data/
  server_run.sh
  sync_gce_instance_metadata.sh
  server_start.sh
  install_server_service.sh
```

## Step 2: Install Boot-Time Server Startup

On the VM:

```bash
chmod +x server_run.sh server_start.sh install_server_service.sh sync_gce_instance_metadata.sh
./install_server_service.sh \
  --service-user <linux-user> \
  --working-dir /home/<linux-user>/game-server \
  --executable /home/<linux-user>/game-server/2dgameproject_server \
  --port 7777 \
  --max-players 4 \
  --idle-timeout 300 \
  --join-ticket-secret '<same secret as cloud service>' \
  --target-id 'game-server-primary'
```

`--service-user`, `--working-dir`, and `--executable` must all match the same real Linux account and deployment folder. If your VM uses `/home/chris/LinuxBuildFiles`, then use `--service-user chris`, `--working-dir /home/chris/LinuxBuildFiles`, and the executable path from that same folder.

What this does:

- Creates a `systemd` service named `unity-game-server`
- Starts the Unity dedicated server on boot
- Restarts it automatically if the process crashes
- Stops the dedicated server after the configured idle timeout when no players remain connected
- Powers off the VM after an idle-triggered clean exit
- Writes a protected environment file with `JOIN_TICKET_SECRET`, `GAME_SERVER_TARGET_ID`, `GAME_TRANSPORT_MODE`, and the runtime-heartbeat settings used by Relay-capable builds
- Writes a service-user runtime override file that can safely replace `GAME_SERVER_TARGET_ID` on a per-instance basis without changing the protected secret file
- Runs `sync_gce_instance_metadata.sh` before each start so a managed GCE VM can consume the `game-server-target-id` and `game-server-lobby-code` metadata attributes

If you want secure Relay transport on the VM, add `--transport-mode relay --orchestration-url https://<your-control-plane-host>` to the installer command so the headless server can publish its live Relay join code back to Cloud Run.

For disposable per-lobby templates, make sure `sync_gce_instance_metadata.sh` is deployed alongside the other startup scripts inside the VM image or instance template.

Useful commands afterward:

```bash
sudo systemctl status unity-game-server --no-pager
sudo journalctl -u unity-game-server -f
tail -f /home/ubuntu/game-server/server.log
```

## Step 3: Deploy the Cloud Run Orchestrator

Use the sample file at `gcp/cloud_run/compute_engine_orchestrator.py`.

Install the Python dependencies listed in `gcp/cloud_run/requirements.txt`.

Required environment variables:

- `GCP_PROJECT_ID`
- `GCP_ZONE`
- `GCP_INSTANCE_NAME`
- `TARGET_INSTANCE_ID`
  Use the same logical value you pass as `--target-id` on the VM. Using the instance name is fine.
- `SERVER_PORT=7777`
- `MAX_PLAYERS=4`
- `JOIN_TICKET_SECRET=<long random secret shared with the VM>`
- `GAME_TRANSPORT_MODE=direct` or `GAME_TRANSPORT_MODE=relay`

Recommended environment variables:

- `SERVER_WARMUP_SECONDS=15`
- `JOIN_TICKET_TTL_SECONDS=90`
- `JOIN_TICKET_SCOPE=join`
- `SERVER_ADDRESS_OVERRIDE=<static ip or dns name>`
- `REQUIRE_PUBLIC_ADDRESS=true`
- `ALLOW_ANONYMOUS_JOIN_TICKETS=false`
- `RELAY_CONNECTION_TYPE=dtls`
- `SERVER_RUNTIME_HEARTBEAT_TTL_SECONDS=20`
- `SERVER_REGISTRATION_TOKEN=<optional; defaults to JOIN_TICKET_SECRET when omitted>`

Per-lobby template mode only:

- `SERVER_ALLOCATION_MODE=per-lobby-template`
- `SERVER_ALLOCATION_STORE=firestore`
- `LOBBY_ALLOCATION_COLLECTION=serverLobbyAllocations`
- `MANAGED_INSTANCE_TEMPLATE=<global instance template resource>`
- `MANAGED_INSTANCE_NAME_PREFIX=lobby-`
- `MANAGED_INSTANCE_METADATA_TARGET_ID_KEY=game-server-target-id`
- `MANAGED_INSTANCE_METADATA_LOBBY_CODE_KEY=game-server-lobby-code`

If you are enforcing player auth through an upstream OIDC layer, also set:

- `OIDC_REQUIRED_ISSUER`
- `OIDC_REQUIRED_AUDIENCE`
- `OIDC_REQUIRED_SCOPE`
- `OIDC_REQUIRED_GROUP`

For Firebase Authentication email/password on this project, the working values are:

- `OIDC_REQUIRED_ISSUER=https://securetoken.google.com/soulfuljourney`
- `OIDC_REQUIRED_AUDIENCE=soulfuljourney`
- `OIDC_REQUIRED_SCOPE=`
- `OIDC_REQUIRED_GROUP=`

Use `gcp/cloud_run/cloud_run.env.yaml.example` as the starting point for your Cloud Run env vars. Copy it to `gcp/cloud_run/cloud_run.env.local.yaml` for real deploys, keep that local file untracked, and do not commit live secret values.

For `JOIN_TICKET_SECRET` and `SERVER_REGISTRATION_TOKEN`, either:

- set them in `gcp/cloud_run/cloud_run.env.local.yaml`, or
- inject them from Secret Manager at deploy time with `--update-secrets`

Minimal GCP permissions for the Cloud Run service account:

- `compute.instances.get`
- `compute.instances.start`

In practice, a small custom role or a narrowly-scoped `Compute Instance Admin` variant is easier than broad project-wide permissions.

Example deployment command:

```bash
gcloud run deploy game-server-orchestrator \
  --source ./gcp/cloud_run \
  --region us-central1 \
  --env-vars-file ./gcp/cloud_run/cloud_run.env.local.yaml \
  --allow-unauthenticated
```

Run that command from the repository root. If you `cd gcp/cloud_run` first, then use `--source .` and `--env-vars-file ./cloud_run.env.local.yaml` instead.

If you prefer Secret Manager, keep the tracked env file secret-free and add the secret bindings during deploy, for example:

```bash
gcloud run deploy game-server-orchestrator \
  --source ./gcp/cloud_run \
  --region us-central1 \
  --env-vars-file ./gcp/cloud_run/cloud_run.env.yaml \
  --update-secrets JOIN_TICKET_SECRET=join-ticket-secret:latest,SERVER_REGISTRATION_TOKEN=server-registration-token:latest \
  --allow-unauthenticated
```

If you need authenticated access only, remove `--allow-unauthenticated` and place a real auth layer in front of the service.

## Step 3a: Enable Firebase Email/Password Auth

This repo now includes a Firebase CLI auth config at `gcp/firebase/firebase.auth.json`.

Apply it with:

```bash
firebase deploy --only auth --project soulfuljourney --config gcp/firebase/firebase.auth.json
```

This enables Email/Password sign-in and provisions a default Firebase Web App for Auth when one does not already exist.

## Step 4: Expose the API Routes

Your public HTTPS service must expose:

- `POST /server/start`
- `GET /server/status`
- `POST /server/join-token`
- `POST /server/runtime`
- `OPTIONS /server/start`
- `OPTIONS /server/status`
- `OPTIONS /server/join-token`
- `OPTIONS /server/runtime`

Cloud Run can serve these routes directly. If you need quota, auth centralization, or a stable edge layer, put API Gateway or another managed gateway in front of Cloud Run.

Expected response shape:

```json
{
  "ok": true,
  "targetId": "game-server-primary",
  "instanceId": "game-server-primary",
  "instanceState": "running",
  "publicIpAddress": "34.122.10.20",
  "connectionAddress": "game.example.com",
  "port": 7777,
  "maxPlayers": 4,
  "instanceStatusOk": true,
  "serverWarmupSeconds": 15,
  "isReady": true,
  "message": "Compute Engine VM is ready for client connection."
}
```

`instanceId` is still returned for backward compatibility with the current Unity client code, but the preferred field name is `targetId`.

## Step 5: Configure the Unity Client

The current implementation is already wired into `ClientDebugger`.

Open the scene that contains `ClientDebugger` and set:

- `Enable Remote Server Orchestration = true`
- `Enable Join Ticket Auth = true`
- `Start Server When Connect Requested = true`
- `Auto Connect On Launch = true` only if you want automatic boot-and-connect behavior
- `Orchestration Api Base Url = https://<your-cloud-run-or-gateway-host>`

Optional headers:

- `Api Key Header Name`
- `Api Key Value`

For production, leave `Authorization Bearer Token` empty in the inspector and supply the bearer token at runtime through one of these sources instead:

- `OIDC_BEARER_TOKEN`
- `-oidcBearerToken <token>`
- `OIDC_BEARER_TOKEN_FILE`
- `-oidcBearerTokenFile <path>`
- `Authorization Bearer Token File Path` on `ClientDebugger`

If you want the Unity client to obtain its own guest token without a separate sign-in UI:

- Set `Enable Firebase Anonymous Auth = true`
- Set `Firebase Web Api Key = <your Firebase web API key>`
- Optionally override that key at runtime with `FIREBASE_WEB_API_KEY` or `-firebaseWebApiKey <key>`

When enabled, `ClientDebugger` will call the Firebase Auth REST API to create an anonymous session, cache the returned ID token for the running game session, and refresh it automatically before expiry.

The runtime token file can either contain the raw bearer token or a JSON object with `accessToken`, `bearerToken`, `token`, or `idToken`.

The center-screen menu now uses a dedicated-lobby flow:

- `Join Lobby | Enter Lobby Code:` text field under the player-name field
- `Join Lobby` button for existing sessions
- `Create Lobby` button that opens a modal for `Enter Custom Lobby Code:`

`Join Lobby` only checks for an already-running dedicated session. `Create Lobby` starts the VM when remote orchestration is enabled and then requests a signed `create` join ticket.

With signed join tickets enabled, the client calls `POST /server/join-token` before `StartClient()` and places the returned token into NGO connection data.

When `GAME_TRANSPORT_MODE=relay`, the dedicated server now creates the Relay allocation itself and heartbeats the live Relay join code to `POST /server/runtime`. The control plane then returns that join code from `/server/status` and `/server/join-token`, and the client switches to Relay/DTLS automatically.

Notes:

- The client now accepts either `targetId` or `instanceId` from the API response.
- The client also sends both `targetId` and `instanceId` in join-ticket requests for compatibility.
- Join-ticket requests now also send `lobbyCode` and `lobbyAction`.
- If `Enable Remote Server Orchestration` is off, the client falls back to the direct-connect path.

## Step 6: Expand the Session to 4 Players

The runtime defaults are already configured for 4 players:

- `DedicatedServerConfig.maxPlayers = 4`
- `MultiplayerGameManager.maxPlayers = 4`
- `server_run.sh` default `--max-players 4`
- `install_server_service.sh` default `--max-players 4`

Manual Unity scene work still required:

1. Open the scene with `MultiplayerGameManager`.
2. Assign `player3SpawnPoint` and `player4SpawnPoint` if you want exact spawn locations.
3. If you leave them empty, the code falls back to offset-based spawn positions.

## Step 7: Test the Full Flow

Recommended validation order:

1. Stop the Compute Engine VM.
2. Call `POST /server/start` manually and confirm the response changes from `terminated` or `provisioning` to `running`.
3. Confirm the VM boots and `unity-game-server.service` starts automatically.
4. If Relay is enabled, confirm `/server/status` begins returning `transportMode=relay` and a non-empty `relayJoinCode` after the Unity server starts.
5. Launch one client and click Connect.
6. Confirm the client shows the startup status, waits for readiness, and then connects.
7. Launch up to 4 clients and confirm all 4 can join.
8. Confirm a 5th client is rejected with a full-session message.

## GCP Notes

- If you do not reserve a static external IP, the public IP may change each time the VM is started.
- Small GCP tiers often need more warmup time than the old AWS examples assumed. `15` to `30` seconds is a safer starting point on constrained VMs.
- The GCP sample now supports a VM-reported runtime heartbeat. Direct transport can use it for a stronger ready signal, and Relay transport depends on it to publish live join codes.