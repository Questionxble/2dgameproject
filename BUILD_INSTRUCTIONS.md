# Unity Build Instructions for AWS EC2 Deployment

## Overview
You need to create three separate builds for your multiplayer deployment:
1. **Linux Server Build** (for AWS EC2)
2. **Windows Client Build** (for Windows Intel 64-bit machines)
3. **macOS Client Build** (for Mac M2/ARM64 machines)

## 1. Linux Server Build (for AWS EC2)

### Build Settings:
- **Target Platform**: Dedicated Server
- **Architecture**: x86_64 (Intel/AMD)
- **Build Name**: `2dgameproject_server`

### Steps:
1. Open `File > Build Settings`
2. Select `Dedicated Server` platform
3. Set Architecture to `x86_64`
4. Add your `StartingScene` to the build
5. Click `Player Settings` and configure:
   - **Product Name**: `2dgameproject`
   - **Company Name**: Your company name
   - **Default Icon**: Set your game icon
   - **Server Build**: Checked ✅
6. Click `Build` and save as `2dgameproject_server`
7. Upload the entire build folder to your EC2 instance

### EC2 Deployment Commands:
```bash
# On your EC2 instance
chmod +x 2dgameproject_server
chmod +x server_start.sh

# Start the server
./server_start.sh

# Or start manually with custom settings
./2dgameproject_server -batchmode -nographics -server -port 7777
```

## 2. Windows Client Build

### Build Settings:
- **Target Platform**: Windows, Mac, Linux
- **Target OS**: Windows
- **Architecture**: x86_64
- **Build Name**: `2dgameproject.exe`

### Configuration:
- Ensure NetworkManager has:
  - **Address**: `18.188.108.134` (your EC2 public IP)
  - **Port**: `7777`
  - **Server Listen Address**: `127.0.0.1` (doesn't matter for client)

### Steps:
1. Switch platform to `Windows, Mac, Linux`
2. Set Target OS to `Windows`
3. Build and distribute the `.exe` file

## 3. macOS Client Build (ARM64)

### Build Settings:
- **Target Platform**: Windows, Mac, Linux
- **Target OS**: Mac OS X
- **Architecture**: Apple Silicon (ARM64)
- **Build Name**: `2dgameproject.app`

### Steps:
1. Switch platform to `Windows, Mac, Linux`
2. Set Target OS to `Mac OS X`
3. Set Architecture to `Apple Silicon`
4. Build and distribute the `.app` bundle

## Important Notes

### Network Configuration
- **Server**: Listens on `0.0.0.0:7777` (all interfaces)
- **Clients**: Connect to `18.188.108.134:7777` (EC2 public IP)

### AWS Security Group
Ensure your EC2 security group allows:
- **Inbound**: UDP port 7777 from `0.0.0.0/0`
- **Outbound**: All traffic (default)

### Testing Connection
1. Start server on EC2: `./server_start.sh`
2. Check server logs: `tail -f server.log`
3. Launch client builds and click "Join" button
4. Both clients should connect to the same game session

### Troubleshooting

#### Server Issues:
- Check `server.log` for Unity errors
- Verify port 7777 is not blocked: `sudo netstat -tulpn | grep 7777`
- Test network connectivity: `telnet your-ec2-ip 7777`

#### Client Issues:
- Ensure firewall allows outbound UDP 7777
- Check Unity logs for connection errors
- Verify EC2 public IP is current (may change on restart)

### Build Size Optimization

#### Server Build:
- Enable "Strip Engine Code"
- Set "Stripping Level" to High
- Disable unnecessary modules in XR/Audio settings

#### Client Builds:
- Compress textures appropriately
- Consider asset bundles for large assets
- Enable "Strip Engine Code" for smaller builds

## File Structure After Build

```
├── EC2 Instance (Linux):
│   ├── 2dgameproject_server        # Main executable
│   ├── 2dgameproject_server_Data/  # Game data
│   ├── server_start.sh             # Start script
│   └── server.log                  # Runtime logs
│
├── Windows Distribution:
│   ├── 2dgameproject.exe           # Main executable
│   └── 2dgameproject_Data/         # Game data
│
└── macOS Distribution:
    └── 2dgameproject.app/          # Application bundle
        ├── Contents/
        │   ├── MacOS/2dgameproject # Executable
        │   └── Resources/          # Game data
```

## Performance Considerations

- **EC2 Instance**: t3.small or larger recommended
- **RAM**: Minimum 1GB, 2GB recommended for 2 players
- **Network**: Monitor bandwidth usage during gameplay
- **CPU**: Monitor CPU usage, upgrade instance if needed

The single-session architecture you described will work well for 2 players on a small EC2 instance.