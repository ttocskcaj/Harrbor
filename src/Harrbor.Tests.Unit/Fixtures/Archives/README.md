# Archive test fixtures

Real archive volume sets used by `RarSequenceExtractionTests`. All contents are
deterministic so tests can regenerate the expected bytes (see the generators in
`RarSequenceExtractionTests`):

- `content.txt` — 100 lines of `Harrbor RAR fixture content line NNNN\n` (3800 bytes)
- `solidN.bin` — 4096 bytes of a SHA256 chain seeded with `harrbor-solid-N`
  (each block is the SHA256 of the previous block, starting from the UTF-8 seed)

| Set | Format | Created with |
|-----|--------|--------------|
| `rar4-oldstyle` | RAR4, old-style volumes (`.rar` + `.r00`–`.r03`) | `rar 6.24: a -ma4 -m0 -v1k -vn -ep` |
| `rar4-parts` | RAR4, new-style volumes (`.part1.rar`–`.part5.rar`) | `rar 6.24: a -ma4 -m0 -v1k -ep` |
| `rar5-parts` | RAR5, `.part1.rar`–`.part5.rar` | `rar 7.12: a -ma5 -m0 -v1k -ep` |
| `rar5-solid` | RAR5, solid, `.part1.rar`–`.part4.rar` | `rar 7.12: a -ma5 -s -m5 -v4k -ep` |
| `rar5-encrypted` | RAR5, password `testpassword` | `rar 7.12: a -ma5 -m0 -ptestpassword -ep` |
| `rar4-single` | RAR4, single volume | `rar 6.24: a -ma4 -m0 -ep` |
| `rar5-single` | RAR5, single volume | `rar 7.12: a -ma5 -m0 -ep` |
| `sevenzip-split` | 7z, split volumes (`.7z.001`–`.7z.004`) | `7z: a -t7z -mx0 -v1k` |

Notes for regeneration:

- RAR4-format archives (`-ma4`) can only be created with rar ≤ 6.x; RAR 7.x
  removed RAR4 creation. Both tools are the official rarlab.com Linux builds.
- The rar tool is proprietary and is NOT part of this repo; only these archive
  files (our own generated data) are checked in.
- All sets were verified with `unrar t` / `7z t` before check-in.
