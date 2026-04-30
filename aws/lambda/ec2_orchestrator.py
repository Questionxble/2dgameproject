import base64
import hashlib
import hmac
import json
import os
import secrets
import time

import boto3
from botocore.exceptions import ClientError


ec2 = boto3.client("ec2")

INSTANCE_ID = os.environ.get("INSTANCE_ID", "")
SERVER_PORT = int(os.environ.get("SERVER_PORT", "7777"))
MAX_PLAYERS = int(os.environ.get("MAX_PLAYERS", "4"))
CORS_ORIGIN = os.environ.get("CORS_ORIGIN", "*")
SERVER_ADDRESS_OVERRIDE = os.environ.get("SERVER_ADDRESS_OVERRIDE", "")
SERVER_WARMUP_SECONDS = float(os.environ.get("SERVER_WARMUP_SECONDS", "8"))
REQUIRE_INSTANCE_STATUS_OK = os.environ.get("REQUIRE_INSTANCE_STATUS_OK", "true").lower() == "true"
JOIN_TICKET_SECRET = os.environ.get("JOIN_TICKET_SECRET", "")
JOIN_TICKET_TTL_SECONDS = int(os.environ.get("JOIN_TICKET_TTL_SECONDS", "90"))
JOIN_TICKET_SCOPE = os.environ.get("JOIN_TICKET_SCOPE", "join")
ALLOW_ANONYMOUS_JOIN_TICKETS = os.environ.get("ALLOW_ANONYMOUS_JOIN_TICKETS", "false").lower() == "true"
COGNITO_REQUIRED_TOKEN_USE = os.environ.get("COGNITO_REQUIRED_TOKEN_USE", "")
COGNITO_REQUIRED_GROUP = os.environ.get("COGNITO_REQUIRED_GROUP", "")
COGNITO_REQUIRED_SCOPE = os.environ.get("COGNITO_REQUIRED_SCOPE", "")
COGNITO_EXPECTED_AUDIENCE = os.environ.get("COGNITO_EXPECTED_AUDIENCE", "")
MAX_PLAYER_NAME_LENGTH = 24


def lambda_handler(event, _context):
    method = get_http_method(event)

    if method == "OPTIONS":
        return respond(200, {"ok": True})

    if not INSTANCE_ID:
        return respond(500, {"ok": False, "message": "INSTANCE_ID is not configured."})

    action = get_action(event)

    try:
        if action == "start":
            return handle_start()

        if action == "status":
            return handle_status()

        if action == "join-token":
            return handle_join_token(event)
    except ClientError as error:
        return respond(500, {"ok": False, "message": error.response.get("Error", {}).get("Message", str(error))})
    except RuntimeError as error:
        return respond(500, {"ok": False, "message": str(error)})

    return respond(404, {"ok": False, "message": f"Unsupported action: {action}"})


def handle_start():
    instance = describe_instance(INSTANCE_ID)
    current_state = get_instance_state(instance)

    if current_state == "stopped":
        ec2.start_instances(InstanceIds=[INSTANCE_ID])
        instance = describe_instance(INSTANCE_ID)
    elif current_state == "stopping":
        return respond(409, build_status_payload(instance, False, "Instance is still stopping. Try again in a few seconds."))

    instance_status_ok = get_instance_status_ok(instance)
    return respond(200, build_status_payload(instance, instance_status_ok, build_status_message(instance, instance_status_ok)))


def handle_status():
    instance = describe_instance(INSTANCE_ID)
    instance_status_ok = get_instance_status_ok(instance)
    return respond(200, build_status_payload(instance, instance_status_ok, build_status_message(instance, instance_status_ok)))


def handle_join_token(event):
    if not JOIN_TICKET_SECRET:
        return respond(500, {"ok": False, "message": "JOIN_TICKET_SECRET is not configured."})

    auth_claims = get_authorizer_claims(event)
    authorization_error, subject = authorize_join_request(auth_claims)
    if authorization_error:
        return respond(authorization_error[0], {"ok": False, "message": authorization_error[1]})

    instance = describe_instance(INSTANCE_ID)
    instance_status_ok = get_instance_status_ok(instance)
    connection_address = resolve_connection_address(instance)

    if get_instance_state(instance) != "running" or not instance_status_ok or not connection_address:
        return respond(409, build_status_payload(instance, instance_status_ok, "Game server is not ready to issue join tickets yet."))

    request_body = parse_body(event)
    player_name = sanitize_player_name(
        request_body.get("playerName")
        or auth_claims.get("preferred_username")
        or auth_claims.get("cognito:username")
        or auth_claims.get("username")
        or "player"
    )

    join_token, expires_at_unix = build_join_token(subject, player_name)

    return respond(
        200,
        {
            "ok": True,
            "instanceId": INSTANCE_ID,
            "connectionAddress": connection_address,
            "port": SERVER_PORT,
            "playerName": player_name,
            "joinToken": join_token,
            "expiresAtUnix": expires_at_unix,
            "message": "Join ticket issued.",
        },
    )


def describe_instance(instance_id):
    response = ec2.describe_instances(InstanceIds=[instance_id])
    reservations = response.get("Reservations", [])

    if not reservations or not reservations[0].get("Instances"):
        raise RuntimeError(f"Instance {instance_id} was not found.")

    return reservations[0]["Instances"][0]


def get_instance_status_ok(instance):
    if get_instance_state(instance) != "running":
        return False

    if not REQUIRE_INSTANCE_STATUS_OK:
        return True

    response = ec2.describe_instance_status(InstanceIds=[INSTANCE_ID], IncludeAllInstances=True)
    statuses = response.get("InstanceStatuses", [])
    if not statuses:
        return False

    status = statuses[0]
    return (
        status.get("InstanceStatus", {}).get("Status") == "ok"
        and status.get("SystemStatus", {}).get("Status") == "ok"
    )


def build_status_payload(instance, instance_status_ok, message):
    state = get_instance_state(instance)
    connection_address = resolve_connection_address(instance)
    is_ready = state == "running" and instance_status_ok and bool(connection_address)

    return {
        "ok": True,
        "instanceId": instance.get("InstanceId", INSTANCE_ID),
        "instanceState": state,
        "publicIpAddress": instance.get("PublicIpAddress", ""),
        "publicDnsName": instance.get("PublicDnsName", ""),
        "connectionAddress": connection_address,
        "port": SERVER_PORT,
        "maxPlayers": MAX_PLAYERS,
        "instanceStatusOk": instance_status_ok,
        "serverWarmupSeconds": SERVER_WARMUP_SECONDS,
        "isReady": is_ready,
        "message": message,
    }


def build_status_message(instance, instance_status_ok):
    state = get_instance_state(instance)

    if state == "stopped":
        return "EC2 instance is stopped. Start requested or retry needed."

    if state == "pending":
        return "EC2 instance is booting. Waiting for AWS health checks to pass."

    if state == "running":
        if REQUIRE_INSTANCE_STATUS_OK and not instance_status_ok:
            return "EC2 instance is running, but AWS status checks are still pending."

        if SERVER_WARMUP_SECONDS > 0:
            return f"EC2 instance is ready. Waiting {SERVER_WARMUP_SECONDS:.0f}s before client connect is recommended."

        return "EC2 instance is ready for client connection."

    return f"EC2 instance state: {state}"


def resolve_connection_address(instance):
    return SERVER_ADDRESS_OVERRIDE or instance.get("PublicDnsName") or instance.get("PublicIpAddress", "")


def build_join_token(subject, player_name):
    expires_at_unix = int(time.time()) + max(15, JOIN_TICKET_TTL_SECONDS)
    payload = {
        "sub": subject,
        "pn": player_name,
        "scp": JOIN_TICKET_SCOPE,
        "iid": INSTANCE_ID,
        "nonce": secrets.token_urlsafe(8),
        "exp": expires_at_unix,
    }
    payload_json = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    payload_encoded = base64url_encode(payload_json)
    signature = hmac.new(JOIN_TICKET_SECRET.encode("utf-8"), payload_encoded.encode("utf-8"), hashlib.sha256).digest()
    return f"{payload_encoded}.{base64url_encode(signature)}", expires_at_unix


def authorize_join_request(claims):
    if not claims:
        if ALLOW_ANONYMOUS_JOIN_TICKETS:
            return None, f"anonymous:{secrets.token_hex(6)}"

        return (401, "Authenticated identity is required to obtain a join token."), None

    subject = claims.get("sub")
    if not subject:
        return (401, "Authenticated identity is missing the sub claim."), None

    if COGNITO_REQUIRED_TOKEN_USE and claims.get("token_use") != COGNITO_REQUIRED_TOKEN_USE:
        return (403, f"JWT token_use must be {COGNITO_REQUIRED_TOKEN_USE}."), None

    if COGNITO_EXPECTED_AUDIENCE:
        audience = claims.get("aud") or claims.get("client_id")
        if audience != COGNITO_EXPECTED_AUDIENCE:
            return (403, "JWT audience is invalid."), None

    if COGNITO_REQUIRED_SCOPE and not has_required_scope(claims, COGNITO_REQUIRED_SCOPE):
        return (403, f"JWT is missing required scope: {COGNITO_REQUIRED_SCOPE}."), None

    if COGNITO_REQUIRED_GROUP and not has_required_group(claims, COGNITO_REQUIRED_GROUP):
        return (403, f"JWT is missing required group: {COGNITO_REQUIRED_GROUP}."), None

    return None, subject


def has_required_scope(claims, required_scope):
    scopes = claims.get("scope") or claims.get("scp") or ""
    if isinstance(scopes, list):
        values = scopes
    else:
        values = str(scopes).split()

    return required_scope in values


def has_required_group(claims, required_group):
    groups = claims.get("cognito:groups") or claims.get("groups") or []

    if isinstance(groups, str):
        candidate = groups.strip()
        if candidate.startswith("["):
            try:
                groups = json.loads(candidate)
            except json.JSONDecodeError:
                groups = [item.strip() for item in candidate.split(",") if item.strip()]
        else:
            groups = [item.strip() for item in candidate.split(",") if item.strip()]

    return required_group in groups


def get_authorizer_claims(event):
    request_context = event.get("requestContext", {})
    authorizer = request_context.get("authorizer", {})

    jwt_claims = authorizer.get("jwt", {}).get("claims")
    if jwt_claims:
        return jwt_claims

    legacy_claims = authorizer.get("claims")
    if legacy_claims:
        return legacy_claims

    return {}


def sanitize_player_name(raw_value):
    candidate = (raw_value or "player").strip()
    if not candidate:
        candidate = "player"

    if len(candidate) > MAX_PLAYER_NAME_LENGTH:
        candidate = candidate[:MAX_PLAYER_NAME_LENGTH]

    return candidate


def get_instance_state(instance):
    return instance.get("State", {}).get("Name", "unknown")


def get_http_method(event):
    request_context = event.get("requestContext", {})
    http = request_context.get("http", {})
    return http.get("method") or event.get("httpMethod") or "GET"


def get_action(event):
    raw_path = event.get("rawPath") or event.get("path") or ""
    lowered_path = raw_path.lower()

    if lowered_path.endswith("/start"):
        return "start"

    if lowered_path.endswith("/status"):
        return "status"

    if lowered_path.endswith("/join-token"):
        return "join-token"

    query = event.get("queryStringParameters") or {}
    if query.get("action"):
        return query["action"].lower()

    body = parse_body(event)
    if body.get("action"):
        return str(body["action"]).lower()

    return "status"


def parse_body(event):
    body = event.get("body")
    if not body:
        return {}

    if event.get("isBase64Encoded"):
        return {}

    try:
        return json.loads(body)
    except json.JSONDecodeError:
        return {}


def base64url_encode(raw_bytes):
    return base64.urlsafe_b64encode(raw_bytes).rstrip(b"=").decode("ascii")


def respond(status_code, body):
    return {
        "statusCode": status_code,
        "headers": {
            "Content-Type": "application/json",
            "Access-Control-Allow-Origin": CORS_ORIGIN,
            "Access-Control-Allow-Headers": "Content-Type,Authorization,X-Api-Key",
            "Access-Control-Allow-Methods": "OPTIONS,GET,POST",
        },
        "body": json.dumps(body),
    }