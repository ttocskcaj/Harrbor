# Harrbor Design System

**Harrbor** is a self-hosted media orchestration dashboard for homelab users. It coordinates moving completed downloads from a remote seedbox (qBittorrent) into a local media library (Sonarr/Radarr) over SFTP.

The organizing metaphor is a **pipeline**: every download is a *release* that flows left-to-right through a fixed six-phase pipeline — **Discover → Download → Transfer → Import → Cleanup → Archival**. Releases are grouped into *jobs* (e.g. `tv-shows`, `movies`) that act as parallel lanes. The UI does two jobs: **monitor** the live pipeline (what's flowing, what's stuck) and **configure** service connections and jobs directly in the app.

Mood: a calm, dark **control-tower** aesthetic. Deep slate/navy base, a single warm **amber "harbor light"** accent (the warm point of light in a cool, dark room), with **teal** reserved for the in-progress/transferring status. Dense but highly legible, monospace for paths and IDs. Dark mode is the only mode. Cleaner and more considered than Sonarr / Radarr / Overseerr.

## Sources
Authored from the written **`uploads/Harrbor-UI-Design-Brief.md`** (the full v1 UI design brief — mental model, screen scope 5.1–5.8, configuration UX principles, state matrix, mood, references). There was **no attached codebase, Figma, or existing brand kit** — all colors, type choices, components, screens, logo and wordmark are original inventions for the concept and should be reconciled against real product code/design if it exists.

> Settled decisions taken from the brief: **amber accent over teal**; **IBM Plex Sans + IBM Plex Mono** (endorsed in the brief over Inter/Geist); dark-first with light theme merely token-feasible; tabular figures for all live numerics; status never by color alone.

---

## CONTENT FUNDAMENTALS

How Harrbor writes copy — it sounds like a competent ops tool, not a marketing site.

- **Voice:** terse, operational, factual. State what happened and what to do. No exclamation, no cheerleading, no emoji.
- **Person:** mostly impersonal / imperative. Label with nouns ("Needs attention", "Connection health") and act with verbs ("Test connection", "Retry", "Recheck"). Avoid "we"; "you" only in help hints ("Credentials are stored encrypted at rest").
- **Casing:** Sentence case for everything — buttons, headings, labels. UPPERCASE (with wide `--tracking-caps` letter-spacing) is reserved for tiny eyebrow labels and kind tags (`TORRENT`, `SFTP`, `x265`).
- **Numbers & machine data:** always monospace, always the real thing. Sizes as `4.31 GB`, latency as `24ms`, IDs as `rel_8fa3c19d`, paths as `/mnt/seedbox/complete/tv-shows/…`, errors as raw codes (`ECONNREFUSED`). Tabular figures for aligned counts.
- **Status language:** short and unambiguous — `Connected` / `Degraded` / `Offline` / `Not set`; `Stuck`, `Failed`, `Live`, `stalled`, `handshake ok`.
- **Failures are plain-language + evidence:** a human sentence ("Import failed — no matching series in Sonarr") paired with the mono release id, phase, and age ("4m ago"). Every failure offers one clear action (Retry / Inspect / Skip / Override).
- **Examples to imitate:** "Transfer stalled over SFTP (0 B/s for 6m)" · "Cleanup skipped: seed ratio below 1.0" · "handshake ok · 38ms".

---

## VISUAL FOUNDATIONS

- **Palette:** a deep navy-slate ramp (`--slate-1000` app well → `--slate-100` headings) carries almost everything. The interface is ~90% neutral. **Amber** (`--accent` / `--amber-500`, hover `--amber-400`) is the single warm accent — the "harbor light" — used sparingly for primary actions, active/selected UI, focus, links, and highlights. It doubles as the **warning/retrying/degraded** status (amber = "this earns your attention", whether a CTA or a warning; the two never collide because one is a solid button and the other a soft icon+label chip). **Teal** (`--status-active`) is reserved for the **in-progress/transferring** status — cool = in motion. Green/red are semantic (success / failed); blue is rare info. Never introduce a second decorative hue.
- **Phase hues:** the six phases get a cool→warm walk (slate-blue → cyan → teal → green → gold → violet) so a release's position reads by color as well as column. These are structural, not decorative — only use them to denote phase.
- **Type:** two families. **IBM Plex Sans** for all interface text (weights 400/450/500/600); **IBM Plex Mono** for anything a machine emitted (paths, IDs, hosts, sizes, hashes, latency). Compact scale tuned for density — default UI body is 13px; big dashboard numbers 30–40px, semibold, tight tracking, tabular figures.
- **Spacing & layout:** 4px base grid, tight operational rhythm. Fixed 232px sidebar, 52px topbar, scrolling content. Dense multi-column grids (metric rows, the six-phase board, two-pane settings).
- **Backgrounds:** flat solid fills only — no images, no gradients, no textures, no patterns. Depth comes from the slate ramp (darker = deeper) plus shadow, never from color washes. The app well (`--slate-1000`) is darker than the page (`--slate-950`).
- **Cards & surfaces:** `--surface-card` fill, 1px `--border-subtle` hairline, `--radius-lg` (10px) corners, soft deep shadow (`--shadow-md`) plus a 3%-white inset top edge (`--edge-light`) for a subtle hardware bevel. Panels/columns use `--surface-panel`. Inputs are *recessed* — darker `--surface-inset` fill with an inset shadow well.
- **Corners:** restrained. 3–10px on controls and cards; pill only for progress tracks and toggles. Nothing is bubble-round.
- **Borders:** 1px hairlines everywhere (`--border-subtle` dividers, `--border-strong` on controls). Left accent bars (2–3px) mark active nav and toned metric tiles. Column headers carry a 2px top border in their phase hue.
- **Shadows & glow:** two systems. **Elevation** = deep, low-chroma black shadows (things float over a dark well). **Glow** = the status language — a soft 3px colored halo ring around status dots, active chips, focused inputs (`--accent-glow` amber, `--status-active-glow` teal, `--green-glow`, `--red-glow`). Focus ring is always amber and always visible.
- **Motion:** quick and mechanical — 120–320ms, `--ease-out`, no bounce/overshoot. Buttons depress 1px on press. Live items use a slow 2.2s opacity **pulse**; in-flight transfer bars use a flowing diagonal **dash** animation. Reduced motion should drop both.
- **Hover / press:** hover lifts elevation and shifts to `--surface-hover` or an accent border; ghost controls fade text from muted → body. Press = 1px downward translate and a darker fill. No scale-up hovers.
- **Transparency & blur:** used sparingly — color-mixed tints for phase-chip backgrounds and glow halos. No glassmorphism / backdrop blur.
- **Imagery:** none by default. This is a data tool; poster art / thumbnails are intentionally absent. If ever added, keep them small, cool-toned and secondary.

---

## ICONOGRAPHY

- **No custom icon library was provided.** The system currently uses a **minimal set of Unicode glyphs** as functional icons (`↻` refresh, `⚙` settings, `＋` add, `⚡` test, `◴` spinner, `⇥`/`◵`/`≡` nav) rendered in the current text color. This keeps the build dependency-free and legible at small sizes.
- **Emoji are never used.** Status is communicated by the `StatusDot` atom (a colored dot with halo/pulse) and `Badge`, not by emoji or colored symbols.
- **Recommended upgrade:** adopt **Lucide** (https://lucide.dev) — thin 1.5px stroke, rounded caps — which matches the calm/engineered tone. Swap the Unicode placeholders for Lucide SVGs when a real icon set is wanted. **Flagged for the user:** confirm an icon library preference and I'll wire it in (CDN or copied SVGs) and replace the placeholders.
- The **logo** is an original geometric mark (`assets/harrbor-mark.svg`): an **amber** beacon of concentric signal arcs converging on a bright node — the "harbor light" / orchestration pulse reaching a port, matching the brief's nautical-but-restrained direction. A wordmark lockup pairs it with the name in Plex Sans 600 (`assets/harrbor-wordmark.svg`). These are placeholders invented for the concept; replace with a real mark if one exists.

---

## FONT SUBSTITUTION NOTE
Fonts load from the **Google Fonts CDN** (`tokens/fonts.css`), not self-hosted binaries — IBM Plex Sans + IBM Plex Mono. If you want offline/self-hosted webfonts (or a different type pairing), attach the files and I'll add real `@font-face` rules. Because the fonts come via CDN `@import`, the compiler reports 0 embedded font-faces — this is expected.

---

## Index / manifest

Root
- `styles.css` — global entry point (import this one file). `@import` lines only.
- `tokens/` — `colors.css`, `typography.css`, `spacing.css`, `effects.css`, `fonts.css`.
- `assets/` — `harrbor-mark.svg`, `harrbor-wordmark.svg`.
- `guidelines/` — foundation specimen cards (Colors, Type, Spacing, Brand).
- `SKILL.md` — Agent-Skills-compatible entry for downloading this system.

Components (`window.HarrborDesignSystem_2a1653.*`)
- **forms/** — `Button`, `IconButton`, `TextField`, `SecretField`, `Select`, `Switch`
- **display/** — `StatusDot`, `Badge`, `ProgressBar`, `MetricTile`, `PhaseChip`, `Card`
- **pipeline/** — `FlowRail`, `ReleaseCard`, `AttentionItem`, `ConnectionRow`

UI kits
- **ui_kits/harrbor-app/** — the Harrbor app: interactive Dashboard, Pipeline board, and Connections (settings) screens sharing one app shell. `index.html` is the entry.

### Signature character (v1.1)
Three devices give Harrbor its identity beyond a generic dark admin theme:
1. **FlowRail** (`components/pipeline/FlowRail.jsx`) — the whole six-phase pipeline as one connected line; segments shimmer where releases are moving, stuck phases carry an amber-ringed badge. It's the Dashboard hero (replaced the old throughput bars) and a condensed column-jump nav strip on the Pipeline view.
2. **Harbor-light** — the amber accent behaving like a beacon: a low (~5%) `--harbor-floor` warm gradient on the content well, a single focal `--harbor-bloom` behind the live phase, and a slow `beacon-breath` halo behind the brand mark. All motion respects `prefers-reduced-motion`.
3. **Instrument voice** — monospace `eyebrow` kickers on cards, a ticking mono **telemetry line** in the sidebar footer (throughput · in-flight · last-sync · uptime), and tabular figures on every live number.

### Intentional additions
Because no source defined a component inventory, the set above was authored to exactly cover the two monitoring views and the connection form in the brief — no speculative primitives. `PhaseChip`, `ReleaseCard`, `ConnectionRow` and `AttentionItem` are domain-specific to Harrbor's pipeline model.
