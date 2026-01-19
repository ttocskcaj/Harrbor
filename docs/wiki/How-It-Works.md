# How It Works

Harrbor runs as a background service that processes releases through a six-phase pipeline. Each configured job runs its own reconciliation loop at a configurable polling interval.

## The Pipeline

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Discover   │───>│  Download   │───>│  Transfer   │
│  (Sonarr/   │    │  (qBit      │    │  (rclone    │
│   Radarr)   │    │   complete) │    │   SFTP)     │
└─────────────┘    └─────────────┘    └─────────────┘
                                             │
                                             ▼
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Archival   │<───│   Cleanup   │<───│   Import    │
│  (category  │    │  (delete    │    │  (Sonarr/   │
│   change)   │    │   staging)  │    │   Radarr)   │
└─────────────┘    └─────────────┘    └─────────────┘
```

## Phases

### 1. Discover

Finds new downloads in the Sonarr/Radarr queue and starts tracking them.

### 2. Download

Waits for torrents to finish downloading in qBittorrent.

### 3. Transfer

Copies completed files from the seedbox to local staging via SFTP. Failed transfers are automatically retried.

### 4. Import

Waits for Sonarr/Radarr to import files from staging into your media library.

### 5. Cleanup

Deletes files from staging after import is confirmed.

### 6. Archival

Moves the torrent to a "completed" category in qBittorrent. This prevents re-processing while preserving seeding.
