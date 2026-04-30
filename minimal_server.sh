#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN_SCRIPT="${SCRIPT_DIR}/server_run.sh"
LOG_FILE="${LOG_FILE:-${SCRIPT_DIR}/server.log}"
DEFAULT_PORT="${SERVER_PORT:-7777}"

if [ ! -f "${RUN_SCRIPT}" ]; then
    echo "Error: ${RUN_SCRIPT} was not found."
    exit 1
fi

chmod +x "${RUN_SCRIPT}"

echo "=== Minimal Unity Server Launcher ==="
echo "Runner : ${RUN_SCRIPT}"
echo "Log    : ${LOG_FILE}"
echo "Public : $(curl -s ifconfig.me || echo unavailable):${DEFAULT_PORT}"
echo "====================================="

"${RUN_SCRIPT}" "$@" &
SERVER_PID=$!

echo "Server PID: ${SERVER_PID}"
echo "Waiting 5 seconds for the server to initialize..."
sleep 5

echo ""
echo "=== Network Status Check ==="
ss -u -lpn | grep "${DEFAULT_PORT}" || echo "No UDP process is listening on port ${DEFAULT_PORT} yet."

cleanup() {
    echo ""
    echo "Stopping server..."
    kill "${SERVER_PID}" 2>/dev/null || true
    wait "${SERVER_PID}" 2>/dev/null || true
}

trap cleanup SIGINT SIGTERM

echo ""
echo "=== Server Logs (live) ==="
tail -f "${LOG_FILE}" &
TAIL_PID=$!

wait "${SERVER_PID}"
kill "${TAIL_PID}" 2>/dev/null || true

echo ""
echo "Server process ended."