#!/bin/bash

# Dedicated Game Server Launcher Script
# This script launches the Unity game build as a dedicated server client

# Configuration
GAME_EXECUTABLE="./2dgameproject"  # Change this to match your actual executable name
SERVER_IP="18.223.166.226"              # IP of the game server to connect to
SERVER_PORT="7777"                 # Port of the game server
MAX_PLAYERS="2"                    # Maximum players per session

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -ip|--address)
            SERVER_IP="$2"
            shift 2
            ;;
        -port|--port)
            SERVER_PORT="$2"
            shift 2
            ;;
        -maxplayers|--max-players)
            MAX_PLAYERS="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  -ip, --address IP        Server IP address (default: 127.0.0.1)"
            echo "  -port, --port PORT       Server port (default: 7777)"
            echo "  -maxplayers, --max-players NUM  Max players per session (default: 2)"
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
    echo "Please ensure the game is built and the executable name is correct."
    exit 1
fi

# Make executable if not already
chmod +x "$GAME_EXECUTABLE"

echo "=== Dedicated Game Server Launcher ==="
echo "Game Executable: $GAME_EXECUTABLE"
echo "Server Address: $SERVER_IP:$SERVER_PORT"
echo "Max Players: $MAX_PLAYERS"
echo "======================================="

# Launch the game with dedicated server parameters
echo "Starting dedicated server..."
"$GAME_EXECUTABLE" \
    -dedicatedserver \
    -address "$SERVER_IP" \
    -port "$SERVER_PORT" \
    -maxplayers "$MAX_PLAYERS" \
    -batchmode \
    -nographics \
    -logFile "server.log"

# Check exit code
EXIT_CODE=$?
if [ $EXIT_CODE -eq 0 ]; then
    echo "Server shut down successfully"
else
    echo "Server exited with error code: $EXIT_CODE"
    echo "Check server.log for details"
fi