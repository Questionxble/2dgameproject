#!/bin/bash

# Unity Dedicated Server Startup Script for AWS EC2
# This script starts the Unity game as a dedicated server

# Configuration
GAME_EXECUTABLE="~/LinuxServerBuild/LinuxServerBuild.x86_64"  # Linux server build executable
SERVER_PORT="7777"
MAX_PLAYERS="2"
LOG_FILE="server.log"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
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
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -port, --port PORT       Server port (default: 7777)"
            echo "  -maxplayers, --max-players NUM  Max players (default: 2)"
            echo "  -executable, --executable PATH  Path to game executable"
            echo "  -h, --help               Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use -h or --help for usage information"
            exit 1
            ;;
    esac
done

# Check if game executable exists
if [ ! -f "$GAME_EXECUTABLE" ]; then
    echo "Error: Game executable '$GAME_EXECUTABLE' not found!"
    echo "Please build your Unity project for Linux Server and place the executable here."
    echo "Build Settings: Target Platform = Dedicated Server, Architecture = x86_64"
    exit 1
fi

# Make executable if not already
chmod +x "$GAME_EXECUTABLE"

# Get network interface info for debugging
echo "=== Network Interface Information ==="
echo "Available network interfaces:"
ip addr show | grep -E "inet [0-9]" | awk '{print $2}' | cut -d'/' -f1
echo ""
echo "Public IP (if available):"
curl -s ifconfig.me || echo "Could not determine public IP"
echo ""

# Display configuration
echo "=== Unity Dedicated Server Configuration ==="
echo "Game Executable: $GAME_EXECUTABLE"
echo "Server Port: $SERVER_PORT (UDP)"
echo "Max Players: $MAX_PLAYERS"
echo "Log File: $LOG_FILE"
echo "Server Mode: Host (accepts connections and manages game state)"
echo "============================================="

# Clear previous log
> "$LOG_FILE"

echo "Starting Unity dedicated server..."
echo "Players should connect to: $(curl -s ifconfig.me):$SERVER_PORT"
echo "Press Ctrl+C to stop the server"
echo ""

# Launch Unity as dedicated server
"$GAME_EXECUTABLE" \
    -batchmode \
    -nographics \
    -logFile "$LOG_FILE" \
    -port "$SERVER_PORT" \
    -maxplayers "$MAX_PLAYERS" &

# Store the process ID
SERVER_PID=$!
echo "Server started with PID: $SERVER_PID"

# Function to handle cleanup on script exit
cleanup() {
    echo ""
    echo "Shutting down server..."
    kill $SERVER_PID 2>/dev/null
    wait $SERVER_PID 2>/dev/null
    echo "Server stopped."
}

# Set up signal handling
trap cleanup SIGINT SIGTERM

# Monitor the server process and display log output
tail -f "$LOG_FILE" &
TAIL_PID=$!

# Wait for server process to end
wait $SERVER_PID
EXIT_CODE=$?

# Clean up tail process
kill $TAIL_PID 2>/dev/null

# Report exit status
if [ $EXIT_CODE -eq 0 ]; then
    echo "Server shut down successfully"
else
    echo "Server exited with error code: $EXIT_CODE"
    echo "Check $LOG_FILE for details"
fi

exit $EXIT_CODE