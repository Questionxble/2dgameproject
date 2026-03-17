# Seamless Multiplayer Configuration Guide

## Setup 2: Automatic Port Management

### Unity Editor Testing (Two Editor Instances)

#### Player 1 Editor (Host):
```
AutoNetworkStarter Configuration:
├── Enable Auto Start: ✅ True
├── Start Delay: 2 seconds  
├── Preferred Mode: Host
├── Auto Port Increment: ✅ True
└── Base Port: 7777
```

#### Player 2 Editor (Client):
```
AutoNetworkStarter Configuration:
├── Enable Auto Start: ✅ True
├── Start Delay: 2 seconds
├── Preferred Mode: Client
├── Auto Port Increment: ✅ True  
└── Base Port: 7777
```

### Mac Build (Client):
```
AutoNetworkStarter Configuration:
├── Enable Auto Start: ✅ True
├── Start Delay: 2 seconds
├── Preferred Mode: Client
├── Auto Port Increment: ✅ True
└── Base Port: 7777
```

## How It Works:

### Automatic Port Assignment:
- **Host**: Uses port 7777 (listening for connections)
- **Client**: Connects to 127.0.0.1:7777 (connects to Host)

### Launch Sequence:
1. **Player 1 Editor**: Auto-starts as Host on port 7777
2. **Player 2 Editor**: Auto-starts as Client, connects to Host
3. **Mac Build**: Auto-starts as Client, connects to Host

### Expected Console Output:
```
[AutoNetworkStarter] HOST configured for port 7777
[AutoNetworkStarter] Successfully started as Host
```

```
[AutoNetworkStarter] CLIENT configured to connect to Host at port 7777  
[AutoNetworkStarter] Successfully started as Client
```

## Testing Steps:

1. **Configure both Unity Editor instances** as shown above
2. **Start Player 1 Editor** (will become Host)
3. **Start Player 2 Editor** (will become Client)
4. **Both players should spawn** automatically
5. **Test collision prevention** - players should walk through each other

## For Your EC2 Dedicated Server:

When ready for EC2 testing:
- **Unity Editor**: Host mode (port 7777)
- **EC2 Mac Build**: Client mode (connects to your local IP)
- **Local Mac Build**: Client mode (connects to 127.0.0.1:7777)

## Troubleshooting:

- If Host fails to start: Check port 7777 isn't in use
- If Client fails to connect: Ensure Host started first
- If no players spawn: Check MultiplayerGameManager has Player Prefab assigned