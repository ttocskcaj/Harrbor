# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Harrbor is a .NET 10 media orchestration service that coordinates transfers between a remote seedbox (running qBittorrent) and a local homelab (running Sonarr/Radarr). It solves the distributed state coordination problem of syncing torrents while maintaining seeding on the remote and avoiding re-download loops.

## Build and Run Commands

```bash
# Build
dotnet build src/Harrbor/Harrbor.csproj

# Run
dotnet run --project src/Harrbor/Harrbor.csproj

# Run tests
dotnet test src/Harrbor.Tests/Harrbor.Tests.csproj

# Run a single test
dotnet test src/Harrbor.Tests/Harrbor.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Docker build
docker build -t harrbor .

# EF Core migrations (from src/Harrbor directory)
dotnet ef migrations add MigrationName
dotnet ef database update
```

## Architecture

### Core Workflow (OrchestrationWorker)

The service runs as a `BackgroundService` with a reconciliation loop for each configured job. Each cycle processes releases through nine sequential phases:

1. **Discover** - Query Sonarr/Radarr queue for new downloads, create `TrackedRelease` records
2. **Process Downloads** - Check qBittorrent for completed torrents, update status when progress=100%
3. **Process Transfers** - Run rclone transfers from seedbox into the hidden holding dir `{StagingPath}/.harrbor/{name}` (with parallelism limits and retry logic)
4. **Process Extractions** - Unpack zip/rar/7z archives in the holding dir (SharpCompress; detects old-style `.r00` and new-style `.partN.rar` volume sets)
5. **Process Conversions** - Run the job's `Converters` (e.g. `DoviP7ToP81`) over media files; failures don't block the release
6. **Publish** - Atomically move the release from the holding dir to `{StagingPath}/{name}`, so the *arr only sees finished files
7. **Process Imports** - Verify Sonarr/Radarr has imported from staging to final library
8. **Process Cleanup** - Delete files from staging after confirmed import
9. **Process Archival** - Change torrent category in qBittorrent to prevent re-processing

The staged item is the torrent's top-level item (`TrackedRelease.RemoteItemPath`: root folder, or the file for a torrent without one), not qBittorrent's `content_path` (`RemotePath`), which is the file itself for a single-file torrent inside a root folder. Files are transferred with `rclone copyto`, directories with `rclone copy`. Use `TrackedRelease.GetWorkingItemPath()` / `GetFinalItemPath()` / `GetHoldingItemPath()` rather than building staging paths by hand.

### Key Abstractions

- **IMediaService** (`ISonarrClient`, `IRadarrClient`) - Abstraction over Sonarr/Radarr APIs for queue queries and import confirmation
- **IRemoteStorageService** - rclone-based SFTP transfers from seedbox
- **IArchiveExtractionService** - SharpCompress-based archive extraction in staging
- **IMediaConverter** - Pluggable lossless file rewrite (`Evaluate` / `ConvertAsync` / `Verify`); registered in `ConversionServiceCollectionExtensions`, resolved by name via `ConverterRegistry`. See `docs/wiki/Converters.md`
- **IConversionService** - Runs converters over a release: probe, convert to a hidden temp file, verify, atomic replace, re-probe between chained converters
- **IMediaProbeService** / `MediaProbeParser` - `ffprobe` JSON → typed `MediaProbe`
- **IProcessRunner** - Runs external processes and stdout→stdin pipelines without a shell; kills process trees on cancellation
- **IQBittorrentClient** - Wrapper around qBittorrent API for torrent state queries and category management
- **MediaServiceResolver** - Resolves the correct media service based on job's `ServiceType`

### Data Model

`TrackedRelease` tracks each download through the pipeline with status enums for each phase:
- `DownloadStatus`, `TransferStatus`, `ExtractionStatus`, `ConversionStatus`, `ImportStatus`, `CleanupStatus`, `ArchivalStatus`
- `PublishedAtUtc` gates Import: a release is importable only once it has been published from the holding dir
- `RemoteItemPath` is null until the download completes; rows from before 0.0.9 are filled in at startup by `RemoteItemPathBackfill`
- Error tracking: `ErrorCount`, `LastError`, `LastErrorAtUtc`
- Timestamps for each phase completion

### Configuration

Jobs are configured in `appsettings.json` under the `Jobs.Definitions` array. Each job specifies:
- `ServiceType` (Sonarr/Radarr)
- `RemotePath` / `StagingPath`
- `QBittorrentCategory` for filtering
- `TransferParallelism`, `MaxTransferRetries`, `TransferRetryDelay`
- `ExtractionEnabled` to unpack archives after transfer (default true)
- `Converters` list of converter names to run in order (default empty); shared settings, tool paths and `ScratchPath` (intermediates dir, e.g. on SSD) in the `Conversion` section
- `CompletedCategory` for archival

Use `appsettings.local.json` for local development overrides (not committed).

### CLI

`dotnet Harrbor.dll convert <path> --converter <Name> [--dry-run]` runs converters over a path without DB or jobs (library backfill). Handled at the top of `Program.cs` by `ConversionCli`.

## Key Packages

- `QBittorrent.Client` - qBittorrent WebUI API client
- `SharpCompress` - zip/rar/7z extraction (MIT licensed, pure .NET)
- `Microsoft.EntityFrameworkCore.Sqlite` - Persistent state storage
- `Microsoft.Extensions.Http.Resilience` - HTTP client retry/circuit breaker policies
- `Serilog` - Structured logging

## Health Checks

- `/health` - All checks
- `/health/ready` - Database + external services
- `/health/live` - Basic liveness (app running)
