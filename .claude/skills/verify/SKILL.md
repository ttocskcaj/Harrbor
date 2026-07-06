---
name: verify
description: Run Harrbor hermetically (no real seedbox/Sonarr/qBittorrent) to observe the orchestration pipeline end-to-end. Use when verifying changes to phase handlers, the worker loop, migrations, or startup behavior.
---

# Verifying Harrbor changes at runtime

Harrbor is a background orchestration service; its surface is the startup log +
per-cycle phase logs. `appsettings.local.json` in `src/Harrbor/` points at the
user's REAL seedbox stack — never run the app from the repo directory, or it
may kick off real rclone transfers.

## Hermetic run recipe

Run the built DLL from a scratch content root (config is loaded relative to cwd,
so `appsettings.local.json` in the repo is not picked up):

1. Scratch dir with: `appsettings.default.json` (copy from `src/Harrbor/`),
   your own `appsettings.json`, `staging/`, `data/`, `bin/`.
2. **Fake rclone** on PATH (startup hard-fails without a working `rclone lsf`;
   transfers only run when a release has `TransferStatus=Pending`):
   ```sh
   #!/bin/sh
   case "$1" in obscure) echo x;; *) exit 0;; esac
   ```
3. **Sonarr stub** (discovery runs on the FIRST cycle uncaught — an unreachable
   Sonarr kills the host): python http server on the port your config names,
   serving `/api/v3/system/status` → `{"appName":"Sonarr"}`,
   `/api/v3/queue` → `{"page":1,"pageSize":100,"totalRecords":0,"records":[]}`,
   `/api/v3/history` → same shape with `pageSize:50`.
4. qBittorrent/Radarr can point at closed ports: startup treats them as
   warnings, and the download phase early-returns when nothing is
   `DownloadStatus=Pending`.
5. Config: one job, `PollingInterval "00:00:05"`, `StagingPath` inside the
   scratch dir, Radarr `Enabled: false`.
6. Launch:
   ```sh
   cd $SCRATCH && PATH=$SCRATCH/bin:$PATH ASPNETCORE_URLS=http://127.0.0.1:18080 \
     timeout -s INT 14 dotnet <repo>/src/Harrbor/bin/Debug/net10.0/Harrbor.dll
   ```
   SIGINT gives graceful shutdown; 14s ≈ startup + 2–3 cycles.

## Driving mid-pipeline phases

Boot once to create `data/harrbor.sqlite` (migrations auto-apply — check
`__EFMigrationsHistory`), stop, then seed rows with `sqlite3` at the state you
want (enums are ints in declaration order; timestamps are `'YYYY-MM-DD HH:MM:SS'`
TEXT). E.g. `TransferStatus=2 (Completed)` + staged files under
`staging/<basename of RemotePath>/` drives the extraction phase on next boot.
Seed a row `InProgress` to observe the startup stuck-state reset.

Observe: console log (Serilog `MinimumLevel.Default: Debug` shows per-phase
lines and the cycle summary `[D:…/T:…/E:…/I:…]=n`), the staging dir, and final
DB state via `sqlite3`.
