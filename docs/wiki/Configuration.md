# Configuration

Harrbor is configured via `appsettings.json`. For local development, use `appsettings.local.json` (git-ignored).

## Full Example

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  },
  "Harrbor": {
    "DataPath": "./data"
  },
  "QBittorrent": {
    "BaseUrl": "http://seedbox.example.com:8080",
    "Username": "admin",
    "Password": "your-password"
  },
  "Sonarr": {
    "BaseUrl": "http://localhost:8989",
    "ApiKey": "your-sonarr-api-key",
    "Enabled": true
  },
  "Radarr": {
    "BaseUrl": "http://localhost:7878",
    "ApiKey": "your-radarr-api-key",
    "Enabled": true
  },
  "RemoteStorage": {
    "Host": "seedbox.example.com",
    "Port": 22,
    "User": "seedbox-user",
    "KeyFile": "/home/harrbor/.ssh/id_ed25519",
    "Transfers": 4,
    "Checkers": 8
  },
  "Jobs": {
    "Definitions": [
      {
        "Name": "tv-shows",
        "PollingInterval": "00:01:00",
        "ServiceType": "Sonarr",
        "RemotePath": "/downloads/tv",
        "StagingPath": "/staging/tv",
        "QBittorrentCategory": "tv-sonarr",
        "CompletedCategory": "tv-archived",
        "TransferParallelism": 2,
        "MaxTransferRetries": 3,
        "TransferRetryDelay": "00:05:00",
        "ImportTimeout": "1.00:00:00",
        "Enabled": true
      },
      {
        "Name": "movies",
        "PollingInterval": "00:01:00",
        "ServiceType": "Radarr",
        "RemotePath": "/downloads/movies",
        "StagingPath": "/staging/movies",
        "QBittorrentCategory": "movies-radarr",
        "CompletedCategory": "movies-archived",
        "TransferParallelism": 2,
        "Enabled": true
      }
    ]
  }
}
```

## Section Reference

### Harrbor

| Setting | Default | Description |
|---------|---------|-------------|
| `DataPath` | `./data` | Directory for SQLite database |

### QBittorrent

| Setting | Default | Description |
|---------|---------|-------------|
| `BaseUrl` | - | qBittorrent Web UI URL |
| `Username` | - | Login username |
| `Password` | - | Login password |

### Sonarr / Radarr

| Setting | Default | Description |
|---------|---------|-------------|
| `BaseUrl` | - | API base URL |
| `ApiKey` | - | API key (Settings → General) |
| `Enabled` | `true` | Enable/disable this service |

### RemoteStorage

Settings for rclone SFTP transfers.

| Setting | Default | Description |
|---------|---------|-------------|
| `Host` | - | Seedbox hostname |
| `Port` | `22` | SSH port |
| `User` | - | SSH username |
| `Password` | `null` | SSH password (prefer key auth) |
| `KeyFile` | `null` | Path to SSH private key |
| `KeyFilePassphrase` | `null` | Passphrase for encrypted key |
| `UseAgent` | `false` | Use SSH agent for auth |
| `Transfers` | `4` | Concurrent file transfers |
| `Checkers` | `8` | Concurrent file checkers |
| `AdditionalFlags` | `[]` | Extra rclone flags |

### Jobs

Each job monitors a specific category of downloads.

| Setting | Default | Description |
|---------|---------|-------------|
| `Name` | - | Unique job identifier |
| `PollingInterval` | `00:01:00` | Time between reconciliation cycles |
| `ServiceType` | `Sonarr` | `Sonarr` or `Radarr` |
| `RemotePath` | - | Base path on seedbox |
| `StagingPath` | - | Local staging directory |
| `QBittorrentCategory` | - | Filter torrents by category |
| `QBittorrentTags` | `[]` | Filter torrents by tags |
| `CompletedCategory` | `null` | Category for archived torrents |
| `TransferParallelism` | `2` | Max concurrent transfers |
| `MaxTransferRetries` | `3` | Retry attempts for failed transfers |
| `TransferRetryDelay` | `00:05:00` | Cooldown between retries |
| `ImportTimeout` | `1.00:00:00` | Max time to wait for import |
| `Enabled` | `true` | Enable/disable this job |

## Sonarr/Radarr Setup

### Download Client Configuration

In Sonarr/Radarr, configure your qBittorrent download client:

1. Go to Settings → Download Clients
2. Add qBittorrent with your seedbox URL
3. Set the category (e.g., `tv-sonarr` or `movies-radarr`)
4. Enable "Remove Completed" if desired

### Remote Path Mapping

Configure remote path mappings so Sonarr/Radarr can find files in staging:

1. Go to Settings → Download Clients → Remote Path Mappings
2. Add mapping:
   - Host: Your seedbox hostname (or qBittorrent URL host)
   - Remote Path: Seedbox download path (e.g., `/downloads/tv`)
   - Local Path: Your staging path (e.g., `/staging/tv`)

This tells Sonarr/Radarr to look in staging for completed downloads.
