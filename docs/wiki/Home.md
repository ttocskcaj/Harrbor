# Harrbor

Welcome to the Harrbor wiki!

Harrbor is a media orchestration service that coordinates transfers between a remote seedbox (running qBittorrent) and a local homelab (running Sonarr/Radarr). It solves the distributed state coordination problem of syncing torrents while maintaining seeding on the remote and avoiding re-download loops.

## Quick Links

- [How It Works](How-It-Works.md) - Understand the orchestration pipeline
- [Running with Docker](Running-with-Docker.md) - Deploy Harrbor in your homelab
- [Configuration](Configuration.md) - Configure jobs, services, and transfers

## Key Features

- **Automated Discovery** - Monitors Sonarr/Radarr queues for new downloads
- **Smart Transfers** - Uses rclone for efficient SFTP transfers with parallelism
- **Import Coordination** - Waits for media managers to import before cleanup
- **Seeding Preservation** - Never breaks torrent seeding on the seedbox
- **Retry Logic** - Automatic retries with configurable backoff for failed transfers
- **Multi-Job Support** - Run separate jobs for TV (Sonarr) and Movies (Radarr)

## Requirements

- qBittorrent on your seedbox with Web UI enabled
- Sonarr and/or Radarr on your homelab
- SSH access to your seedbox (for rclone SFTP transfers)
- Docker for running Harrbor
