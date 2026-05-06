#!/usr/bin/env bash

set -euo pipefail

log() {
    echo "[deploy-server-template] $*"
}

fail() {
    echo "[deploy-server-template] ERROR: $*" >&2
    exit 1
}

require_env() {
    local name="$1"
    if [ -z "${!name:-}" ]; then
        fail "Required environment variable '${name}' is missing."
    fi
}

is_true() {
    case "${1:-}" in
        1|true|TRUE|True|yes|YES|on|ON)
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

secret_value_or_empty() {
    local secret_name="$1"
    if [ -z "${secret_name}" ]; then
        return 0
    fi

    gcloud secrets versions access latest --secret="${secret_name}" 2>/dev/null || true
}

wait_for_instance_status() {
    local desired_status="$1"
    local attempt

    for attempt in $(seq 1 "${INSTANCE_WAIT_ATTEMPTS}"); do
        local current_status
        current_status="$(gcloud compute instances describe "${GCP_SOURCE_INSTANCE_NAME}" \
            --project "${GCP_PROJECT_ID}" \
            --zone "${GCP_ZONE}" \
            --format='get(status)')"

        if [ "${current_status}" = "${desired_status}" ]; then
            log "Instance ${GCP_SOURCE_INSTANCE_NAME} reached status ${desired_status}."
            return 0
        fi

        log "Waiting for instance ${GCP_SOURCE_INSTANCE_NAME} to reach ${desired_status} (current: ${current_status}, attempt ${attempt}/${INSTANCE_WAIT_ATTEMPTS})."
        sleep "${INSTANCE_WAIT_SECONDS}"
    done

    fail "Instance ${GCP_SOURCE_INSTANCE_NAME} did not reach status ${desired_status}."
}

wait_for_ssh() {
    local attempt

    for attempt in $(seq 1 "${SSH_WAIT_ATTEMPTS}"); do
        if gcloud compute ssh "${GCP_SOURCE_INSTANCE_NAME}" \
            --project "${GCP_PROJECT_ID}" \
            --zone "${GCP_ZONE}" \
            --quiet \
            --command 'echo ssh-ready' >/dev/null 2>&1; then
            log "SSH is ready on ${GCP_SOURCE_INSTANCE_NAME}."
            return 0
        fi

        log "Waiting for SSH on ${GCP_SOURCE_INSTANCE_NAME} (attempt ${attempt}/${SSH_WAIT_ATTEMPTS})."
        sleep "${SSH_WAIT_SECONDS}"
    done

    fail "SSH did not become ready on ${GCP_SOURCE_INSTANCE_NAME}."
}

shell_quote() {
    printf '%q' "$1"
}

require_env GCP_PROJECT_ID
require_env GCP_ZONE
require_env GCP_SOURCE_INSTANCE_NAME
require_env GCP_CLOUD_RUN_SERVICE
require_env GCP_CLOUD_RUN_REGION
require_env SERVER_BUNDLE_ARCHIVE

VM_SERVICE_USER="${VM_SERVICE_USER:-chris}"
GCP_SSH_USER="${GCP_SSH_USER:-${VM_SERVICE_USER}}"
VM_WORKING_DIR="${VM_WORKING_DIR:-/home/${VM_SERVICE_USER}/LinuxBuildFiles}"
VM_EXECUTABLE_PATH="${VM_EXECUTABLE_PATH:-${VM_WORKING_DIR}/LinuxServerBuild.x86_64}"
VM_SERVICE_NAME="${VM_SERVICE_NAME:-unity-game-server}"
GCP_TARGET_INSTANCE_ID="${GCP_TARGET_INSTANCE_ID:-${GCP_SOURCE_INSTANCE_NAME}}"
GCP_TEMPLATE_NAME_PREFIX="${GCP_TEMPLATE_NAME_PREFIX:-unity-server-template}"
SERVER_PORT="${SERVER_PORT:-7777}"
MAX_PLAYERS="${MAX_PLAYERS:-4}"
IDLE_TIMEOUT_SECONDS="${IDLE_TIMEOUT_SECONDS:-300}"
GAME_TRANSPORT_MODE="${GAME_TRANSPORT_MODE:-relay}"
RELAY_CONNECTION_TYPE="${RELAY_CONNECTION_TYPE:-dtls}"
SERVER_RUNTIME_STATUS_ENDPOINT="${SERVER_RUNTIME_STATUS_ENDPOINT:-/server/runtime}"
JOIN_TICKET_SECRET_SECRET_NAME="${JOIN_TICKET_SECRET_SECRET_NAME:-join-ticket-secret}"
SERVER_REGISTRATION_TOKEN_SECRET_NAME="${SERVER_REGISTRATION_TOKEN_SECRET_NAME:-server-registration-token}"
STOP_SOURCE_INSTANCE_AFTER_DEPLOY="${STOP_SOURCE_INSTANCE_AFTER_DEPLOY:-true}"
DEPLOY_CLOUD_RUN_SOURCE="${DEPLOY_CLOUD_RUN_SOURCE:-false}"
CLOUD_RUN_SOURCE_DIR="${CLOUD_RUN_SOURCE_DIR:-$(pwd)/gcp/cloud_run}"
ALLOW_UNAUTHENTICATED_CLOUD_RUN="${ALLOW_UNAUTHENTICATED_CLOUD_RUN:-true}"
INSTANCE_WAIT_ATTEMPTS="${INSTANCE_WAIT_ATTEMPTS:-30}"
INSTANCE_WAIT_SECONDS="${INSTANCE_WAIT_SECONDS:-10}"
SSH_WAIT_ATTEMPTS="${SSH_WAIT_ATTEMPTS:-20}"
SSH_WAIT_SECONDS="${SSH_WAIT_SECONDS:-10}"

[ -f "${SERVER_BUNDLE_ARCHIVE}" ] || fail "Bundle archive was not found at ${SERVER_BUNDLE_ARCHIVE}."

JOIN_TICKET_SECRET="${JOIN_TICKET_SECRET:-$(secret_value_or_empty "${JOIN_TICKET_SECRET_SECRET_NAME}")}"
SERVER_REGISTRATION_TOKEN="${SERVER_REGISTRATION_TOKEN:-$(secret_value_or_empty "${SERVER_REGISTRATION_TOKEN_SECRET_NAME}")}"

if [ -z "${SERVER_REGISTRATION_TOKEN}" ]; then
    SERVER_REGISTRATION_TOKEN="${JOIN_TICKET_SECRET}"
fi

[ -n "${JOIN_TICKET_SECRET}" ] || fail "JOIN_TICKET_SECRET is empty and Secret Manager lookup failed."
[ -n "${SERVER_REGISTRATION_TOKEN}" ] || fail "SERVER_REGISTRATION_TOKEN is empty and no fallback value was available."

CURRENT_INSTANCE_STATUS="$(gcloud compute instances describe "${GCP_SOURCE_INSTANCE_NAME}" \
    --project "${GCP_PROJECT_ID}" \
    --zone "${GCP_ZONE}" \
    --format='get(status)')"

INSTANCE_STARTED_BY_WORKFLOW="false"

if [ "${CURRENT_INSTANCE_STATUS}" != "RUNNING" ]; then
    log "Starting source instance ${GCP_SOURCE_INSTANCE_NAME} from status ${CURRENT_INSTANCE_STATUS}."
    gcloud compute instances start "${GCP_SOURCE_INSTANCE_NAME}" \
        --project "${GCP_PROJECT_ID}" \
        --zone "${GCP_ZONE}" \
        --quiet
    INSTANCE_STARTED_BY_WORKFLOW="true"
fi

wait_for_instance_status RUNNING
wait_for_ssh

CLOUD_RUN_URL="$(gcloud run services describe "${GCP_CLOUD_RUN_SERVICE}" \
    --project "${GCP_PROJECT_ID}" \
    --region "${GCP_CLOUD_RUN_REGION}" \
    --format='value(status.url)')"

[ -n "${CLOUD_RUN_URL}" ] || fail "Cloud Run service URL could not be resolved for ${GCP_CLOUD_RUN_SERVICE}."

REMOTE_ARCHIVE_PATH="/tmp/$(basename "${SERVER_BUNDLE_ARCHIVE}")"

log "Uploading release bundle to ${GCP_SOURCE_INSTANCE_NAME}:${REMOTE_ARCHIVE_PATH}."
gcloud compute scp "${SERVER_BUNDLE_ARCHIVE}" "${GCP_SSH_USER}@${GCP_SOURCE_INSTANCE_NAME}:${REMOTE_ARCHIVE_PATH}" \
    --project "${GCP_PROJECT_ID}" \
    --zone "${GCP_ZONE}" \
    --quiet

REMOTE_COMMAND=$(cat <<EOF
set -euo pipefail
WORKING_DIR=$(shell_quote "${VM_WORKING_DIR}")
ARCHIVE_PATH=$(shell_quote "${REMOTE_ARCHIVE_PATH}")
mkdir -p "\${WORKING_DIR}"
find "\${WORKING_DIR}" -mindepth 1 -maxdepth 1 \
    ! -name 'server.log' \
    ! -name '.unity-game-server-runtime.env' \
    ! -name '.unity-game-server-stop-vm' \
    -exec rm -rf {} +
tar -xzf "\${ARCHIVE_PATH}" -C "\${WORKING_DIR}"
cd "\${WORKING_DIR}"
chmod +x server_run.sh server_start.sh install_server_service.sh sync_gce_instance_metadata.sh LinuxServerBuild.x86_64
./install_server_service.sh \
  --service-name $(shell_quote "${VM_SERVICE_NAME}") \
  --service-user $(shell_quote "${VM_SERVICE_USER}") \
  --working-dir $(shell_quote "${VM_WORKING_DIR}") \
  --executable $(shell_quote "${VM_EXECUTABLE_PATH}") \
  --port $(shell_quote "${SERVER_PORT}") \
  --max-players $(shell_quote "${MAX_PLAYERS}") \
  --idle-timeout $(shell_quote "${IDLE_TIMEOUT_SECONDS}") \
  --transport-mode $(shell_quote "${GAME_TRANSPORT_MODE}") \
  --relay-connection-type $(shell_quote "${RELAY_CONNECTION_TYPE}") \
  --orchestration-url $(shell_quote "${CLOUD_RUN_URL}") \
  --runtime-status-endpoint $(shell_quote "${SERVER_RUNTIME_STATUS_ENDPOINT}") \
  --join-ticket-secret $(shell_quote "${JOIN_TICKET_SECRET}") \
  --server-registration-token $(shell_quote "${SERVER_REGISTRATION_TOKEN}") \
  --target-id $(shell_quote "${GCP_TARGET_INSTANCE_ID}")
rm -f "\${ARCHIVE_PATH}"
EOF
)

log "Installing release bundle and refreshing service on ${GCP_SOURCE_INSTANCE_NAME}."
gcloud compute ssh "${GCP_SOURCE_INSTANCE_NAME}" \
    --project "${GCP_PROJECT_ID}" \
    --zone "${GCP_ZONE}" \
    --quiet \
    --command "${REMOTE_COMMAND}"

if [ -n "${GCP_TEMPLATE_NAME:-}" ]; then
    TEMPLATE_NAME="${GCP_TEMPLATE_NAME}"
else
    TEMPLATE_NAME="${GCP_TEMPLATE_NAME_PREFIX}-$(date -u +%Y%m%d-%H%M%S)-r${GITHUB_RUN_NUMBER:-manual}"
fi

TEMPLATE_NAME="$(printf '%s' "${TEMPLATE_NAME}" | tr '[:upper:]' '[:lower:]' | tr -cd 'a-z0-9-')"
TEMPLATE_NAME="${TEMPLATE_NAME:0:62}"
TEMPLATE_RESOURCE="global/instanceTemplates/${TEMPLATE_NAME}"

log "Creating instance template ${TEMPLATE_RESOURCE}."
gcloud compute instance-templates create "${TEMPLATE_NAME}" \
    --project "${GCP_PROJECT_ID}" \
    --source-instance "${GCP_SOURCE_INSTANCE_NAME}" \
    --source-instance-zone "${GCP_ZONE}" \
    --quiet

if is_true "${DEPLOY_CLOUD_RUN_SOURCE}"; then
    [ -d "${CLOUD_RUN_SOURCE_DIR}" ] || fail "Cloud Run source directory ${CLOUD_RUN_SOURCE_DIR} was not found."

    log "Deploying Cloud Run source from ${CLOUD_RUN_SOURCE_DIR}."

    DEPLOY_ARGS=(
        run deploy "${GCP_CLOUD_RUN_SERVICE}"
        --project "${GCP_PROJECT_ID}"
        --region "${GCP_CLOUD_RUN_REGION}"
        --source "${CLOUD_RUN_SOURCE_DIR}"
        --quiet
    )

    if is_true "${ALLOW_UNAUTHENTICATED_CLOUD_RUN}"; then
        DEPLOY_ARGS+=(--allow-unauthenticated)
    fi

    if [ -n "${JOIN_TICKET_SECRET_SECRET_NAME}" ]; then
        DEPLOY_ARGS+=(--update-secrets "JOIN_TICKET_SECRET=${JOIN_TICKET_SECRET_SECRET_NAME}:latest,SERVER_REGISTRATION_TOKEN=${SERVER_REGISTRATION_TOKEN_SECRET_NAME}:latest")
    fi

    gcloud "${DEPLOY_ARGS[@]}"
fi

log "Updating Cloud Run service ${GCP_CLOUD_RUN_SERVICE} to use ${TEMPLATE_RESOURCE}."
gcloud run services update "${GCP_CLOUD_RUN_SERVICE}" \
    --project "${GCP_PROJECT_ID}" \
    --region "${GCP_CLOUD_RUN_REGION}" \
    --update-env-vars "MANAGED_INSTANCE_TEMPLATE=${TEMPLATE_RESOURCE}" \
    --quiet

if is_true "${STOP_SOURCE_INSTANCE_AFTER_DEPLOY}" && is_true "${INSTANCE_STARTED_BY_WORKFLOW}"; then
    log "Stopping source instance ${GCP_SOURCE_INSTANCE_NAME} because the workflow started it."
    gcloud compute instances stop "${GCP_SOURCE_INSTANCE_NAME}" \
        --project "${GCP_PROJECT_ID}" \
        --zone "${GCP_ZONE}" \
        --quiet
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
    {
        echo "template_name=${TEMPLATE_NAME}"
        echo "template_resource=${TEMPLATE_RESOURCE}"
        echo "cloud_run_url=${CLOUD_RUN_URL}"
    } >> "${GITHUB_OUTPUT}"
fi

log "Deployment finished. New template: ${TEMPLATE_RESOURCE}."