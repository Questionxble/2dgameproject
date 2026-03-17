# Unity Netcode Multiplayer Setup Guide

## Overview
This guide will help you set up the 2-player multiplayer functionality using Unity Netcode for GameObjects.

## Prerequisites
- Unity Netcode for GameObjects v2.7.0 (already installed)
- Updated assembly definition files (already configured)

## Setup Steps

### 1. NetworkManager Setup
1. Create an empty GameObject in your scene
2. Name it "NetworkManager"
3. Add the "NetworkManager" component from Unity Netcode
4. Add the "MultiplayerGameManager" script to the same GameObject

### 2. Player Prefab Setup
1. Take your existing Player GameObject (the one with PlayerMovement and WeaponClassController)
2. Create a prefab from it by dragging it to the Project window
3. Add a "NetworkObject" component to the Player prefab
4. In the NetworkObject component:
   - Check "Don't Destroy with Owner" if you want players to persist
   - Set "Object Pool" to false
   - Set "Network Transform" to true for position synchronization

### 3. NetworkManager Configuration
In the NetworkManager component:
1. **Prefabs List**: 
   - Add your Player prefab to the "Prefabs" list
   - This allows the NetworkManager to spawn networked players

2. **Transport**:
   - Use "Unity Transport" (default)
   - For testing locally, keep default settings
   - For internet play, you'll need Unity Gaming Services

### 4. Spawn Points Setup
1. Create empty GameObjects for spawn points:
   - Name them "Player1SpawnPoint" and "Player2SpawnPoint"
   - Position Player1 at (0, 0, 0) 
   - Position Player2 at (3, 0, 0)
2. Assign these to the MultiplayerGameManager script

### 5. UI Setup (Optional)
1. Create a Canvas if you don't have one
2. Add UI buttons for:
   - "Start Host" (host + client)
   - "Join Game" (client only)
   - "Start Server" (server only)
3. Add a Text component for status updates
4. Add the MultiplayerUI script to a GameObject
5. Assign the UI elements to the MultiplayerUI script

### 6. Scene Preparation
1. Remove any existing player GameObjects from the scene
2. Players will be spawned automatically when clients connect
3. Keep only:
   - NetworkManager GameObject
   - Environment/background objects
   - UI elements
   - Camera (should follow the local player)

## Testing Multiplayer

### Local Testing
1. Build your project
2. Run one instance as Host
3. Run another instance as Client
4. Both players should spawn and be controllable

### Network Testing
1. Use Unity Gaming Services for internet play
2. Or setup port forwarding for local network play
3. Default port is 7777

## Key Changes Made

### PlayerMovement.cs
- Converted to NetworkBehaviour
- Added network variables for health, shield, burning status
- Added ServerRpc/ClientRpc methods for damage and effects
- Network spawn positioning for multiplayer

### WeaponClassController.cs  
- Converted to NetworkBehaviour
- Added network variables for equipped shards and active slot
- Added ServerRpc/ClientRpc methods for attack events
- Synchronized shard equipping and slot switching

### Assembly Definitions
- Updated Game.asmdef to include Unity.Netcode.Runtime reference
- This allows scripts to access Netcode namespace

## Network Architecture

### Server Authority
- Server makes all gameplay decisions
- Clients send input via ServerRpc
- Server processes input and sends results via ClientRpc

### Synchronization
- Network variables automatically sync state
- Animation events use RPC pattern for attacks
- Visual effects triggered on all clients

## Troubleshooting

### Common Issues
1. **"Netcode namespace not found"** - Check assembly definition includes Unity.Netcode.Runtime
2. **"Players not spawning"** - Ensure Player prefab is in NetworkManager prefab list
3. **"Input not working"** - Check IsOwner conditions in input handling
4. **"Animations not syncing"** - Verify RPC methods are called only by owner

### Debug Tips
- Use Console logs to track network events
- Check NetworkManager connection status
- Verify NetworkObject is properly configured on prefabs

## Cloud Hosting (Future Enhancement)
For production multiplayer, consider:
- Unity Gaming Services (Relay, Lobbies)
- Dedicated server hosting
- Matchmaking system
- Anti-cheat protection

## Performance Notes
- Network variables have update limits
- Use RPCs for events, network variables for state
- Consider object pooling for projectiles/effects
- Monitor network traffic in profiler