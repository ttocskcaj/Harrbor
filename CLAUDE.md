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

# Run all tests (unit + integration, discovered via the solution)
dotnet test

# Run only unit tests / only integration tests
dotnet test src/Harrbor.Tests.Unit/Harrbor.Tests.Unit.csproj
dotnet test src/Harrbor.Tests.Integration/Harrbor.Tests.Integration.csproj

# Run a single test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Docker build
docker build -t harrbor .

# EF Core migrations (dotnet-ef is a local tool; run `dotnet tool restore` once)
# From the src/Harrbor directory:
dotnet ef migrations add MigrationName
dotnet ef database update

# UI (from ui/ directory)
pnpm install          # restore dependencies
pnpm dev              # dev server on :5173 with proxy to backend on :8080
pnpm build            # type-check (tsc -b) + production build to ui/dist
```

## Architecture

### Core Workflow (OrchestrationWorker)

`OrchestrationWorker` is a `BackgroundService` running a single scheduler loop (5-second tick) that re-reads enabled jobs from the database every tick and starts a reconciliation cycle for each job whose polling interval has elapsed (no overlapping cycles per job). Each cycle runs in its own DI scope via `JobCycleRunner` and processes releases through six sequential phases:

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

All operational configuration lives in the SQLite database and is managed through the REST API under `/api` — changes apply live (connections on next use, jobs on the next scheduler tick):
- **Connections** (`ServiceConnections` table, one JSON-settings row per type): qBittorrent, Sonarr, Radarr, Remote Storage/SFTP. Accessed via the singleton `IConnectionStore`; rows carry a `Version` stamp consumers use to rebuild expensive clients only when config changes. Secrets are stored plaintext (Servarr-style) but are **write-only through the API** (`passwordSet`/`apiKeySet` flags; null keeps, `""` clears, value replaces).
- **Jobs** (`Jobs` table, typed columns): full CRUD at `/api/jobs`; `POST /api/connections/{type}/test` tests unsaved payloads with stored-secret fallback. `JobDefinition` remains the immutable per-cycle snapshot type passed to phase handlers (`Job.ToDefinition()`).

Only bootstrap config stays file-based (`appsettings.default.json`): `Serilog` and `Harrbor:DataPath`. `appsettings.local.json` (not committed) can still override those.

## Key Packages

- `QBittorrent.Client` - qBittorrent WebUI API client
- `Microsoft.EntityFrameworkCore.Sqlite` - Persistent state storage
- `Microsoft.Extensions.Http.Resilience` - HTTP client retry/circuit breaker policies
- `Serilog` - Structured logging

## Health Checks

- `/health` - All checks
- `/health/ready` - App status + Database
- `/health/live` - Basic liveness (app running)
