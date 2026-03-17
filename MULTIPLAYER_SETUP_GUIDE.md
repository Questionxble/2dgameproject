# Multiplayer Implementation Guide

## Current Status
The multiplayer conversion has been started but is currently disabled due to missing Netcode package installation.

## Next Steps

### 1. Install Unity Netcode for GameObjects
1. Open Unity Package Manager (Window > Package Manager)
2. Change dropdown from "In Project" to "Unity Registry"
3. Search for "Netcode for GameObjects"
4. Install the latest version (should be 1.0.0 or newer)

### 2. Install Unity Gaming Services Packages
Install these additional packages:
- `com.unity.services.core`
- `com.unity.services.authentication`
- `com.unity.services.lobby`
- `com.unity.services.relay`

### 3. Re-enable Networking Code
Once Netcode is installed:

#### In PlayerMovement.cs:
- Uncomment `using Unity.Netcode;` (line 9)
- Change `MonoBehaviour` to `NetworkBehaviour` (line 11)
- Uncomment the NetworkVariable declarations (lines 151-156)
- Uncomment the OnNetworkSpawn/OnNetworkDespawn methods
- Uncomment all ServerRpc and ClientRpc methods

#### In WeaponClassController.cs:
- Uncomment `using Unity.Netcode;`
- Change `MonoBehaviour` to `NetworkBehaviour`
- Re-enable networking in weapon attack methods

### 4. Setup Network Manager
1. Create empty GameObject named "NetworkManager"
2. Add NetworkManager component
3. Add UnityTransport component
4. Drag your Player prefab into Network Prefabs list
5. Set Player prefab as the Player Prefab

### 5. Configure Player Prefab
Your player prefab needs:
- NetworkObject component
- NetworkTransform component (with only X,Y position enabled)
- NetworkRigidbody2D component
- Set Authority to Owner

## Architecture Summary

### Network Variables (Server Authority):
- `networkHealth` - Player health
- `networkIsBurning` - Burning status effect  
- `networkIsPlayerDead` - Death state
- `networkEquippedShardType` - Current equipped shard
- `networkCurrentShield` - Aegis shield amount
- `networkPlayerID` - Unique player identifier

### RPC Methods:
- **ServerRpc**: Input actions (damage, shard switching, attacks)
- **ClientRpc**: Visual effects (particles, animations, UI updates)

### Spawn System:
- Player 1: Original spawn position
- Player 2: +3 units to the right
- Additional players: Spread out with 1.5x multiplier

## Testing Checklist
After enabling networking:
- [ ] Players spawn in different positions
- [ ] Health synchronizes between clients
- [ ] Storm Shard lightning effects visible to both players
- [ ] Weapon switching updates animations for all clients
- [ ] Damage/burning effects work across network
- [ ] Death states synchronize properly

## Cloud Hosting Setup (Later Phase)
1. Create Unity project in Unity Cloud Dashboard
2. Setup Authentication service
3. Configure Lobby service for matchmaking
4. Setup Relay service for NAT traversal
5. Build and deploy server builds

## Current Code Status
- ✅ Basic networking architecture planned
- ✅ Network variables defined
- ✅ RPC patterns established  
- ⏸️ **PAUSED**: Waiting for Netcode package installation
- ⏳ **TODO**: Complete weapon system networking
- ⏳ **TODO**: Add lobby/matchmaking system