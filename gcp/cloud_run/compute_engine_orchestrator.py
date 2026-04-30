import base64
import hashlib
import hmac
import json
import os
import secrets
import time

import firebase_admin
from flask import Flask, jsonify, request
from firebase_admin import auth as firebase_auth
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError


app = Flask(__name__)
compute = build("compute", "v1", cache_discovery=False)

GCP_PROJECT_ID = os.environ.get("GCP_PROJECT_ID", "")
GCP_ZONE = os.environ.get("GCP_ZONE", "")
GCP_INSTANCE_NAME = os.environ.get("GCP_INSTANCE_NAME", "")
TARGET_INSTANCE_ID = os.environ.get("TARGET_INSTANCE_ID", GCP_INSTANCE_NAME)
SERVER_PORT = int(os.environ.get("SERVER_PORT", "7777"))
MAX_PLAYERS = int(os.environ.get("MAX_PLAYERS", "4"))
CORS_ORIGIN = os.environ.get("CORS_ORIGIN", "*")
SERVER_ADDRESS_OVERRIDE = os.environ.get("SERVER_ADDRESS_OVERRIDE", "")
SERVER_WARMUP_SECONDS = float(os.environ.get("SERVER_WARMUP_SECONDS", "15"))
REQUIRE_PUBLIC_ADDRESS = os.environ.get("REQUIRE_PUBLIC_ADDRESS", "true").lower() == "true"
JOIN_TICKET_SECRET = os.environ.get("JOIN_TICKET_SECRET", "")
JOIN_TICKET_TTL_SECONDS = int(os.environ.get("JOIN_TICKET_TTL_SECONDS", "90"))
JOIN_TICKET_SCOPE = os.environ.get("JOIN_TICKET_SCOPE", "join")
ALLOW_ANONYMOUS_JOIN_TICKETS = os.environ.get("ALLOW_ANONYMOUS_JOIN_TICKETS", "false").lower() == "true"
OIDC_REQUIRED_ISSUER = os.environ.get("OIDC_REQUIRED_ISSUER", "")
OIDC_REQUIRED_AUDIENCE = os.environ.get("OIDC_REQUIRED_AUDIENCE", "")
OIDC_REQUIRED_SCOPE = os.environ.get("OIDC_REQUIRED_SCOPE", "")
OIDC_REQUIRED_GROUP = os.environ.get("OIDC_REQUIRED_GROUP", "")
MAX_PLAYER_NAME_LENGTH = 24
MAX_LOBBY_CODE_LENGTH = 12
FIREBASE_TOKEN_CLOCK_SKEW_SECONDS = int(os.environ.get("FIREBASE_TOKEN_CLOCK_SKEW_SECONDS", "30"))


firebase_app_options = {"projectId": GCP_PROJECT_ID} if GCP_PROJECT_ID else None
try:
    firebase_admin.get_app()
except ValueError:
    firebase_admin.initialize_app(options=firebase_app_options)


@app.after_request
def add_cors_headers(response):
    response.headers["Access-Control-Allow-Origin"] = CORS_ORIGIN
    response.headers["Access-Control-Allow-Headers"] = "Content-Type,Authorization,X-Api-Key,X-Auth-Claims"
    response.headers["Access-Control-Allow-Methods"] = "OPTIONS,GET,POST"
    return response


@app.route("/server/start", methods=["POST", "OPTIONS"])
def start_server():
    if request.method == "OPTIONS":
        return jsonify({"ok": True})

    validation_error = validate_configuration()
    if validation_error:
        return jsonify({"ok": False, "message": validation_error}), 500

    authorization_failure = require_authenticated_identity()
    if authorization_failure:
        return authorization_failure

    try:
        instance = describe_instance()
        current_state = get_instance_state(instance)

        if current_state == "terminated":
            compute.instances().start(
                project=GCP_PROJECT_ID,
                zone=GCP_ZONE,
                instance=GCP_INSTANCE_NAME,
            ).execute()
            instance = describe_instance()
        elif current_state in ("stopping", "suspending"):
            return jsonify(build_status_payload(instance, False, "Instance is still stopping. Try again in a few seconds.")), 409

        is_ready = get_instance_ready(instance)
        return jsonify(build_status_payload(instance, is_ready, build_status_message(instance, is_ready)))
    except HttpError as error:
        return jsonify({"ok": False, "message": str(error)}), 500


@app.route("/server/status", methods=["GET", "OPTIONS"])
def server_status():
    if request.method == "OPTIONS":
        return jsonify({"ok": True})

    validation_error = validate_configuration()
    if validation_error:
        return jsonify({"ok": False, "message": validation_error}), 500

    authorization_failure = require_authenticated_identity()
    if authorization_failure:
        return authorization_failure

    try:
        instance = describe_instance()
        is_ready = get_instance_ready(instance)
        return jsonify(build_status_payload(instance, is_ready, build_status_message(instance, is_ready)))
    except HttpError as error:
        return jsonify({"ok": False, "message": str(error)}), 500


@app.route("/server/join-token", methods=["POST", "OPTIONS"])
def join_token():
    if request.method == "OPTIONS":
        return jsonify({"ok": True})

    validation_error = validate_configuration(require_join_secret=True)
    if validation_error:
        return jsonify({"ok": False, "message": validation_error}), 500

    claims, claims_error = get_authorizer_claims()
    authorization_error, subject = authorize_request(
        claims,
        claims_error,
        allow_anonymous=ALLOW_ANONYMOUS_JOIN_TICKETS,
        missing_identity_message="Authenticated identity is required to obtain a join token.",
    )
    if authorization_error:
        return jsonify({"ok": False, "message": authorization_error[1]}), authorization_error[0]

    try:
        instance = describe_instance()
        is_ready = get_instance_ready(instance)
        connection_address = resolve_connection_address(instance)

        if get_instance_state(instance) != "running" or not is_ready or not connection_address:
            return jsonify(build_status_payload(instance, is_ready, "Game server is not ready to issue join tickets yet.")), 409

        request_body = parse_request_body()
        player_name = sanitize_player_name(
            request_body.get("playerName")
            or claims.get("name")
            or claims.get("email")
            or claims.get("preferred_username")
            or "player"
        )
        lobby_code = sanitize_lobby_code(request_body.get("lobbyCode"))
        if not lobby_code:
            return jsonify({"ok": False, "message": "Lobby code is required."}), 400

        lobby_action = sanitize_lobby_action(request_body.get("lobbyAction"))
        if not lobby_action:
            return jsonify({"ok": False, "message": "Lobby action must be 'create' or 'join'."}), 400

        join_token_value, expires_at_unix = build_join_token(subject, player_name, lobby_code, lobby_action)

        return jsonify(
            {
                "ok": True,
                "targetId": TARGET_INSTANCE_ID,
                "instanceId": TARGET_INSTANCE_ID,
                "connectionAddress": connection_address,
                "port": SERVER_PORT,
                "playerName": player_name,
                "lobbyCode": lobby_code,
                "lobbyAction": lobby_action,
                "joinToken": join_token_value,
                "expiresAtUnix": expires_at_unix,
                "message": "Lobby create ticket issued." if lobby_action == "create" else "Lobby join ticket issued.",
            }
        )
    except HttpError as error:
        return jsonify({"ok": False, "message": str(error)}), 500


def validate_configuration(require_join_secret=False):
    if not GCP_PROJECT_ID:
        return "GCP_PROJECT_ID is not configured."

    if not GCP_ZONE:
        return "GCP_ZONE is not configured."

    if not GCP_INSTANCE_NAME:
        return "GCP_INSTANCE_NAME is not configured."

    if require_join_secret and not JOIN_TICKET_SECRET:
        return "JOIN_TICKET_SECRET is not configured."

    return ""


def describe_instance():
    return compute.instances().get(
        project=GCP_PROJECT_ID,
        zone=GCP_ZONE,
        instance=GCP_INSTANCE_NAME,
    ).execute()


def get_instance_state(instance):
    return str(instance.get("status", "UNKNOWN")).lower()


def get_instance_ready(instance):
    if get_instance_state(instance) != "running":
        return False

    if not REQUIRE_PUBLIC_ADDRESS:
        return True

    return bool(resolve_connection_address(instance))


def resolve_connection_address(instance):
    if SERVER_ADDRESS_OVERRIDE:
        return SERVER_ADDRESS_OVERRIDE

    interfaces = instance.get("networkInterfaces", [])
    for interface in interfaces:
        access_configs = interface.get("accessConfigs", [])
        for access_config in access_configs:
            nat_ip = access_config.get("natIP")
            if nat_ip:
                return nat_ip

    return instance.get("networkIP", "")


def build_status_payload(instance, is_ready, message):
    connection_address = resolve_connection_address(instance)
    network_ip = instance.get("networkIP", "")

    return {
        "ok": True,
        "targetId": TARGET_INSTANCE_ID,
        "instanceId": TARGET_INSTANCE_ID,
        "instanceState": get_instance_state(instance),
        "publicIpAddress": connection_address,
        "publicDnsName": "",
        "privateIpAddress": network_ip,
        "connectionAddress": connection_address,
        "port": SERVER_PORT,
        "maxPlayers": MAX_PLAYERS,
        "instanceStatusOk": is_ready,
        "serverWarmupSeconds": SERVER_WARMUP_SECONDS,
        "isReady": is_ready,
        "message": message,
    }


def build_status_message(instance, is_ready):
    state = get_instance_state(instance)

    if state == "terminated":
        return "Compute Engine VM is stopped. Start requested or retry needed."

    if state in ("provisioning", "staging"):
        return "Compute Engine VM is booting. Waiting for the external address and server warmup."

    if state == "running":
        if not is_ready:
            return "Compute Engine VM is running, but the public address or warmup requirements are not ready yet."

        if SERVER_WARMUP_SECONDS > 0:
            return f"Compute Engine VM is ready. Waiting {SERVER_WARMUP_SECONDS:.0f}s before client connect is recommended."

        return "Compute Engine VM is ready for client connection."

    return f"Compute Engine VM state: {state}"


def build_join_token(subject, player_name, lobby_code, lobby_action):
    expires_at_unix = int(time.time()) + max(15, JOIN_TICKET_TTL_SECONDS)
    payload = {
        "sub": subject,
        "pn": player_name,
        "scp": JOIN_TICKET_SCOPE,
        "iid": TARGET_INSTANCE_ID,
        "lc": lobby_code,
        "la": lobby_action,
        "nonce": secrets.token_urlsafe(8),
        "exp": expires_at_unix,
    }
    payload_json = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    payload_encoded = base64url_encode(payload_json)
    signature = hmac.new(JOIN_TICKET_SECRET.encode("utf-8"), payload_encoded.encode("utf-8"), hashlib.sha256).digest()
    return f"{payload_encoded}.{base64url_encode(signature)}", expires_at_unix


def get_authorizer_claims():
    firebase_claims, firebase_error = get_firebase_claims()
    if firebase_error:
        return {}, firebase_error

    if firebase_claims:
        return firebase_claims, ""

    claims_header = request.headers.get("X-Auth-Claims", "")
    if claims_header:
        try:
            return json.loads(claims_header), ""
        except json.JSONDecodeError:
            return {}, "X-Auth-Claims header is not valid JSON."

    goog_user_id = request.headers.get("X-Goog-Authenticated-User-Id", "")
    goog_user_email = request.headers.get("X-Goog-Authenticated-User-Email", "")
    if goog_user_id or goog_user_email:
        return {
            "sub": goog_user_id or goog_user_email,
            "email": goog_user_email,
            "iss": "google-iap",
        }, ""

    return {}, ""


def get_firebase_claims():
    authorization_header = request.headers.get("Authorization", "")
    if not authorization_header:
        return {}, ""

    scheme, _, token = authorization_header.partition(" ")
    if scheme.lower() != "bearer" or not token.strip():
        return {}, "Authorization header must use the Bearer scheme."

    try:
        claims = firebase_auth.verify_id_token(
            token.strip(),
            check_revoked=False,
            clock_skew_seconds=max(0, FIREBASE_TOKEN_CLOCK_SKEW_SECONDS),
        )
        return claims, ""
    except Exception as error:
        return {}, f"Firebase ID token verification failed: {error}"


def require_authenticated_identity():
    claims, claims_error = get_authorizer_claims()
    authorization_error, _ = authorize_request(claims, claims_error)
    if authorization_error:
        return jsonify({"ok": False, "message": authorization_error[1]}), authorization_error[0]

    return None


def authorize_request(claims, claims_error="", allow_anonymous=False, missing_identity_message="Authenticated identity is required."):
    if claims_error:
        return (401, claims_error), None

    if not claims:
        if allow_anonymous:
            return None, f"anonymous:{secrets.token_hex(6)}"

        return (401, missing_identity_message), None

    subject = claims.get("sub") or claims.get("user_id") or claims.get("email")
    if not subject:
        return (401, "Authenticated identity is missing the sub claim."), None

    if OIDC_REQUIRED_ISSUER and claims.get("iss") != OIDC_REQUIRED_ISSUER:
        return (403, "OIDC issuer is invalid."), None

    if OIDC_REQUIRED_AUDIENCE:
        audience = claims.get("aud") or claims.get("azp") or claims.get("client_id")
        if audience != OIDC_REQUIRED_AUDIENCE:
            return (403, "OIDC audience is invalid."), None

    if OIDC_REQUIRED_SCOPE and not has_required_scope(claims, OIDC_REQUIRED_SCOPE):
        return (403, f"OIDC token is missing required scope: {OIDC_REQUIRED_SCOPE}."), None

    if OIDC_REQUIRED_GROUP and not has_required_group(claims, OIDC_REQUIRED_GROUP):
        return (403, f"OIDC token is missing required group: {OIDC_REQUIRED_GROUP}."), None

    return None, subject


def has_required_scope(claims, required_scope):
    scopes = claims.get("scope") or claims.get("scp") or ""
    if isinstance(scopes, list):
        values = scopes
    else:
        values = str(scopes).split()

    return required_scope in values


def has_required_group(claims, required_group):
    groups = claims.get("groups") or claims.get("google/groups") or []
    if isinstance(groups, str):
        groups = [item.strip() for item in groups.split(",") if item.strip()]
    return required_group in groups


def sanitize_player_name(raw_value):
    candidate = (raw_value or "player").strip()
    if not candidate:
        candidate = "player"

    if len(candidate) > MAX_PLAYER_NAME_LENGTH:
        candidate = candidate[:MAX_PLAYER_NAME_LENGTH]

    return candidate


def sanitize_lobby_code(raw_value):
    if raw_value is None:
        return ""

    candidate = str(raw_value).strip().upper()
    if not candidate:
        return ""

    sanitized = []
    for character in candidate:
        if character.isalnum() or character in ("-", "_"):
            sanitized.append(character)

        if len(sanitized) >= MAX_LOBBY_CODE_LENGTH:
            break

    return "".join(sanitized)


def sanitize_lobby_action(raw_value):
    candidate = str(raw_value or "").strip().lower()
    if candidate in ("create", "join"):
        return candidate

    return ""


def parse_request_body():
    if not request.data:
        return {}

    try:
        return request.get_json(silent=True) or {}
    except Exception:
        return {}


def base64url_encode(raw_bytes):
    return base64.urlsafe_b64encode(raw_bytes).rstrip(b"=").decode("ascii")


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=int(os.environ.get("PORT", "8080")))