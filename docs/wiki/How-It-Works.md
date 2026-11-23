# How It Works

Harrbor runs as a background service that processes releases through a nine-phase pipeline. Each configured job runs its own reconciliation loop at a configurable polling interval.

## The Pipeline

```
┌───────────┐   ┌───────────┐   ┌───────────┐   ┌────────────┐   ┌────────────┐
│ Discover  │──>│ Download  │──>│ Transfer  │──>│ Extraction │──>│ Conversion │
│ (Sonarr/  │   │ (qBit     │   │ (rclone → │   │ (unpack    │   │ (rewrite   │
│  Radarr)  │   │  complete)│   │  holding) │   │  archives) │   │  media)    │
└───────────┘   └───────────┘   └───────────┘   └────────────┘   └────────────┘
                                                                        │
                                                                        ▼
                ┌───────────┐   ┌───────────┐   ┌────────────┐   ┌────────────┐
                │ Archival  │<──│  Cleanup  │<──│   Import   │<──│  Publish   │
                │ (category │   │ (delete   │   │ (Sonarr/   │   │ (holding → │
                │  change)  │   │  staging) │   │  Radarr)   │   │  staging)  │
                └───────────┘   └───────────┘   └────────────┘   └────────────┘
```

## The holding directory

Sonarr/Radarr find a finished download at `{StagingPath}/{name}` (via the remote path mapping) and can import it on their next poll, about a minute later. If files appeared there while Harrbor was still working on them, the *arr could import a half-extracted or not-yet-converted file.

So new releases are transferred into a hidden holding directory on the same filesystem, `{StagingPath}/.harrbor/{name}`. Extraction and conversion run there. When they finish, the **Publish** phase moves the release to `{StagingPath}/{name}` with a single atomic rename. The *arr only sees the release once it is complete and final.

Releases tracked before the holding directory existed keep their old behaviour: they transfer straight to `{StagingPath}/{name}` and are never converted.

## Phases

### 1. Discover

Finds new downloads in the Sonarr/Radarr queue and starts tracking them.

### 2. Download

Waits for torrents to finish downloading in qBittorrent.

### 3. Transfer

Copies completed files from the seedbox into the holding directory via SFTP. Failed transfers are automatically retried.

### 4. Extraction

Unpacks any zip, rar, or 7z archives in the staged release so Sonarr/Radarr can import the media files. Both old-style RAR volume sets (`.rar` + `.r00`, `.r01`, ...) and new-style sets (`.part1.rar`, `.part2.rar`, ...) are detected, along with split zip and 7z volumes. Files are extracted in place next to the archives; the original archives are removed later by the Cleanup phase along with the rest of the staging item. Releases without archives pass straight through. Password-protected archives and volume sets with missing parts fail the release with a clear error. Extraction can be disabled per job with `ExtractionEnabled: false`.

### 5. Conversion

Runs the job's configured [converters](Converters.md), in order, over each media file in the release. A converter checks a file and can rewrite it losslessly, for example turning Dolby Vision profile 7 into profile 8.1 so that LG/webOS clients can play it. Each converter:

1. inspects the file with `ffprobe` and decides whether to convert, skip, or leave it alone as unsupported;
2. writes its output to a hidden temp file next to the original (`.<name>.harrbor-tmp.<ext>`);
3. probes the output and verifies it against the source (stream counts, duration, and so on);
4. replaces the original with an atomic rename, but only if verification passed.

If anything fails, the temp files are deleted and the original is left untouched. Conversion failures do **not** block the release: they are logged as errors, and the unconverted file is still published and imported. Files smaller than `Conversion:MinFileSizeBytes` (1 GiB by default), such as samples, are ignored. At most one release is converted per cycle, to bound CPU and disk use. Jobs with no converters skip this phase.

### 6. Publish

Moves the release from `{StagingPath}/.harrbor/{name}` to `{StagingPath}/{name}`. If the destination already exists, Harrbor refuses to overwrite it, logs an error, and retries on later cycles.

### 7. Import

Waits for Sonarr/Radarr to import files from staging into your media library. The import timeout is measured from the moment the release was published.

### 8. Cleanup

Deletes files from staging after import is confirmed, including any leftover copy in the holding directory.

### 9. Archival

Moves the torrent to a "completed" category in qBittorrent. This prevents re-processing while preserving seeding.
