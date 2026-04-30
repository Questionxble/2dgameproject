# Unity Build Instructions for Linux Dedicated Server Deployment

For the current GCP deployment path and the 4-player configuration, use `GCP_COMPUTE_ENGINE_AUTOSTART_AND_4_PLAYER_GUIDE.md` alongside this build document.
For cloud-neutral secrets and auth setup, use `SERVER_SECRET_AND_CONFIG_QUICK_GUIDE.md` and `OIDC_AND_JOIN_TOKEN_GUIDE.md`.

## Overview
You need to create three separate builds for your multiplayer deployment:
1. **Linux Server Build** (for GCP Compute Engine or another Linux VM)
2. **Windows Client Build** (for Windows Intel 64-bit machines)
3. **macOS Client Build** (for Mac M2/ARM64 machines)

## 1. Linux Server Build (for GCP Compute Engine or another Linux VM)

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
7. Upload the entire build folder to your Linux VM

### Command-Line Build Option
If the interactive Unity build window hangs during `PostprocessPlayer`, use the editor script entry point instead:

```powershell
"C:\Program Files\Unity\Hub\Editor\6000.2.5f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "C:\Users\chris\source\repos\2dgameproject" `
  -executeMethod CommandLineBuild.BuildLinuxDedicatedServer `
  -buildOutput "C:\Users\chris\source\deployments\Linux\LinuxServerBuild_cli\2dgameproject_server" `
  -logFile "C:\Users\chris\source\deployments\Linux\linux-server-build.log"
```

Notes:
- `-buildOutput` should be the full output path to the Linux server executable, not just a folder.
- This uses `StandaloneLinux64` with `StandaloneBuildSubtarget.Server`.
- The detailed build log will be written to the `-logFile` path you provide.

### Linux VM Deployment Commands:
```bash
# On your Linux VM
chmod +x 2dgameproject_server
chmod +x server_start.sh
chmod +x install_server_service.sh

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
  - **Address**: your VM public IP or DNS name
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
- **Clients**: Connect to your VM public IP or DNS name on port `7777`

### Firewall Rules
Ensure your cloud firewall allows:
- **Inbound**: UDP port 7777 from `0.0.0.0/0`
- **Inbound**: TCP port 22 from your admin IP
- **Outbound**: Default egress or equivalent

### Testing Connection
1. Start server on EC2: `./server_start.sh`
2. Check server logs: `tail -f server.log`
3. Launch client builds and click "Join" button
4. Up to four clients should connect to the same game session

### Troubleshooting

#### Server Issues:
- Check `server.log` for Unity errors
- Verify port 7777 is not blocked: `sudo netstat -tulpn | grep 7777`
- Test network connectivity: `telnet your-server-ip 7777`

#### Client Issues:
- Ensure firewall allows outbound UDP 7777
- Check Unity logs for connection errors
- Verify the VM public IP is current or use a reserved static IP

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
├── Linux VM:
│   ├── 2dgameproject_server        # Main executable
│   ├── 2dgameproject_server_Data/  # Game data
│   ├── server_start.sh             # Start script
│   ├── install_server_service.sh   # systemd installer
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

- **GCP Compute Engine**: `e2-standard-2` is a comfortable baseline, `e2-medium` can work for lightweight tests, and smaller tiers may need a longer warmup delay
- **RAM**: Minimum 2GB, 4GB recommended for 4 players
- **Network**: Monitor bandwidth usage during gameplay
- **CPU**: Monitor CPU usage and cold-start time, then adjust the VM tier if needed

The single-session architecture in this repo is now configured for up to 4 players on a single dedicated Linux session.