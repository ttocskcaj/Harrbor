# Running with Docker

```bash
docker pull ghcr.io/ttocskcaj/harrbor:latest
```

## Docker Compose

Create a `docker-compose.yml`:

```yaml
services:
  harrbor:
    image: ghcr.io/ttocskcaj/harrbor:latest
    container_name: harrbor
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Pacific/Auckland
    volumes:
      # Configuration
      - ./config/appsettings.json:/app/appsettings.json:ro

      # SSH key for seedbox access
      - ./ssh/id_ed25519:/home/harrbor/.ssh/id_ed25519:ro

      # Persistent data (SQLite database)
      - ./data:/app/data

      # Staging directory (where transfers land)
      - /path/to/staging:/staging
    ports:
      - "8080:8080"
    restart: unless-stopped
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `PUID` | 1000 | User ID for file permissions |
| `PGID` | 1000 | Group ID for file permissions |
| `TZ` | UTC | Timezone for logs |

## Volume Mounts

### Configuration (`/app/appsettings.json`)

Mount your configuration file. See [Configuration](Configuration.md) for details.

### SSH Key (`/home/harrbor/.ssh/id_ed25519`)

Mount your SSH private key for seedbox access. The container's SSH config is pre-configured to accept new host keys automatically.

```bash
# Generate a key if needed
ssh-keygen -t ed25519 -f ./ssh/id_ed25519 -N ""

# Copy to seedbox
ssh-copy-id -i ./ssh/id_ed25519.pub user@seedbox.example.com
```

### Data Directory (`/app/data`)

Persistent storage for the SQLite database. Contains the state of all tracked releases.

### Staging Directory

The local directory where transfers from the seedbox will land. This should be:

- Accessible by Sonarr/Radarr for import
- Writable by the Harrbor container (check PUID/PGID)

## Health Checks

The container exposes health check endpoints:

- `/health` - All checks
- `/health/ready` - Database + external service connectivity
- `/health/live` - Basic liveness (app running)

The Docker image includes a built-in health check that polls `/health/live`.

## Permissions

Harrbor follows the linuxserver.io pattern for PUID/PGID:

1. On startup, the entrypoint adjusts the internal user/group IDs
2. Ownership of `/app/data` and `/app/logs` is fixed
3. The app runs as the `harrbor` user with your specified IDs

Ensure your staging directory is writable by the configured PUID/PGID.

## Logs

Logs are written to:
- Console (visible via `docker logs`)
- `/app/logs/harrbor-{date}.log` (rolling daily, 7 day retention)

Set log level in `appsettings.json`:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  }
}
```
