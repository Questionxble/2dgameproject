#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GENERIC_SCRIPT_PATH="${SCRIPT_DIR}/install_server_service.sh"

if [ ! -f "${GENERIC_SCRIPT_PATH}" ]; then
    echo "Error: ${GENERIC_SCRIPT_PATH} was not found."
    echo "Deploy install_server_service.sh alongside this compatibility wrapper."
    exit 1
fi

chmod +x "${GENERIC_SCRIPT_PATH}"
exec "${GENERIC_SCRIPT_PATH}" "$@"