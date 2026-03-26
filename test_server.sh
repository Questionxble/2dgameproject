#!/bin/bash

# Simple Unity Server Test Script
# This script checks if Unity is running properly and shows detailed logs

GAME_EXECUTABLE="/home/ubuntu/LinuxBuildFiles/LinuxServerBuild.x86_64"
LOG_FILE="server.log"

echo "=== Unity Server Diagnostic ==="
echo "Executable: $GAME_EXECUTABLE"
echo "Log file: $LOG_FILE"
echo ""

# Check if executable exists and is executable
if [ ! -f "$GAME_EXECUTABLE" ]; then
    echo "❌ ERROR: Game executable not found at $GAME_EXECUTABLE"
    exit 1
fi

if [ ! -x "$GAME_EXECUTABLE" ]; then
    echo "❌ ERROR: Game executable is not executable. Run: chmod +x $GAME_EXECUTABLE"
    exit 1
fi

echo "✅ Executable found and is executable"

# Clear previous log
> "$LOG_FILE"

echo ""
echo "Starting Unity server in test mode..."

# Start Unity with minimal arguments
"$GAME_EXECUTABLE" -batchmode -nographics -logFile "$LOG_FILE" -port 7777 &

SERVER_PID=$!
echo "Server PID: $SERVER_PID"

# Wait for log file to be created and show initial output
echo ""
echo "Waiting for server to start..."
sleep 3

if [ -f "$LOG_FILE" ]; then
    echo ""
    echo "=== Server Log Output (first 50 lines) ==="
    head -n 50 "$LOG_FILE"
    echo "=== End Log Output ==="
else
    echo "❌ No log file created - server may not be starting"
fi

# Check if process is still running
if ps -p $SERVER_PID > /dev/null; then
    echo ""
    echo "✅ Server process is running (PID: $SERVER_PID)"
    echo "To see live logs: tail -f $LOG_FILE"
    echo "To stop server: kill $SERVER_PID"
else
    echo ""
    echo "❌ Server process has stopped"
    if [ -f "$LOG_FILE" ]; then
        echo ""
        echo "=== Full Log Output ==="
        cat "$LOG_FILE"
    fi
fi