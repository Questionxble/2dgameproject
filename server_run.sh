#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

load_runtime_environment() {
    local runtime_env_file="${GAME_SERVER_RUNTIME_ENV_FILE_PATH:-}"
    if [ -z "${runtime_env_file}" ] || [ ! -r "${runtime_env_file}" ]; then
        return 0
    fi

    set -a
    # shellcheck disable=SC1090
    . "${runtime_env_file}"
    set +a
}

load_runtime_environment

resolve_default_executable() {
    local candidates=(
        "${SCRIPT_DIR}/2dgameproject_server"
        "${SCRIPT_DIR}/LinuxServerBuild.x86_64"
        "/home/ubuntu/LinuxServerBuild/2dgameproject_server"
        "/home/ubuntu/LinuxBuildFiles/LinuxServerBuild.x86_64"
    )

    for candidate in "${candidates[@]}"; do
        if [ -f "${candidate}" ]; then
            echo "${candidate}"
            return 0
        fi
    done

    echo "${SCRIPT_DIR}/2dgameproject_server"
}

GAME_EXECUTABLE="${GAME_EXECUTABLE:-$(resolve_default_executable)}"
SERVER_PORT="${SERVER_PORT:-7777}"
MAX_PLAYERS="${MAX_PLAYERS:-4}"
LOG_FILE="${LOG_FILE:-${SCRIPT_DIR}/server.log}"
IDLE_STOP_MARKER_FILE="${IDLE_STOP_MARKER_FILE:-${SCRIPT_DIR}/.unity-game-server-stop-vm}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        -port|--port)
            SERVER_PORT="$2"
            shift 2
            ;;
        -maxplayers|--max-players)
            MAX_PLAYERS="$2"
            shift 2
            ;;
        -executable|--executable)
            GAME_EXECUTABLE="$2"
            shift 2
            ;;
        -logfile|--log-file)
            LOG_FILE="$2"
            shift 2
            ;;
        --idle-stop-marker)
            IDLE_STOP_MARKER_FILE="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -port, --port PORT             Server port (default: 7777)"
            echo "  -maxplayers, --max-players NUM Max players (default: 4)"
            echo "  -executable, --executable PATH Linux server executable path"
            echo "  -logfile, --log-file PATH      Log file path"
            echo "  --idle-stop-marker PATH        Marker file cleared before server startup"
            echo "  -h, --help                     Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information."
            exit 1
            ;;
    esac
done

if [ ! -f "${GAME_EXECUTABLE}" ]; then
    echo "Error: Unity server executable was not found at ${GAME_EXECUTABLE}"
    exit 1
fi

mkdir -p "$(dirname "${LOG_FILE}")"
touch "${LOG_FILE}"
chmod +x "${GAME_EXECUTABLE}"
rm -f "${IDLE_STOP_MARKER_FILE}"
export SERVER_PORT
export MAX_PLAYERS

echo "=== Unity Dedicated Server Runner ==="
echo "Executable : ${GAME_EXECUTABLE}"
echo "Port       : ${SERVER_PORT}"
echo "Max Players: ${MAX_PLAYERS}"
echo "Log File   : ${LOG_FILE}"
echo "Runtime Env: ${GAME_SERVER_RUNTIME_ENV_FILE_PATH:-"(not set)"}"
echo "Idle Marker: ${IDLE_STOP_MARKER_FILE}"
echo "====================================="

exec "${GAME_EXECUTABLE}" \
    -logFile "${LOG_FILE}"