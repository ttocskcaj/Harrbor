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

The service runs as a `BackgroundService` with a reconciliation loop for each configured job. Each cycle processes releases through six sequential phases:

1. **Discover** - Query Sonarr/Radarr queue for new downloads, create `TrackedRelease` records
2. **Process Downloads** - Check qBittorrent for completed torrents, update status when progress=100%
3. **Process Transfers** - Run rclone transfers from seedbox to local staging (with parallelism limits and retry logic)
4. **Process Imports** - Verify Sonarr/Radarr has imported from staging to final library
5. **Process Cleanup** - Delete files from staging after confirmed import
6. **Process Archival** - Change torrent category in qBittorrent to prevent re-processing

### Key Abstractions

- **IMediaService** (`ISonarrClient`, `IRadarrClient`) - Abstraction over Sonarr/Radarr APIs for queue queries and import confirmation
- **IRemoteStorageService** - rclone-based SFTP transfers from seedbox
- **IQBittorrentClient** - Wrapper around qBittorrent API for torrent state queries and category management
- **MediaServiceResolver** - Resolves the correct media service based on job's `ServiceType`

### Data Model

`TrackedRelease` tracks each download through the pipeline with status enums for each phase:
- `DownloadStatus`, `TransferStatus`, `ImportStatus`, `CleanupStatus`, `ArchivalStatus`
- Error tracking: `ErrorCount`, `LastError`, `LastErrorAtUtc`
- Timestamps for each phase completion

### Configuration

Jobs are configured in `appsettings.json` under the `Jobs.Definitions` array. Each job specifies:
- `ServiceType` (Sonarr/Radarr)
- `RemotePath` / `StagingPath`
- `QBittorrentCategory` for filtering
- `TransferParallelism`, `MaxTransferRetries`, `TransferRetryDelay`
- `CompletedCategory` for archival

Use `appsettings.local.json` for local development overrides (not committed).

## Key Packages

- `QBittorrent.Client` - qBittorrent WebUI API client
- `Microsoft.EntityFrameworkCore.Sqlite` - Persistent state storage
- `Microsoft.Extensions.Http.Resilience` - HTTP client retry/circuit breaker policies
- `Serilog` - Structured logging

## Health Checks

- `/health` - All checks
- `/health/ready` - Database + external services
- `/health/live` - Basic liveness (app running)
