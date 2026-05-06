# CI/CD Pipeline Setup

This repository now includes two GitHub Actions workflows:

- [.github/workflows/gcp-dedicated-server-cicd.yml](.github/workflows/gcp-dedicated-server-cicd.yml)
- [.github/workflows/gcp-cloud-run-source-deploy.yml](.github/workflows/gcp-cloud-run-source-deploy.yml)

The dedicated-server workflow automates the current server release process:

1. Build the Linux dedicated server with Unity.
2. Start the baseline Compute Engine VM if it is stopped.
3. Upload the server bundle and deployment scripts to the VM.
4. Reinstall and restart the `unity-game-server` service.
5. Create a fresh Compute Engine instance template from that VM.
6. Update Cloud Run so new lobby VMs use the new template.
7. Optionally redeploy the Cloud Run source in the same run.

The Cloud Run source workflow automatically redeploys the orchestrator when files under `gcp/cloud_run/**` change on `multiplayer-release`.

## Why This Fixes The Stopped-VM Bottleneck

The pipeline no longer assumes the source VM is already on. The deploy script explicitly:

1. checks the source VM status,
2. starts it if needed,
3. waits for `RUNNING`,
4. waits for SSH to become ready,
5. deploys the release,
6. optionally stops it again if the workflow had to start it.

That removes the most common manual failure mode you called out.

## One-Time Manual Setup

These parts still need to be done manually once.

### 1. GitHub Secrets

Add these repository or environment secrets:

- `UNITY_LICENSE`
- `GCP_WORKLOAD_IDENTITY_PROVIDER`
- `GCP_SERVICE_ACCOUNT_EMAIL`

`UNITY_LICENSE` is required because GitHub Actions cannot build the Unity server without an activated license.

### 2. GitHub Variables

Add these repository or environment variables:

- `GCP_PROJECT_ID=soulfuljourney`
- `GCP_ZONE=us-central1-f`
- `GCP_SOURCE_INSTANCE_NAME=soulfuljourneyserver`
- `GCP_TARGET_INSTANCE_ID=soulfuljourneyserver`
- `GCP_TEMPLATE_NAME_PREFIX=unity-server-template`
- `GCP_CLOUD_RUN_SERVICE=game-server-orchestrator`
- `GCP_CLOUD_RUN_REGION=us-central1`
- `VM_SERVICE_USER=chris`
- `GCP_SSH_USER=chris`
- `VM_WORKING_DIR=/home/chris/LinuxBuildFiles`
- `VM_EXECUTABLE_PATH=/home/chris/LinuxBuildFiles/LinuxServerBuild.x86_64`
- `VM_SERVICE_NAME=unity-game-server`
- `SERVER_PORT=7777`
- `MAX_PLAYERS=4`
- `IDLE_TIMEOUT_SECONDS=300`
- `GAME_TRANSPORT_MODE=relay`
- `RELAY_CONNECTION_TYPE=dtls`
- `SERVER_RUNTIME_STATUS_ENDPOINT=/server/runtime`
- `JOIN_TICKET_SECRET_SECRET_NAME=join-ticket-secret`
- `SERVER_REGISTRATION_TOKEN_SECRET_NAME=server-registration-token`

### 3. Google Cloud IAM

The GitHub Actions service account needs enough access to:

- build and deploy Cloud Run revisions,
- start and stop the source VM,
- SSH and SCP into the source VM,
- create Compute Engine instance templates,
- read Secret Manager values.

At minimum, plan for these capabilities:

- `roles/run.admin`
- `roles/compute.instanceAdmin.v1`
- `roles/compute.osAdminLogin` or the equivalent SSH/OS Login access used by your VM
- `roles/secretmanager.secretAccessor`
- `roles/iam.serviceAccountUser` if your Cloud Run deploy path needs it

If you use Workload Identity Federation, bind the GitHub repository to that GCP service account.

### 4. Baseline VM Bootstrap

The pipeline assumes there is already one source VM that can be used to mint templates. That VM must already support the current deployment layout:

- the Linux user exists,
- the working directory exists,
- SSH access from the GitHub Actions service account works,
- `install_server_service.sh` can run successfully on it.

If the SSH login account is different from the systemd service account, set `GCP_SSH_USER` and `VM_SERVICE_USER` separately.

This is still a one-time bootstrap step.

## How To Run It

### Automatic

Push to `multiplayer-release` with changes in:

- `Assets/**`
- `Packages/**`
- `ProjectSettings/**`
- `Assets/Editor/CommandLineBuild.cs`
- `install_server_service.sh`
- `server_run.sh`
- `server_start.sh`
- `sync_gce_instance_metadata.sh`
- `ci/gcp/**`

That will build and roll the server template automatically.

Push to `multiplayer-release` with changes in:

- `gcp/cloud_run/**`
- `.github/workflows/gcp-cloud-run-source-deploy.yml`

That will redeploy the Cloud Run orchestrator source automatically.

### Manual

Run the `GCP Dedicated Server CI/CD` workflow from GitHub Actions.

Keep the workflow files on the repository default branch so they remain visible in the Actions UI, but run deployment automation from `multiplayer-release`.

Optional workflow inputs:

- `deploy_orchestrator_source=true` if you also changed `gcp/cloud_run/**`
- `stop_source_instance_after_deploy=false` if you want to leave the source VM on for debugging

## Recommended Next Improvement

This workflow automates the current process, but it still mutates a baseline VM and then snapshots it into a template. That is much better than manual deployment, but it is not the cleanest long-term shape.

The next upgrade I recommend is this:

1. Build the dedicated-server bundle in CI.
2. Upload the bundle to a versioned GCS location.
3. Make the VM startup path pull the bundle from GCS on boot.
4. Stop baking each server release through a mutable source VM.

That removes most SSH/SCP dependence entirely and makes the allocator flow better aligned with ephemeral per-lobby instances.

## Practical Recommendation

Use the new workflow immediately, then plan a second pass that replaces source-instance template creation with one of these:

1. startup-time artifact pull from GCS, or
2. image baking with a dedicated build image/template pipeline.

For this project, the GCS artifact pull approach is likely the best next step because it keeps your per-lobby allocator model intact while removing the release friction around stopped VMs.