#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN_SCRIPT="${SCRIPT_DIR}/server_run.sh"

if [ ! -f "${RUN_SCRIPT}" ]; then
    echo "Error: ${RUN_SCRIPT} was not found."
    exit 1
fi

chmod +x "${RUN_SCRIPT}"
exec "${RUN_SCRIPT}" "$@"