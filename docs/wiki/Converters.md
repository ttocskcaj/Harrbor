# Converters

Converters run in the [Conversion phase](How-It-Works.md#5-conversion). Each one inspects a media file and can rewrite it losslessly before Sonarr/Radarr import it. To enable converters for a job, list them in its `Converters` setting, in the order they should run:

```json
{ "Name": "movies", "ServiceType": "Radarr", "Converters": ["DoviP7ToP81"] }
```

Shared settings (tool paths, dry run, minimum file size) are under [`Conversion`](Configuration.md#conversion).

## Available converters

### `DoviP7ToP81`

Converts **Dolby Vision profile 7**, the dual-layer format of UHD Blu-ray remuxes, to **profile 8.1**. Many clients, such as LG TVs running Jellyfin for webOS, can't play profile 7 but do play 8.1 (and HDR10 players can still use the HDR10 base layer).

- **Applies to:** `.mkv` files whose first video stream is HEVC with a Dolby Vision profile 7 configuration record.
- **Skips:** profile 5, profile 8, HDR10, HDR10+ and SDR files.
- **Leaves untouched, with a warning:** dual-track profile 7 (the enhancement layer in a separate track), and files with more than one video stream.
- **How it works:**
  1. `ffmpeg` extracts the video stream as Annex-B HEVC and pipes it into `dovi_tool -m 2 convert --discard`. This drops the enhancement layer and converts the RPU to profile 8.1. The base layer is copied bit for bit.
  2. `mkvmerge` muxes the new video first, followed by every non-video track from the original: all audio, subtitles, chapters, attachments and tags. It keeps the video's language, track name and frame rate, and the file title.
- **Verification:** the output must have DV profile 8 with BL compatibility id 1, a duration within `DurationToleranceSeconds` of the source, and the same number of audio and subtitle streams.
- **Disk space:** about twice the file size is needed temporarily, next to the file. Harrbor checks this before it starts.
- **Tools:** `ffprobe`, `ffmpeg`, `mkvmerge`, `dovi_tool` (all included in the Docker image).
- **Note:** profile 7 FEL (full enhancement layer) discs lose the FEL's extra detail. This is the usual trade-off of a P7→8.1 conversion.

## Converting an existing library

The same code can be run once over files already in your library, without the database or any jobs:

```sh
docker run --rm -v /media/movies:/media/movies ghcr.io/ttocskcaj/harrbor:latest \
  convert /media/movies --converter DoviP7ToP81 --dry-run
```

- `convert <path>`: a file or a directory (searched recursively).
- `--converter <Name>`: may be repeated. Converters run in the order given.
- `--dry-run`: report what would be converted without writing anything.
- `--min-size <bytes>`: overrides `Conversion:MinFileSizeBytes`.

It prints one line per file and converter, then a summary. The exit code is `0` on success, `1` if any conversion failed, and `2` for a usage or configuration error. `Conversion` settings are read from `appsettings*.json` in the working directory and from environment variables (e.g. `Conversion__Tools__mkvmerge=/usr/bin/mkvmerge`).

Each file is replaced with a rename. Files hardlinked elsewhere (e.g. a seeding copy) therefore keep the original content on the other link, and are not modified through it.

Set `-e PUID=... -e PGID=...` to the owner of the library files, as for the main service. A converted file keeps the original's permission bits, but is owned by the user that ran the conversion.

## Writing a converter

1. Implement `IMediaConverter` in `src/Harrbor/Services/Conversion/Converters/`:
   - `Name`: the config key.
   - `Extensions`: e.g. `[".mkv"]`.
   - `RequiredTools`: keys of `Conversion:Tools`, checked at startup.
   - `Evaluate(probe)`: a pure, cheap decision, `Convert`, `Skip(reason)` or `Unsupported(reason)`.
   - `ConvertAsync(input, output, probe, scratchDir, ct)`: write to `output`, never touch `input`. Run tools through `IProcessRunner` (no shell; pipelines are supported) so tests can fake them.
   - `Verify(source, output)`: compare the two probes.
2. Register it in `ConversionServiceCollectionExtensions.AddMediaConversion`:
   `services.AddSingleton<IMediaConverter, MyConverter>();`
   If it needs settings, bind them from `Conversion:Converters:<Name>`.
3. Add tests: `Evaluate` against ffprobe JSON fixtures in `src/Harrbor.Tests/Fixtures/Probe/`, `Verify` pass/fail cases, and `ConvertAsync` argument building with a fake `IProcessRunner`. See `DoviP7ToP81ConverterTests`.

The pipeline handles file discovery, the minimum size filter, temp and scratch files, verification, the atomic replace, re-probing between chained converters, dry run and cleanup. You don't need to change it to add a converter.
