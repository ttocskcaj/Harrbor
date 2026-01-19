# Harrbor

Media orchestration service that coordinates transfers between a remote seedbox (qBittorrent) and a local homelab (Sonarr/Radarr).

## What It Does

- Monitors Sonarr/Radarr for completed downloads on your seedbox
- Transfers files via SFTP to local staging
- Coordinates import while preserving seeding on the seedbox

## Quick Start

```yaml
services:
  harrbor:
    image: ghcr.io/ttocskcaj/harrbor:latest
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Pacific/Auckland
    volumes:
      - ./config/appsettings.json:/app/appsettings.json:ro
      - ./ssh/id_ed25519:/home/harrbor/.ssh/id_ed25519:ro
      - ./data:/app/data
      - ./staging:/staging
    ports:
      - "8080:8080"
```

## Documentation

- [How It Works](docs/wiki/How-It-Works.md)
- [Running with Docker](docs/wiki/Running-with-Docker.md)
- [Configuration](docs/wiki/Configuration.md)

## Requirements

- qBittorrent on your seedbox with Web UI enabled
- Sonarr and/or Radarr on your homelab
- SSH access to your seedbox

## License

MIT
