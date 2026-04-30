#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVICE_NAME="${SERVICE_NAME:-unity-game-server}"
SERVICE_USER="${SERVICE_USER:-ubuntu}"
WORKING_DIR="${WORKING_DIR:-${SCRIPT_DIR}}"
EXECUTABLE_PATH="${EXECUTABLE_PATH:-${WORKING_DIR}/2dgameproject_server}"
SERVER_PORT="${SERVER_PORT:-7777}"
MAX_PLAYERS="${MAX_PLAYERS:-4}"
LOG_FILE="${LOG_FILE:-${WORKING_DIR}/server.log}"
RUN_SCRIPT_PATH="${RUN_SCRIPT_PATH:-${WORKING_DIR}/server_run.sh}"
JOIN_TICKET_SECRET="${JOIN_TICKET_SECRET:-}"
GAME_SERVER_TARGET_ID="${GAME_SERVER_TARGET_ID:-${GAME_SERVER_INSTANCE_ID:-}}"
ENV_FILE_PATH="${ENV_FILE_PATH:-/etc/${SERVICE_NAME}.env}"
IDLE_TIMEOUT_SECONDS="${IDLE_TIMEOUT_SECONDS:-300}"
AUTO_STOP_VM_ON_IDLE="${AUTO_STOP_VM_ON_IDLE:-true}"
IDLE_STOP_MARKER_FILE="${IDLE_STOP_MARKER_FILE:-}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --service-name)
            SERVICE_NAME="$2"
            shift 2
            ;;
        --service-user)
            SERVICE_USER="$2"
            shift 2
            ;;
        --working-dir)
            WORKING_DIR="$2"
            shift 2
            ;;
        --executable)
            EXECUTABLE_PATH="$2"
            shift 2
            ;;
        --port)
            SERVER_PORT="$2"
            shift 2
            ;;
        --max-players)
            MAX_PLAYERS="$2"
            shift 2
            ;;
        --log-file)
            LOG_FILE="$2"
            shift 2
            ;;
        --join-ticket-secret)
            JOIN_TICKET_SECRET="$2"
            shift 2
            ;;
        --target-id|--instance-id)
            GAME_SERVER_TARGET_ID="$2"
            shift 2
            ;;
        --env-file)
            ENV_FILE_PATH="$2"
            shift 2
            ;;
        --idle-timeout)
            IDLE_TIMEOUT_SECONDS="$2"
            shift 2
            ;;
        --disable-vm-auto-stop)
            AUTO_STOP_VM_ON_IDLE="false"
            shift
            ;;
        --idle-stop-marker)
            IDLE_STOP_MARKER_FILE="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  --service-name NAME   systemd service name (default: unity-game-server)"
            echo "  --service-user USER   Linux user that should run the server (default: ubuntu)"
            echo "  --working-dir PATH    Deployment folder containing server_run.sh"
            echo "  --executable PATH     Unity server executable path"
            echo "  --port PORT           Server port (default: 7777)"
            echo "  --max-players NUM     Max players (default: 4)"
            echo "  --log-file PATH       Log file path"
            echo "  --join-ticket-secret VALUE  HMAC secret used for join-ticket validation"
            echo "  --target-id ID        Expected target id embedded in join tickets"
            echo "  --instance-id ID      Legacy alias for --target-id"
            echo "  --env-file PATH       systemd environment file path"
            echo "  --idle-timeout SEC    Stop the dedicated server after SEC seconds with no connected players"
            echo "  --disable-vm-auto-stop  Leave the VM powered on after an idle shutdown"
            echo "  --idle-stop-marker PATH  Marker file used to request a VM poweroff on idle exit"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information."
            exit 1
            ;;
    esac
done

if [ ! -f "${RUN_SCRIPT_PATH}" ]; then
    echo "Error: ${RUN_SCRIPT_PATH} was not found."
    exit 1
fi

if [ -z "${IDLE_STOP_MARKER_FILE}" ]; then
    IDLE_STOP_MARKER_FILE="${WORKING_DIR}/.unity-game-server-stop-vm"
fi

chmod +x "${RUN_SCRIPT_PATH}"

SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

echo "Installing ${SERVICE_NAME}.service"
echo "  User       : ${SERVICE_USER}"
echo "  Working Dir: ${WORKING_DIR}"
echo "  Executable : ${EXECUTABLE_PATH}"
echo "  Port       : ${SERVER_PORT}"
echo "  Max Players: ${MAX_PLAYERS}"
echo "  Log File   : ${LOG_FILE}"
echo "  Target Id  : ${GAME_SERVER_TARGET_ID}"
echo "  Env File   : ${ENV_FILE_PATH}"
echo "  Idle Timeout: ${IDLE_TIMEOUT_SECONDS}"
echo "  Auto Poweroff: ${AUTO_STOP_VM_ON_IDLE}"
echo "  Idle Marker : ${IDLE_STOP_MARKER_FILE}"

sudo mkdir -p "$(dirname "${ENV_FILE_PATH}")"
{
    printf 'SERVER_IDLE_TIMEOUT_SECONDS=%s\n' "${IDLE_TIMEOUT_SECONDS}"
    printf 'AUTO_STOP_VM_ON_IDLE=%s\n' "${AUTO_STOP_VM_ON_IDLE}"
    printf 'IDLE_STOP_MARKER_FILE=%s\n' "${IDLE_STOP_MARKER_FILE}"

    if [ -n "${JOIN_TICKET_SECRET}" ]; then
        printf 'JOIN_TICKET_SECRET=%s\n' "${JOIN_TICKET_SECRET}"
    fi

    if [ -n "${GAME_SERVER_TARGET_ID}" ]; then
        printf 'GAME_SERVER_TARGET_ID=%s\n' "${GAME_SERVER_TARGET_ID}"
        printf 'GAME_SERVER_INSTANCE_ID=%s\n' "${GAME_SERVER_TARGET_ID}"
    fi
} | sudo tee "${ENV_FILE_PATH}" > /dev/null

sudo chmod 600 "${ENV_FILE_PATH}"

sudo tee "${SERVICE_FILE}" > /dev/null <<EOF
[Unit]
Description=Unity Dedicated Game Server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=${SERVICE_USER}
WorkingDirectory=${WORKING_DIR}
EnvironmentFile=-${ENV_FILE_PATH}
ExecStart=${RUN_SCRIPT_PATH} --executable ${EXECUTABLE_PATH} --port ${SERVER_PORT} --max-players ${MAX_PLAYERS} --log-file ${LOG_FILE}
Restart=on-failure
RestartSec=5
TimeoutStopSec=30
ExecStopPost=+/bin/bash -lc 'if [ "\${AUTO_STOP_VM_ON_IDLE:-false}" = "true" ] && [ -f "\${IDLE_STOP_MARKER_FILE}" ]; then rm -f "\${IDLE_STOP_MARKER_FILE}"; /usr/bin/systemctl poweroff; fi'

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable "${SERVICE_NAME}"
sudo systemctl restart "${SERVICE_NAME}"
sudo systemctl status "${SERVICE_NAME}" --no-pager

echo ""
echo "${SERVICE_NAME}.service is installed and enabled."
echo "Use 'sudo journalctl -u ${SERVICE_NAME} -f' or inspect ${LOG_FILE} for runtime logs."