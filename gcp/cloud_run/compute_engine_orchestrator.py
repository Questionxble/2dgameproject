import base64
import hashlib
import hmac
import json
import os
import secrets
import time
import uuid

import firebase_admin
from flask import Flask, jsonify, request
from firebase_admin import auth as firebase_auth
try:
    from google.cloud import firestore
except ImportError:
    firestore = None
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError


app = Flask(__name__)
compute = build("compute", "v1", cache_discovery=False)

GCP_PROJECT_ID = os.environ.get("GCP_PROJECT_ID", "")
GCP_ZONE = os.environ.get("GCP_ZONE", "")
GCP_INSTANCE_NAME = os.environ.get("GCP_INSTANCE_NAME", "")
TARGET_INSTANCE_ID = os.environ.get("TARGET_INSTANCE_ID", GCP_INSTANCE_NAME)
SERVER_ALLOCATION_MODE = os.environ.get("SERVER_ALLOCATION_MODE", "single-instance")
SERVER_ALLOCATION_STORE = os.environ.get("SERVER_ALLOCATION_STORE", "firestore")
LOBBY_ALLOCATION_COLLECTION = os.environ.get("LOBBY_ALLOCATION_COLLECTION", "serverLobbyAllocations")
MANAGED_INSTANCE_TEMPLATE = os.environ.get("MANAGED_INSTANCE_TEMPLATE", "")
MANAGED_INSTANCE_NAME_PREFIX = os.environ.get("MANAGED_INSTANCE_NAME_PREFIX", "lobby-")
MANAGED_INSTANCE_DESCRIPTION_PREFIX = os.environ.get("MANAGED_INSTANCE_DESCRIPTION_PREFIX", "Ephemeral dedicated lobby server")
MANAGED_INSTANCE_METADATA_TARGET_ID_KEY = os.environ.get("MANAGED_INSTANCE_METADATA_TARGET_ID_KEY", "game-server-target-id")
MANAGED_INSTANCE_METADATA_LOBBY_CODE_KEY = os.environ.get("MANAGED_INSTANCE_METADATA_LOBBY_CODE_KEY", "game-server-lobby-code")
SERVER_PORT = int(os.environ.get("SERVER_PORT", "7777"))
MAX_PLAYERS = int(os.environ.get("MAX_PLAYERS", "4"))
CORS_ORIGIN = os.environ.get("CORS_ORIGIN", "*")
SERVER_ADDRESS_OVERRIDE = os.environ.get("SERVER_ADDRESS_OVERRIDE", "")
SERVER_WARMUP_SECONDS = float(os.environ.get("SERVER_WARMUP_SECONDS", "15"))
REQUIRE_PUBLIC_ADDRESS = os.environ.get("REQUIRE_PUBLIC_ADDRESS", "true").lower() == "true"
GAME_TRANSPORT_MODE = os.environ.get("GAME_TRANSPORT_MODE", "direct").strip().lower()
RELAY_CONNECTION_TYPE = os.environ.get("RELAY_CONNECTION_TYPE", "dtls").strip().lower()
JOIN_TICKET_SECRET = os.environ.get("JOIN_TICKET_SECRET", "")
SERVER_REGISTRATION_HEADER_NAME = os.environ.get("SERVER_REGISTRATION_HEADER_NAME", "X-Server-Registration-Token")
SERVER_REGISTRATION_TOKEN = os.environ.get("SERVER_REGISTRATION_TOKEN", JOIN_TICKET_SECRET).strip()
SERVER_RUNTIME_HEARTBEAT_TTL_SECONDS = float(os.environ.get("SERVER_RUNTIME_HEARTBEAT_TTL_SECONDS", "20"))
COMPUTE_API_MAX_ATTEMPTS = max(1, int(os.environ.get("COMPUTE_API_MAX_ATTEMPTS", "3")))
COMPUTE_API_RETRY_DELAY_SECONDS = max(0.0, float(os.environ.get("COMPUTE_API_RETRY_DELAY_SECONDS", "0.75")))
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
runtime_server_registry = {}
lobby_allocation_registry = {}
firestore_client = None
firestore_warning_logged = False


firebase_app_options = {"projectId": GCP_PROJECT_ID} if GCP_PROJECT_ID else None
try:
    firebase_admin.get_app()
except ValueError:
    firebase_admin.initialize_app(options=firebase_app_options)


@app.after_request
def add_cors_headers(response):
    response.headers["Access-Control-Allow-Origin"] = CORS_ORIGIN
    response.headers["Access-Control-Allow-Headers"] = f"Content-Type,Authorization,X-Api-Key,X-Auth-Claims,{SERVER_REGISTRATION_HEADER_NAME}"
    response.headers["Access-Control-Allow-Methods"] = "OPTIONS,GET,POST"
    return response


@app.route("/server/runtime", methods=["POST", "OPTIONS"])
def server_runtime():
    if request.method == "OPTIONS":
        return jsonify({"ok": True})

    validation_error = validate_configuration(require_server_registration_token=True)
    if validation_error:
        return jsonify({"ok": False, "message": validation_error}), 500

    registration_failure = require_server_registration()
    if registration_failure:
        return registration_failure

    request_body = parse_request_body()
    target_context = resolve_target_context(request_body=request_body)

    try:
        runtime_state = build_runtime_server_state(request_body, target_context)
    except ValueError as error:
        return jsonify({"ok": False, "message": str(error)}), 400

    runtime_server_registry[runtime_state["targetId"]] = runtime_state

    return jsonify(
        {
            "ok": True,
            "targetId": runtime_state["targetId"],
            "allocationMode": target_context["allocationMode"],
            "transportMode": runtime_state["transportMode"],
            "isReady": runtime_state["isReady"],
            "lastHeartbeatUnix": runtime_state["lastHeartbeatUnix"],
            "message": "Runtime status updated.",
        }
    )


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

    request_body = parse_request_body()
    requested_lobby_code = sanitize_lobby_code(request_body.get("lobbyCode"))
    requested_lobby_action = sanitize_lobby_action(request_body.get("lobbyAction"))

    try:
        target_context = resolve_target_context(
            request_body=request_body,
            create_if_missing=requested_lobby_action == "create",
        )

        if target_context.get("error"):
            return jsonify({"ok": False, "message": target_context["error"]}), 409

        if target_context["lobbyCode"] and requested_lobby_action == "create":
            upsert_lobby_allocation(target_context["lobbyCode"], target_context, status="allocating")

        instance = describe_instance_if_exists(target_context)
        if instance is None:
            if should_create_managed_instance(target_context, requested_lobby_action):
                create_instance_from_template(target_context)
                upsert_lobby_allocation(target_context["lobbyCode"], target_context, status="provisioning")
                return jsonify(build_pending_status_payload(target_context, build_pending_instance_message(target_context)))

            missing_message = build_missing_lobby_allocation_message(target_context.get("lobbyCode"))
            return jsonify({"ok": False, "message": missing_message}), 409

        current_state = get_instance_state(instance)

        if current_state == "terminated":
            runtime_server_registry.pop(target_context["targetId"], None)
            start_instance(target_context)
            instance = describe_instance(target_context)
        elif current_state in ("stopping", "suspending"):
            runtime_server_registry.pop(target_context["targetId"], None)
            return jsonify(build_status_payload(target_context, instance, False, "Instance is still stopping. Try again in a few seconds.")), 409

        if target_context["lobbyCode"]:
            upsert_lobby_allocation(target_context["lobbyCode"], target_context, status=current_state or "assigned")

        is_ready = get_instance_ready(target_context, instance)
        return jsonify(build_status_payload(target_context, instance, is_ready, build_status_message(target_context, instance, is_ready)))
    except ValueError as error:
        return jsonify({"ok": False, "message": str(error)}), 400
    except HttpError as error:
        return jsonify({"ok": False, "message": str(error)}), 500
    except Exception:
        app.logger.exception("Transient error while starting or describing the Compute Engine instance.")
        return jsonify({"ok": False, "message": "Temporary Compute Engine control-plane error. Retry in a few seconds."}), 503


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
        target_context = resolve_target_context()
        if target_context.get("error"):
            return jsonify({"ok": False, "message": target_context["error"]}), 409

        instance = describe_instance_if_exists(target_context)
        if instance is None:
            if should_report_pending_instance(target_context):
                return jsonify(build_pending_status_payload(target_context, build_pending_instance_message(target_context)))

            return jsonify({"ok": False, "message": "Requested dedicated server instance does not exist."}), 404

        is_ready = get_instance_ready(target_context, instance)
        return jsonify(build_status_payload(target_context, instance, is_ready, build_status_message(target_context, instance, is_ready)))
    except ValueError as error:
        return jsonify({"ok": False, "message": str(error)}), 400
    except HttpError as error:
        return jsonify({"ok": False, "message": str(error)}), 500
    except Exception:
        app.logger.exception("Transient error while reading Compute Engine status.")
        return jsonify({"ok": False, "message": "Temporary Compute Engine control-plane error. Retry in a few seconds."}), 503


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

    request_body = parse_request_body()

    try:
        target_context = resolve_target_context(request_body=request_body)
        if target_context.get("error"):
            return jsonify({"ok": False, "message": target_context["error"]}), 409

        instance = describe_instance_if_exists(target_context)
        if instance is None:
            return jsonify(build_pending_status_payload(target_context, "Game server is not ready to issue join tickets yet.")), 409

        runtime_state = get_runtime_server_state(target_context["targetId"]) if get_instance_state(instance) == "running" else None
        is_ready = get_instance_ready(target_context, instance)
        connection_address = resolve_connection_address(instance, runtime_state)
        transport_mode = resolve_transport_mode(runtime_state)
        relay_join_code = resolve_runtime_value(runtime_state, "relayJoinCode")
        relay_region = resolve_runtime_value(runtime_state, "relayRegion")
        relay_connection_type = resolve_relay_connection_type(runtime_state)
        resolved_port = resolve_port(runtime_state)

        requires_direct_address = transport_mode != "relay"
        if get_instance_state(instance) != "running" or not is_ready or (requires_direct_address and not connection_address):
            return jsonify(build_status_payload(target_context, instance, is_ready, "Game server is not ready to issue join tickets yet.")), 409

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

        if lobby_action == "create":
            upsert_lobby_allocation(lobby_code, target_context, owner_subject=subject, status="assigned")

        join_token_value, expires_at_unix = build_join_token(subject, player_name, lobby_code, lobby_action, target_context)

        return jsonify(
            {
                "ok": True,
                "targetId": target_context["targetId"],
                "instanceId": target_context["targetId"],
                "allocationMode": target_context["allocationMode"],
                "connectionAddress": connection_address,
                "port": resolved_port,
                "transportMode": transport_mode,
                "relayJoinCode": relay_join_code,
                "relayRegion": relay_region,
                "relayConnectionType": relay_connection_type,
                "playerName": player_name,
                "lobbyCode": lobby_code,
                "lobbyAction": lobby_action,
                "joinToken": join_token_value,
                "expiresAtUnix": expires_at_unix,
                "message": "Lobby create ticket issued." if lobby_action == "create" else "Lobby join ticket issued.",
            }
        )
    except ValueError as error:
        return jsonify({"ok": False, "message": str(error)}), 400
    except HttpError as error:
        return jsonify({"ok": False, "message": str(error)}), 500
    except Exception:
        app.logger.exception("Transient error while issuing a join token.")
        return jsonify({"ok": False, "message": "Temporary control-plane error while issuing a join token. Retry in a few seconds."}), 503


def validate_configuration(require_join_secret=False, require_server_registration_token=False):
    if not normalize_allocation_mode(SERVER_ALLOCATION_MODE):
        return "SERVER_ALLOCATION_MODE must be 'single-instance' or 'per-lobby-template'."

    if not normalize_allocation_store(SERVER_ALLOCATION_STORE):
        return "SERVER_ALLOCATION_STORE must be 'firestore' or 'memory'."

    if get_server_allocation_mode() == "per-lobby-template" and not MANAGED_INSTANCE_TEMPLATE:
        return "MANAGED_INSTANCE_TEMPLATE is not configured for per-lobby-template allocation mode."

    if not GCP_PROJECT_ID:
        return "GCP_PROJECT_ID is not configured."

    if not GCP_ZONE:
        return "GCP_ZONE is not configured."

    if not GCP_INSTANCE_NAME:
        return "GCP_INSTANCE_NAME is not configured."

    if require_join_secret and not JOIN_TICKET_SECRET:
        return "JOIN_TICKET_SECRET is not configured."

    if require_server_registration_token and not SERVER_REGISTRATION_TOKEN:
        return "SERVER_REGISTRATION_TOKEN is not configured."

    return ""


def describe_instance(target_context=None):
    target_context = target_context or get_default_target_context()
    instance_name = target_context["instanceName"]
    if not instance_name:
        raise ValueError("Target instance name is not configured.")

    return execute_compute_request(
        lambda: compute.instances().get(
            project=GCP_PROJECT_ID,
            zone=GCP_ZONE,
            instance=instance_name,
        ),
        "describe instance",
    )


def describe_instance_if_exists(target_context=None):
    try:
        return describe_instance(target_context)
    except HttpError as error:
        if is_not_found_http_error(error):
            return None

        raise


def start_instance(target_context=None):
    target_context = target_context or get_default_target_context()
    instance_name = target_context["instanceName"]
    if not instance_name:
        raise ValueError("Target instance name is not configured.")

    return execute_compute_request(
        lambda: compute.instances().start(
            project=GCP_PROJECT_ID,
            zone=GCP_ZONE,
            instance=instance_name,
        ),
        "start instance",
    )


def create_instance_from_template(target_context):
    instance_name = target_context["instanceName"]
    if not instance_name:
        raise ValueError("Managed instance name is not configured.")

    request_body = {
        "name": instance_name,
        "description": build_managed_instance_description(target_context),
        "labels": {
            "serverallocator": "managed",
            "allocationmode": "per-lobby",
        },
    }

    metadata_items = build_managed_instance_metadata_items(target_context)
    if metadata_items:
        request_body["metadata"] = {"items": metadata_items}

    try:
        return execute_compute_request(
            lambda: compute.instances().insert(
                project=GCP_PROJECT_ID,
                zone=GCP_ZONE,
                sourceInstanceTemplate=MANAGED_INSTANCE_TEMPLATE,
                requestId=str(uuid.uuid4()),
                body=request_body,
            ),
            "create managed lobby instance",
        )
    except HttpError as error:
        if is_conflict_http_error(error):
            return None

        raise


def execute_compute_request(request_factory, operation_name):
    last_error = None

    for attempt in range(1, COMPUTE_API_MAX_ATTEMPTS + 1):
        try:
            return request_factory().execute()
        except HttpError as error:
            if not is_retryable_http_error(error) or attempt >= COMPUTE_API_MAX_ATTEMPTS:
                raise

            last_error = error
        except (ConnectionResetError, TimeoutError, OSError) as error:
            if attempt >= COMPUTE_API_MAX_ATTEMPTS:
                raise

            last_error = error

        app.logger.warning(
            "Retrying Compute Engine %s after transient failure (%s), attempt %s/%s.",
            operation_name,
            type(last_error).__name__,
            attempt,
            COMPUTE_API_MAX_ATTEMPTS,
        )

        if COMPUTE_API_RETRY_DELAY_SECONDS > 0:
            time.sleep(COMPUTE_API_RETRY_DELAY_SECONDS * attempt)

    if last_error is not None:
        raise last_error

    raise RuntimeError(f"Compute Engine {operation_name} failed without an error.")


def is_retryable_http_error(error):
    status_code = getattr(getattr(error, "resp", None), "status", 0)
    return status_code in (429, 500, 502, 503, 504)


def get_http_status_code(error):
    return int(getattr(getattr(error, "resp", None), "status", 0) or 0)


def is_not_found_http_error(error):
    return get_http_status_code(error) == 404


def is_conflict_http_error(error):
    return get_http_status_code(error) == 409


def get_instance_state(instance):
    return str(instance.get("status", "UNKNOWN")).lower()


def get_instance_ready(target_context, instance):
    if get_instance_state(instance) != "running":
        return False

    runtime_state = get_runtime_server_state(target_context["targetId"])
    transport_mode = resolve_transport_mode(runtime_state)

    if transport_mode == "relay":
        return bool(runtime_state and runtime_state.get("isReady") and runtime_state.get("relayJoinCode"))

    if not REQUIRE_PUBLIC_ADDRESS:
        return bool(runtime_state.get("isReady")) if runtime_state else True

    if runtime_state:
        return bool(runtime_state.get("isReady") and resolve_connection_address(instance, runtime_state))

    return bool(resolve_connection_address(instance))


def resolve_connection_address(instance, runtime_state=None):
    runtime_connection_address = resolve_runtime_value(runtime_state, "connectionAddress")
    if runtime_connection_address:
        return runtime_connection_address

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


def build_pending_status_payload(target_context, message, instance_state="provisioning"):
    transport_mode = normalize_transport_mode(GAME_TRANSPORT_MODE)
    return {
        "ok": True,
        "targetId": target_context["targetId"],
        "instanceId": target_context["targetId"],
        "allocationMode": target_context["allocationMode"],
        "lobbyCode": target_context.get("lobbyCode", ""),
        "instanceState": instance_state,
        "publicIpAddress": "",
        "publicDnsName": "",
        "privateIpAddress": "",
        "connectionAddress": "",
        "port": SERVER_PORT,
        "transportMode": transport_mode,
        "relayJoinCode": "",
        "relayRegion": "",
        "relayConnectionType": normalize_relay_connection_type(RELAY_CONNECTION_TYPE),
        "maxPlayers": MAX_PLAYERS,
        "connectedPlayers": 0,
        "lastHeartbeatUnix": 0,
        "instanceStatusOk": False,
        "serverWarmupSeconds": SERVER_WARMUP_SECONDS,
        "isReady": False,
        "message": message,
    }


def build_status_payload(target_context, instance, is_ready, message):
    runtime_state = get_runtime_server_state(target_context["targetId"]) if get_instance_state(instance) == "running" else None
    public_ip_address = resolve_connection_address(instance)
    connection_address = resolve_connection_address(instance, runtime_state)
    network_ip = instance.get("networkIP", "")
    transport_mode = resolve_transport_mode(runtime_state)

    if transport_mode == "relay":
        connection_address = ""

    return {
        "ok": True,
        "targetId": target_context["targetId"],
        "instanceId": target_context["targetId"],
        "allocationMode": target_context["allocationMode"],
        "lobbyCode": target_context.get("lobbyCode", ""),
        "instanceState": get_instance_state(instance),
        "publicIpAddress": public_ip_address,
        "publicDnsName": "",
        "privateIpAddress": network_ip,
        "connectionAddress": connection_address,
        "port": resolve_port(runtime_state),
        "transportMode": transport_mode,
        "relayJoinCode": resolve_runtime_value(runtime_state, "relayJoinCode"),
        "relayRegion": resolve_runtime_value(runtime_state, "relayRegion"),
        "relayConnectionType": resolve_relay_connection_type(runtime_state),
        "maxPlayers": int(resolve_runtime_value(runtime_state, "maxPlayers") or MAX_PLAYERS),
        "connectedPlayers": int(resolve_runtime_value(runtime_state, "connectedPlayers") or 0),
        "lastHeartbeatUnix": int(resolve_runtime_value(runtime_state, "lastHeartbeatUnix") or 0),
        "instanceStatusOk": is_ready,
        "serverWarmupSeconds": SERVER_WARMUP_SECONDS,
        "isReady": is_ready,
        "message": message,
    }


def build_status_message(target_context, instance, is_ready):
    state = get_instance_state(instance)
    runtime_state = get_runtime_server_state(target_context["targetId"]) if state == "running" else None
    transport_mode = resolve_transport_mode(runtime_state)

    if state == "terminated":
        return "Compute Engine VM is stopped. Start requested or retry needed."

    if state in ("provisioning", "staging"):
        return "Compute Engine VM is booting. Waiting for the external address and server warmup."

    if state == "running":
        if not is_ready:
            if transport_mode == "relay":
                return "Compute Engine VM is running, but the dedicated server has not published a live Relay join code yet."

            if runtime_state:
                return "Compute Engine VM is running, but the dedicated server heartbeat has not marked the process ready yet."

            return "Compute Engine VM is running, but the public address or warmup requirements are not ready yet."

        if SERVER_WARMUP_SECONDS > 0:
            if transport_mode == "relay":
                return "Dedicated server heartbeat is live and Relay join code is ready."

            return f"Compute Engine VM is ready. Waiting {SERVER_WARMUP_SECONDS:.0f}s before client connect is recommended."

        return "Compute Engine VM is ready for client connection."

    return f"Compute Engine VM state: {state}"


def require_server_registration():
    provided_token = request.headers.get(SERVER_REGISTRATION_HEADER_NAME, "").strip()
    if not provided_token:
        return jsonify({"ok": False, "message": f"Missing {SERVER_REGISTRATION_HEADER_NAME} header."}), 401

    if not hmac.compare_digest(provided_token, SERVER_REGISTRATION_TOKEN):
        return jsonify({"ok": False, "message": "Server registration token is invalid."}), 403

    return None


def build_runtime_server_state(request_body, target_context):
    target_id = normalize_target_id(request_body.get("targetId") or request_body.get("instanceId") or target_context["targetId"])
    if target_id != target_context["targetId"]:
        raise ValueError(f"Runtime heartbeat target id '{target_id}' does not match resolved target id '{target_context['targetId']}'.")

    transport_mode = normalize_transport_mode(request_body.get("transportMode"))
    relay_join_code = str(request_body.get("relayJoinCode") or "").strip()
    is_ready = parse_bool(request_body.get("isReady"))

    if transport_mode == "relay":
        is_ready = is_ready and bool(relay_join_code)

    return {
        "targetId": target_id,
        "instanceId": target_id,
        "transportMode": transport_mode,
        "connectionAddress": str(request_body.get("connectionAddress") or "").strip(),
        "port": parse_int(request_body.get("port"), SERVER_PORT),
        "relayJoinCode": relay_join_code,
        "relayRegion": str(request_body.get("relayRegion") or "").strip(),
        "relayConnectionType": normalize_relay_connection_type(request_body.get("relayConnectionType")),
        "maxPlayers": parse_int(request_body.get("maxPlayers"), MAX_PLAYERS),
        "connectedPlayers": parse_int(request_body.get("connectedPlayers"), 0),
        "isReady": is_ready,
        "lastHeartbeatUnix": int(time.time()),
    }


def get_runtime_server_state(target_id=None):
    target_id = normalize_target_id(target_id) or TARGET_INSTANCE_ID
    runtime_state = runtime_server_registry.get(target_id)
    if not runtime_state:
        return None

    last_heartbeat_unix = int(runtime_state.get("lastHeartbeatUnix") or 0)
    if SERVER_RUNTIME_HEARTBEAT_TTL_SECONDS > 0 and time.time() - last_heartbeat_unix > SERVER_RUNTIME_HEARTBEAT_TTL_SECONDS:
        runtime_server_registry.pop(target_id, None)
        return None

    return runtime_state


def resolve_transport_mode(runtime_state=None):
    return normalize_transport_mode(resolve_runtime_value(runtime_state, "transportMode") or GAME_TRANSPORT_MODE)


def resolve_port(runtime_state=None):
    return parse_int(resolve_runtime_value(runtime_state, "port"), SERVER_PORT)


def resolve_relay_connection_type(runtime_state=None):
    return normalize_relay_connection_type(resolve_runtime_value(runtime_state, "relayConnectionType") or RELAY_CONNECTION_TYPE)


def resolve_runtime_value(runtime_state, key):
    if not runtime_state:
        return ""

    return runtime_state.get(key, "")


def normalize_transport_mode(raw_value):
    candidate = str(raw_value or GAME_TRANSPORT_MODE or "direct").strip().lower()
    if candidate == "relay":
        return "relay"

    return "direct"


def normalize_relay_connection_type(raw_value):
    candidate = str(raw_value or RELAY_CONNECTION_TYPE or "dtls").strip().lower()
    if candidate in ("udp", "dtls", "ws", "wss"):
        return candidate

    return "dtls"


def parse_int(raw_value, fallback):
    try:
        return int(raw_value)
    except (TypeError, ValueError):
        return fallback


def parse_bool(raw_value):
    if isinstance(raw_value, bool):
        return raw_value

    return str(raw_value or "").strip().lower() in ("1", "true", "yes", "on")


def build_join_token(subject, player_name, lobby_code, lobby_action, target_context=None):
    target_context = target_context or get_default_target_context()
    expires_at_unix = int(time.time()) + max(15, JOIN_TICKET_TTL_SECONDS)
    payload = {
        "sub": subject,
        "pn": player_name,
        "scp": JOIN_TICKET_SCOPE,
        "iid": target_context["targetId"],
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


def normalize_target_id(raw_value):
    return str(raw_value or "").strip()


def normalize_managed_instance_name_component(raw_value, fallback="lobby"):
    candidate = str(raw_value or "").strip().lower()
    normalized = []
    previous_was_dash = False

    for character in candidate:
        if "a" <= character <= "z" or "0" <= character <= "9":
            normalized.append(character)
            previous_was_dash = False
        elif character in ("-", "_", " "):
            if normalized and not previous_was_dash:
                normalized.append("-")
                previous_was_dash = True

    result = "".join(normalized).strip("-")
    if not result:
        result = fallback

    if not ("a" <= result[0] <= "z"):
        result = f"{fallback}-{result}"

    result = result[:63].strip("-")
    return result or fallback


def normalize_managed_instance_name_prefix(raw_value):
    candidate = normalize_managed_instance_name_component(raw_value, fallback="lobby")
    if not candidate.endswith("-"):
        candidate = f"{candidate}-"

    return candidate


def build_managed_instance_name(lobby_code):
    prefix = normalize_managed_instance_name_prefix(MANAGED_INSTANCE_NAME_PREFIX)
    lobby_fragment = normalize_managed_instance_name_component(lobby_code, fallback="lobby")
    lobby_hash = hashlib.sha1(lobby_code.encode("utf-8")).hexdigest()[:8]
    available_length = max(1, 63 - len(prefix) - len(lobby_hash) - 1)
    lobby_fragment = lobby_fragment[:available_length].strip("-") or "lobby"
    instance_name = f"{prefix}{lobby_fragment}-{lobby_hash}"
    return instance_name[:63].strip("-")


def build_managed_instance_description(target_context):
    prefix = str(MANAGED_INSTANCE_DESCRIPTION_PREFIX or "Managed dedicated lobby server").strip()
    lobby_code = target_context.get("lobbyCode")
    if not lobby_code:
        return prefix

    return f"{prefix} for lobby {lobby_code}"


def build_managed_instance_metadata_items(target_context):
    metadata_items = []
    target_key = str(MANAGED_INSTANCE_METADATA_TARGET_ID_KEY or "").strip()
    lobby_key = str(MANAGED_INSTANCE_METADATA_LOBBY_CODE_KEY or "").strip()

    if target_key and target_context.get("targetId"):
        metadata_items.append({"key": target_key, "value": target_context["targetId"]})

    if lobby_key and target_context.get("lobbyCode"):
        metadata_items.append({"key": lobby_key, "value": target_context["lobbyCode"]})

    return metadata_items


def should_create_managed_instance(target_context, requested_lobby_action):
    return (
        get_server_allocation_mode() == "per-lobby-template"
        and requested_lobby_action == "create"
        and bool(target_context.get("lobbyCode"))
    )


def should_report_pending_instance(target_context):
    return get_server_allocation_mode() == "per-lobby-template" and bool(target_context.get("targetId") or target_context.get("lobbyCode"))


def build_missing_lobby_allocation_message(lobby_code):
    if lobby_code:
        return f"No active dedicated lobby is running for lobby code '{lobby_code}'. Create the lobby first."

    return "No active dedicated lobby is running. Create the lobby first."


def build_pending_instance_message(target_context):
    lobby_code = target_context.get("lobbyCode")
    if lobby_code:
        return f"Dedicated lobby VM allocation exists for lobby {lobby_code}. Waiting for Compute Engine provisioning."

    return "Dedicated lobby VM allocation exists. Waiting for Compute Engine provisioning."


def normalize_allocation_mode(raw_value):
    candidate = str(raw_value or "").strip().lower()
    if candidate in ("single-instance", "single_instance", "single"):
        return "single-instance"

    if candidate in ("per-lobby-template", "per_lobby_template", "per-lobby"):
        return "per-lobby-template"

    return ""


def normalize_allocation_store(raw_value):
    candidate = str(raw_value or "").strip().lower()
    if candidate in ("firestore", "memory"):
        return candidate

    return ""


def get_server_allocation_mode():
    return normalize_allocation_mode(SERVER_ALLOCATION_MODE) or "single-instance"


def get_lobby_allocation_store():
    return normalize_allocation_store(SERVER_ALLOCATION_STORE) or "firestore"


def build_target_context(instance_name, target_id, lobby_code="", error=""):
    resolved_target_id = normalize_target_id(target_id) or normalize_target_id(instance_name)
    return {
        "instanceName": str(instance_name or "").strip(),
        "targetId": resolved_target_id,
        "allocationMode": get_server_allocation_mode(),
        "lobbyCode": sanitize_lobby_code(lobby_code),
        "error": str(error or "").strip(),
    }


def get_default_target_context():
    return build_target_context(GCP_INSTANCE_NAME, TARGET_INSTANCE_ID)


def allocate_target_context_for_lobby(lobby_code):
    lobby_code = sanitize_lobby_code(lobby_code)
    if not lobby_code:
        raise ValueError("Lobby code is required to allocate a dedicated lobby VM.")

    existing_record = get_lobby_allocation(lobby_code)
    if existing_record is not None:
        return build_target_context(existing_record.get("instanceName"), existing_record.get("targetId"), lobby_code)

    instance_name = build_managed_instance_name(lobby_code)
    target_context = build_target_context(instance_name, instance_name, lobby_code)
    upsert_lobby_allocation(lobby_code, target_context, status="allocating")
    return target_context


def log_firestore_warning(message):
    global firestore_warning_logged
    if firestore_warning_logged:
        return

    app.logger.warning(message)
    firestore_warning_logged = True


def get_firestore_client():
    global firestore_client

    if get_lobby_allocation_store() != "firestore":
        return None

    if firestore is None:
        log_firestore_warning("google-cloud-firestore is not installed. Falling back to in-memory lobby allocation storage.")
        return None

    if firestore_client is not None:
        return firestore_client

    try:
        client_kwargs = {"project": GCP_PROJECT_ID} if GCP_PROJECT_ID else {}
        firestore_client = firestore.Client(**client_kwargs)
        return firestore_client
    except Exception as error:
        log_firestore_warning(f"Firestore client initialization failed ({type(error).__name__}). Falling back to in-memory lobby allocation storage.")
        return None


def get_lobby_allocation_collection_ref():
    collection_name = str(LOBBY_ALLOCATION_COLLECTION or "").strip()
    if not collection_name:
        return None

    client = get_firestore_client()
    if client is None:
        return None

    return client.collection(collection_name)


def normalize_lobby_allocation_record(raw_record):
    if not isinstance(raw_record, dict):
        return None

    lobby_code = sanitize_lobby_code(raw_record.get("lobbyCode"))
    target_id = normalize_target_id(raw_record.get("targetId") or raw_record.get("instanceId"))
    instance_name = normalize_target_id(raw_record.get("instanceName") or target_id)
    if not lobby_code or not target_id:
        return None

    now = int(time.time())
    return {
        "lobbyCode": lobby_code,
        "targetId": target_id,
        "instanceId": target_id,
        "instanceName": instance_name or target_id,
        "allocationMode": normalize_allocation_mode(raw_record.get("allocationMode")) or get_server_allocation_mode(),
        "ownerSubject": str(raw_record.get("ownerSubject") or "").strip(),
        "status": str(raw_record.get("status") or "assigned").strip().lower() or "assigned",
        "createdAtUnix": parse_int(raw_record.get("createdAtUnix"), now),
        "updatedAtUnix": parse_int(raw_record.get("updatedAtUnix"), now),
    }


def get_lobby_allocation(lobby_code):
    lobby_code = sanitize_lobby_code(lobby_code)
    if not lobby_code:
        return None

    collection_ref = get_lobby_allocation_collection_ref()
    if collection_ref is not None:
        try:
            snapshot = collection_ref.document(lobby_code).get()
            if snapshot.exists:
                record = normalize_lobby_allocation_record(snapshot.to_dict())
                if record is not None:
                    return record
        except Exception as error:
            log_firestore_warning(f"Firestore lobby allocation read failed ({type(error).__name__}). Falling back to in-memory lobby allocation storage.")

    return normalize_lobby_allocation_record(lobby_allocation_registry.get(lobby_code))


def upsert_lobby_allocation(lobby_code, target_context, owner_subject="", status="assigned"):
    lobby_code = sanitize_lobby_code(lobby_code)
    if not lobby_code:
        return None

    target_context = target_context or get_default_target_context()
    existing_record = get_lobby_allocation(lobby_code) or {}
    now = int(time.time())
    record = {
        "lobbyCode": lobby_code,
        "targetId": target_context["targetId"],
        "instanceId": target_context["targetId"],
        "instanceName": target_context["instanceName"] or target_context["targetId"],
        "allocationMode": target_context["allocationMode"],
        "ownerSubject": str(owner_subject or existing_record.get("ownerSubject") or "").strip(),
        "status": str(status or existing_record.get("status") or "assigned").strip().lower() or "assigned",
        "createdAtUnix": parse_int(existing_record.get("createdAtUnix"), now),
        "updatedAtUnix": now,
    }

    collection_ref = get_lobby_allocation_collection_ref()
    if collection_ref is not None:
        try:
            collection_ref.document(lobby_code).set(record)
            return record
        except Exception as error:
            log_firestore_warning(f"Firestore lobby allocation write failed ({type(error).__name__}). Falling back to in-memory lobby allocation storage.")

    lobby_allocation_registry[lobby_code] = record
    return record


def resolve_request_value(request_body, *keys):
    for key in keys:
        query_value = request.args.get(key)
        if query_value is not None and str(query_value).strip():
            return query_value

        if request_body:
            body_value = request_body.get(key)
            if body_value is not None and str(body_value).strip():
                return body_value

    return ""


def resolve_target_context(request_body=None, create_if_missing=False):
    default_target_context = get_default_target_context()
    requested_lobby_code = sanitize_lobby_code(resolve_request_value(request_body, "lobbyCode"))

    if get_server_allocation_mode() == "single-instance":
        return build_target_context(default_target_context["instanceName"], default_target_context["targetId"], requested_lobby_code)

    requested_target_id = normalize_target_id(resolve_request_value(request_body, "targetId", "instanceId"))
    if requested_target_id:
        return build_target_context(requested_target_id, requested_target_id, requested_lobby_code)

    if requested_lobby_code:
        lobby_allocation = get_lobby_allocation(requested_lobby_code)
        if lobby_allocation is not None:
            return build_target_context(lobby_allocation.get("instanceName"), lobby_allocation.get("targetId"), requested_lobby_code)

        requested_lobby_action = sanitize_lobby_action(resolve_request_value(request_body, "lobbyAction"))
        if create_if_missing and requested_lobby_action == "create":
            return allocate_target_context_for_lobby(requested_lobby_code)

        return build_target_context("", "", requested_lobby_code, build_missing_lobby_allocation_message(requested_lobby_code))

    return default_target_context


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