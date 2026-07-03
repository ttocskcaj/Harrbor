# Configuration

Harrbor stores all of its operational configuration — service connections and jobs — in its SQLite database, managed through the REST API (and the web UI built on top of it). A fresh install starts unconfigured: connect your services and create a job via the API/UI and changes apply live, with no restart.

Only **bootstrap configuration** (logging and the data directory) lives in `appsettings.json` / environment variables, because it is needed before the database exists.

## Bootstrap Configuration (file / environment)

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information"
    }
  },
  "Harrbor": {
    "DataPath": "./data"
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Harrbor:DataPath` | `./data` | Directory for the SQLite database (`harrbor.sqlite`) |
| `Serilog:*` | see `appsettings.default.json` | Logging levels and sinks |

Environment variables override file values (e.g. `Harrbor__DataPath=/app/data`).

## Connections API

One connection per external service. Secrets are **write-only**: responses never include the stored password/API key, only a `passwordSet` / `apiKeySet` flag. In update requests, omitting a secret keeps the stored value, sending `""` clears it, and any other value replaces it.

| Endpoint | Methods | Notes |
|----------|---------|-------|
| `/api/connections` | GET | Summary of all four connections (`configured`, `updatedAtUtc`) |
| `/api/connections/qbittorrent` | GET, PUT | `baseUrl`, `username`, `password` (secret) |
| `/api/connections/sonarr` | GET, PUT | `baseUrl`, `apiKey` (secret), `enabled` |
| `/api/connections/radarr` | GET, PUT | `baseUrl`, `apiKey` (secret), `enabled` |
| `/api/connections/remote-storage` | GET, PUT | `host`, `port`, `user`, `password` (secret), `keyFile`, `keyFilePassphrase` (secret), `useAgent`, `transfers`, `checkers`, `additionalFlags` |
| `/api/connections/{type}/test` | POST | Tests the posted settings against the real service without saving. Omitted secrets fall back to stored values, so you can test after editing only the host. |

Validation errors come back as HTTP 400 with a field-keyed `errors` object.

## Jobs API

Each job watches one qBittorrent category and runs the six-phase pipeline on its own polling interval. Full CRUD:

| Endpoint | Methods |
|----------|---------|
| `/api/jobs` | GET, POST |
| `/api/jobs/{id}` | GET, PUT, DELETE |

Deleting a job that still has releases mid-pipeline returns **409 Conflict** with the active release count; re-issue the delete with `?force=true` to remove the job anyway (its tracked releases are kept as history but stop being processed). Disabling a job pauses it — releases freeze mid-pipeline and resume when re-enabled.

### Job fields

| Field | Default | Description |
|-------|---------|-------------|
| `name` | - | Unique job identifier |
| `enabled` | `true` | Enable/disable this job |
| `serviceType` | `sonarr` | `sonarr` or `radarr` |
| `pollingIntervalSeconds` | `60` | Time between reconciliation cycles (10–86400) |
| `remotePath` | - | Base path on seedbox (absolute) |
| `stagingPath` | - | Local staging directory (absolute, must be writable) |
| `qbittorrentCategory` | - | Filter torrents by category |
| `qbittorrentTags` | `[]` | Filter torrents by tags |
| `completedCategory` | `null` | Category for archived torrents |
| `transferParallelism` | `2` | Max concurrent transfers (1–16) |
| `maxTransferRetries` | `3` | Retry attempts for failed transfers (0–20) |
| `transferRetryDelaySeconds` | `300` | Cooldown between retries |
| `importTimeoutSeconds` | `86400` | Max time to wait for import (60–604800) |

Job responses additionally report `lastRunAtUtc`, `nextRunAtUtc`, and `activeReleaseCount`.

### When changes take effect

The scheduler re-reads jobs from the database every 5 seconds, so job changes (create, edit, enable/disable, interval) apply on the next poll cycle. Connection changes apply on next use — a cycle already in flight finishes with the settings it started with.

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
