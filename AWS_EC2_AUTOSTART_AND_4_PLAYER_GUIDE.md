# Legacy AWS EC2 Autostart and 4-Player Guide

The active deployment path in this repo is now GCP-first and cloud-neutral.

Use these files instead:

- `GCP_COMPUTE_ENGINE_AUTOSTART_AND_4_PLAYER_GUIDE.md`
- `SERVER_SECRET_AND_CONFIG_QUICK_GUIDE.md`
- `OIDC_AND_JOIN_TOKEN_GUIDE.md`
- `install_server_service.sh`
- `gcp/cloud_run/compute_engine_orchestrator.py`

This file is kept only as an AWS-specific reference for the older Lambda/API Gateway/EC2 setup.

## Operational Notes

- If you do not attach an Elastic IP, the EC2 public IP may change each time the instance is started.
- The client code supports this by accepting the address returned by the orchestration API.
- For production, protect the API with a real identity system. API keys or bearer strings embedded in the client are only acceptable for limited testing.
- If you need true app-level readiness, extend the Lambda to check a health signal from the instance instead of relying only on EC2 instance-status checks.

## Security Recommendation

Treat this as two separate security problems:

1. Control plane security: who is allowed to call your start/status API.
2. Game traffic security: whether the UDP gameplay stream is encrypted and tamper-resistant in transit.

For this architecture, secure the control plane first.

- Use Cognito or another real auth layer in front of API Gateway.
- Add rate limiting and logging on the API.
- Do not rely on a shared API key inside the Unity client for production.

DTLS or another UDP encryption layer is still useful, but it does not replace API authentication.

- DTLS helps with confidentiality and integrity for gameplay packets.
- DTLS does not decide who is allowed to start your EC2 instance.
- DTLS does not stop unauthorized users from abusing your orchestration endpoint.

Practical order of implementation:

1. Add Cognito or equivalent auth to the orchestration API.
2. Keep server start/status behind authenticated requests.
3. Then evaluate DTLS or another secure transport approach for the live gameplay channel if your threat model requires encrypted public-internet UDP.

## Fastest Practical Setup

If you want the fewest moving parts:

1. Attach an Elastic IP to the EC2 instance.
2. Install the `unity-game-server` systemd service with `install_ec2_service.sh`.
3. Deploy `aws/lambda/ec2_orchestrator.py`.
4. Set `Enable Remote Server Orchestration = true` on `ClientDebugger`.
5. Use Connect-to-start, not launch-to-start, for the first round of testing.