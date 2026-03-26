#!/bin/bash

# Minimal Unity Server Launcher
# Let DedicatedServerConfig handle the server setup

GAME_EXECUTABLE="/home/ubuntu/LinuxBuildFiles/LinuxServerBuild.x86_64"
LOG_FILE="server.log"

echo "=== Minimal Unity Server Launcher ==="
echo "Executable: $GAME_EXECUTABLE"
echo "Log: $LOG_FILE"
echo "Network: $(curl -s ifconfig.me):7777"
echo "======================================"

# Make sure it's executable
chmod +x "$GAME_EXECUTABLE"

# Clear previous log
> "$LOG_FILE"

echo "=== Pre-flight Checks ==="
echo "Checking executable..."
if [ ! -f "$GAME_EXECUTABLE" ]; then
    echo "❌ ERROR: Executable not found at: $GAME_EXECUTABLE"
    echo "Looking for Unity executables..."
    find /home/ubuntu -name "*LinuxServerBuild*" -type f 2>/dev/null
    exit 1
fi

if [ ! -x "$GAME_EXECUTABLE" ]; then
    echo "⚠️  Making executable..."
    chmod +x "$GAME_EXECUTABLE"
fi

echo "✅ Executable found and is executable"
echo "File info:"
ls -la "$GAME_EXECUTABLE"

echo ""
echo "=== Starting Unity Server ==="

# Start with absolute minimal arguments
echo "Launching: $GAME_EXECUTABLE -batchmode -nographics"
"$GAME_EXECUTABLE" -batchmode -nographics &

SERVER_PID=$!
echo "Server PID: $SERVER_PID"

# Wait for server to start
echo "Waiting for server to initialize..."
sleep 5

# Check if server is listening on port 7777
echo ""
echo "=== Network Status Check ==="
echo "Processes listening on port 7777:"
sudo ss -tulpn | grep 7777 || echo "No process listening on port 7777"

echo ""
echo "All Unity processes:"
ps aux | grep -i LinuxServerBuild | grep -v grep || echo "No Unity processes found"

echo ""
echo "Server process status:"
if ps -p $SERVER_PID > /dev/null; then
    echo "✅ Unity server process is running (PID: $SERVER_PID)"
else
    echo "❌ Unity server process is NOT running - likely crashed"
    echo "Checking for crash logs..."
    if [ -f "$LOG_FILE" ]; then
        echo "=== Unity Log Output ==="
        cat "$LOG_FILE"
    fi
fi

# Setup cleanup
cleanup() {
    echo ""
    echo "Stopping server..."
    kill $SERVER_PID 2>/dev/null
    wait $SERVER_PID 2>/dev/null
    echo "Server stopped"
}

trap cleanup SIGINT SIGTERM

# Show logs
echo ""
echo "=== Server Logs (live) ==="
tail -f "$LOG_FILE" &
TAIL_PID=$!

# Wait for server to finish
wait $SERVER_PID

# Cleanup
kill $TAIL_PID 2>/dev/null
echo ""
echo "Server process ended"