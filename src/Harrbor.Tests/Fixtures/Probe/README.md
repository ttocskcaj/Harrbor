# ffprobe fixtures

`ffprobe -v error -show_streams -show_format -of json` output used by `MediaProbeParserTests` and
`DoviP7ToP81ConverterTests`. They are trimmed to the fields Harrbor reads, modelled on real ffprobe 7–9
output for UHD remuxes (structure checked against a real ffprobe run; DV side data follows the
`DOVI configuration record` layout ffprobe emits).

| File | Content |
|------|---------|
| `p7-single-track.json` | DV profile 7 (BL+EL+RPU in one HEVC track), HDR10 side data, 2 audio, 2 PGS subs, a cover-art attachment exposed as an `attached_pic` video stream |
| `p7-dual-track.json` | DV profile 7 with the EL in a second HEVC track |
| `p8-1.json` | DV profile 8, BL compatibility id 1 |
| `p5.json` | DV profile 5 |
| `hdr10.json` | HDR10 (mastering display + CLL side data), no DV |
| `sdr.json` | SDR HEVC |
| `real-p8-1-source.json` | **Unedited** ffprobe 8.1 output for a synthetic DV 8.1 MKV (x265 HDR10 BL + `dovi_tool generate`/`inject-rpu`, muxed by mkvmerge 99 with 2 AAC + 1 SRT) |
| `real-p8-1-remuxed.json` | **Unedited** ffprobe output after running `DoviP7ToP81`'s exact ffmpeg → `dovi_tool -m 2 convert --discard` → mkvmerge commands on that source |
