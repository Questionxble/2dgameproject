#!/usr/bin/env bash

set -euo pipefail

RUNTIME_ENV_FILE_PATH="${RUNTIME_ENV_FILE_PATH:-${GAME_SERVER_RUNTIME_ENV_FILE_PATH:-}}"
RUNTIME_ENV_OWNER="${RUNTIME_ENV_OWNER:-}"
METADATA_HOST="${METADATA_HOST:-${GCE_METADATA_HOST:-http://metadata.google.internal/computeMetadata/v1}}"
TARGET_ID_KEY="${TARGET_ID_KEY:-${GCE_METADATA_TARGET_ID_KEY:-game-server-target-id}}"
LOBBY_CODE_KEY="${LOBBY_CODE_KEY:-${GCE_METADATA_LOBBY_CODE_KEY:-game-server-lobby-code}}"
TARGET_ID_ENV_KEY="${TARGET_ID_ENV_KEY:-GAME_SERVER_TARGET_ID}"
LEGACY_TARGET_ID_ENV_KEY="${LEGACY_TARGET_ID_ENV_KEY:-GAME_SERVER_INSTANCE_ID}"
LOBBY_CODE_ENV_KEY="${LOBBY_CODE_ENV_KEY:-GAME_SERVER_ALLOCATED_LOBBY_CODE}"

log() {
    echo "[gce-metadata-sync] $*" >&2
}

fetch_metadata_value() {
    local metadata_path="$1"
    local metadata_url="${METADATA_HOST%/}/${metadata_path}"

    if command -v curl >/dev/null 2>&1; then
        curl -fsS --connect-timeout 2 --max-time 5 \
            -H "Metadata-Flavor: Google" \
            "${metadata_url}"
        return 0
    fi

    if command -v wget >/dev/null 2>&1; then
        wget -qO- --timeout=5 \
            --header="Metadata-Flavor: Google" \
            "${metadata_url}"
        return 0
    fi

    return 127
}

install_runtime_env_file() {
    local source_file="$1"
    local runtime_env_dir
    local owner_group

    runtime_env_dir="$(dirname "${RUNTIME_ENV_FILE_PATH}")"
    mkdir -p "${runtime_env_dir}"

    if [ "$(id -u)" -eq 0 ] && [ -n "${RUNTIME_ENV_OWNER}" ] && id -u "${RUNTIME_ENV_OWNER}" >/dev/null 2>&1; then
        owner_group="$(id -gn "${RUNTIME_ENV_OWNER}")"
        install -o "${RUNTIME_ENV_OWNER}" -g "${owner_group}" -m 600 "${source_file}" "${RUNTIME_ENV_FILE_PATH}"
        return 0
    fi

    install -m 600 "${source_file}" "${RUNTIME_ENV_FILE_PATH}"
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --runtime-env-file)
            RUNTIME_ENV_FILE_PATH="$2"
            shift 2
            ;;
        --runtime-env-owner)
            RUNTIME_ENV_OWNER="$2"
            shift 2
            ;;
        --metadata-host)
            METADATA_HOST="$2"
            shift 2
            ;;
        --target-id-key)
            TARGET_ID_KEY="$2"
            shift 2
            ;;
        --lobby-code-key)
            LOBBY_CODE_KEY="$2"
            shift 2
            ;;
        --target-id-env-key)
            TARGET_ID_ENV_KEY="$2"
            shift 2
            ;;
        --legacy-target-id-env-key)
            LEGACY_TARGET_ID_ENV_KEY="$2"
            shift 2
            ;;
        --lobby-code-env-key)
            LOBBY_CODE_ENV_KEY="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  --runtime-env-file PATH        Override env file that the Unity service entrypoint reads"
            echo "  --runtime-env-owner USER       Linux user that should own the runtime env file"
            echo "  --metadata-host URL            GCE metadata host (default: http://metadata.google.internal/computeMetadata/v1)"
            echo "  --target-id-key KEY            Metadata attribute key for the target id"
            echo "  --lobby-code-key KEY           Metadata attribute key for the lobby code"
            echo "  --target-id-env-key KEY        Env variable name for the target id override"
            echo "  --legacy-target-id-env-key KEY Env variable name for the legacy target id override"
            echo "  --lobby-code-env-key KEY       Env variable name for the lobby code override"
            exit 0
            ;;
        *)
            log "Unknown option: $1"
            exit 1
            ;;
    esac
done

if [ -z "${RUNTIME_ENV_FILE_PATH}" ]; then
    log "No runtime env file path was configured. Skipping metadata sync."
    exit 0
fi

target_id=""
lobby_code=""

if target_id="$(fetch_metadata_value "instance/attributes/${TARGET_ID_KEY}" 2>/dev/null)"; then
    :
else
    metadata_status=$?
    if [ "${metadata_status}" -eq 127 ]; then
        log "Neither curl nor wget is available. Skipping metadata sync."
    fi

    log "No GCE target id metadata override was found. Keeping the existing runtime env file."
    exit 0
fi

target_id="$(printf '%s' "${target_id}" | tr -d '\r\n')"
if [ -z "${target_id}" ]; then
    log "The GCE target id metadata override was empty. Keeping the existing runtime env file."
    exit 0
fi

if lobby_code="$(fetch_metadata_value "instance/attributes/${LOBBY_CODE_KEY}" 2>/dev/null)"; then
    lobby_code="$(printf '%s' "${lobby_code}" | tr -d '\r\n')"
else
    lobby_code=""
fi

temp_file="$(mktemp)"
trap 'rm -f "${temp_file}"' EXIT

printf '%s=%s\n' "${TARGET_ID_ENV_KEY}" "${target_id}" > "${temp_file}"
printf '%s=%s\n' "${LEGACY_TARGET_ID_ENV_KEY}" "${target_id}" >> "${temp_file}"

if [ -n "${lobby_code}" ]; then
    printf '%s=%s\n' "${LOBBY_CODE_ENV_KEY}" "${lobby_code}" >> "${temp_file}"
fi

install_runtime_env_file "${temp_file}"
log "Updated runtime env overrides for target id ${target_id}."