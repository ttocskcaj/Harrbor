# How It Works

Harrbor runs as a background service that processes releases through a seven-phase pipeline. Each configured job runs its own reconciliation loop at a configurable polling interval.

## The Pipeline

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Discover   │───>│  Download   │───>│  Transfer   │───>│ Extraction  │
│  (Sonarr/   │    │  (qBit      │    │  (rclone    │    │ (unpack     │
│   Radarr)   │    │   complete) │    │   SFTP)     │    │  archives)  │
└─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘
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

### 4. Extraction

Unpacks any zip, rar, or 7z archives in the staged release so Sonarr/Radarr can import the media files. Both old-style RAR volume sets (`.rar` + `.r00`, `.r01`, ...) and new-style sets (`.part1.rar`, `.part2.rar`, ...) are detected, along with split zip and 7z volumes. Files are extracted in place next to the archives; the original archives are removed later by the Cleanup phase along with the rest of the staging item. Releases without archives pass straight through. Password-protected archives and volume sets with missing parts fail the release with a clear error. Extraction can be disabled per job with `ExtractionEnabled: false`.

### 5. Import

Waits for Sonarr/Radarr to import files from staging into your media library.

### 6. Cleanup

Deletes files from staging after import is confirmed.

### 7. Archival

Moves the torrent to a "completed" category in qBittorrent. This prevents re-processing while preserving seeding.
