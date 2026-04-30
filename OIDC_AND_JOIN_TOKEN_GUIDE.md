# OIDC and Join-Token Guide

## Goal

Use an OIDC identity provider to authenticate the player for the control plane, then mint a short-lived signed join token that the Unity dedicated server validates during NGO connection approval.

This is the security split:

1. OIDC token: proves the player is allowed to call your backend.
2. Join token: proves the backend admitted this player to this specific game-server target.

The game server should validate the join token, not the OIDC token directly.

## Recommended Control Plane Layout

Use this route layout behind your HTTPS service:

- `POST /server/start`
- `GET /server/status`
- `POST /server/join-token`

Recommended protection:

- An upstream OIDC-aware gateway, proxy, or auth layer
- Request throttling and logs on every route
- Short join-token TTLs

On GCP, common choices are:

- Google Identity Platform or Firebase Auth for player identity
- Cloud Run for the orchestration service
- API Gateway, IAP, or another trusted edge layer if you want central auth enforcement before requests reach the service

## OIDC Claims To Validate In The Control Plane

Your gateway or backend should validate these claims before minting a join token:

- `iss`
  Must match your expected issuer.
- `exp`
  Must be in the future.
- `nbf`
  Must not be in the future when present.
- `aud`, `azp`, or `client_id`
  Must match your expected client application.
- `sub`
  Required. This becomes the authoritative player identity in the join token.
- `scope` or `scp`
  Recommended when your provider supports scopes.
- `groups`
  Optional. Use this if only certain users should be allowed to access multiplayer.

In this repo, the control-plane env vars for those checks are:

- `OIDC_REQUIRED_ISSUER`
- `OIDC_REQUIRED_AUDIENCE`
- `OIDC_REQUIRED_SCOPE`
- `OIDC_REQUIRED_GROUP`

## Claims The Game Server Should Validate

The Unity server validates these join-token claims during `ConnectionApprovalCallback`:

- `sub`
  The authenticated player identity.
- `pn`
  The approved player name.
- `exp`
  Short-lived expiry.
- `iid`
  The expected game-server target id.
- `lc`
  The approved lobby code for this dedicated session.
- `la`
  The approved lobby action, either `create` or `join`.
- `scp`
  Required scope string, recommended value: `join`.
- `nonce`
  Uniqueness marker for each issued token.

## Least-Friction Setup For This Repo

### 1. Identity Provider

Create an OIDC-capable login path for the Unity client.

For GCP, the practical options are:

- Google Identity Platform
- Firebase Auth
- Another provider that can produce OIDC-compatible bearer tokens your control plane can validate

### 2. HTTPS Edge

Protect these routes:

- `POST /server/start`
- `GET /server/status`
- `POST /server/join-token`

Leave `OPTIONS` unauthenticated for CORS.

### 3. Control-Plane Environment Variables

Set these on your orchestration service:

- `TARGET_INSTANCE_ID`
- `SERVER_PORT=7777`
- `MAX_PLAYERS=4`
- `JOIN_TICKET_SECRET=<long random secret>`
- `JOIN_TICKET_TTL_SECONDS=90`
- `JOIN_TICKET_SCOPE=join`
- `ALLOW_ANONYMOUS_JOIN_TICKETS=false`

If you want OIDC enforcement in the service itself, also set:

- `OIDC_REQUIRED_ISSUER`
- `OIDC_REQUIRED_AUDIENCE`
- `OIDC_REQUIRED_SCOPE`
- `OIDC_REQUIRED_GROUP`

The repo now includes `gcp/cloud_run/cloud_run.env.yaml.example` as a Cloud Run env-vars template.

For Firebase Authentication with email/password on the `soulfuljourney` project, use:

- `OIDC_REQUIRED_ISSUER=https://securetoken.google.com/soulfuljourney`
- `OIDC_REQUIRED_AUDIENCE=soulfuljourney`
- `OIDC_REQUIRED_SCOPE=`
- `OIDC_REQUIRED_GROUP=`

Firebase ID tokens do not carry a custom `scope` claim for this flow, so `OIDC_REQUIRED_SCOPE` should stay empty.

### 4. Linux VM Service Installation

Install the Unity server service with the same join-token secret and target id:

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

This exposes `JOIN_TICKET_SECRET` and `GAME_SERVER_TARGET_ID` to the Unity server process.

### 5. Unity Client

Set these on the existing `ClientDebugger` component:

- `Enable Remote Server Orchestration = true`
- `Enable Join Ticket Auth = true`
- `Orchestration Api Base Url = https://<your-control-plane-host>`

For production, do not store a real OIDC token in the inspector. The client now resolves its bearer token at runtime from these sources, in priority order:

- `-oidcBearerToken <token>`
- `OIDC_BEARER_TOKEN`
- `-oidcBearerTokenFile <path>`
- `OIDC_BEARER_TOKEN_FILE`
- `Authorization Bearer Token File Path`
- `Authorization Bearer Token` as the last-resort inspector fallback

The runtime token file can contain either the raw bearer token or JSON with one of these fields: `accessToken`, `bearerToken`, `token`, or `idToken`.

If you want guest access without a separate login prompt, the client can now mint Firebase anonymous ID tokens directly through the Firebase Auth REST API:

- Enable `Firebase Anonymous Auth` on `ClientDebugger`
- Set `Firebase Web Api Key` to your Firebase Web API key
- Or provide the key at runtime with `FIREBASE_WEB_API_KEY` or `-firebaseWebApiKey <key>`

When no explicit bearer token is supplied, `ClientDebugger` will sign in anonymously, cache the Firebase ID token for the current session, and refresh it automatically before it expires.

The client menu now separates `Join Lobby` from `Create Lobby`, and those actions are forwarded into the join-ticket payload as `lobbyAction=join` or `lobbyAction=create` together with the requested `lobbyCode`.

The Cloud Run orchestrator now verifies Firebase ID tokens directly from the `Authorization: Bearer <id-token>` header using the Firebase Admin SDK.

For the `soulfuljourney` project, anonymous Firebase Auth is now a valid client-side option in addition to email/password.

The client flow becomes:

1. Call `/server/start`
2. Poll `/server/status`
3. Call `/server/join-token`
4. Put the join token into `NetworkConfig.ConnectionData`
5. Call `StartClient()`

## Threat Model Summary

This design protects you from the main abuse case in this repo:

- An unauthenticated user cannot hit the orchestration endpoints.
- A user who never received a backend-issued join token cannot join the dedicated server.
- A stolen old join token expires quickly and is bound to a specific target id.

It does not by itself encrypt gameplay packets. If you later add DTLS or another secure UDP transport, that protects the data plane, not the control plane.

## Practical Next Step After This

The remaining auth improvement after this repo state is a true interactive Unity login and refresh flow, so the client can acquire and rotate the OIDC token itself instead of receiving it from a launcher, environment variable, or token file.