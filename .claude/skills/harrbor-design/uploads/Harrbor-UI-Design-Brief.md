# Harrbor — UI Design Brief

## 1. Project Overview

**Harrbor** is a self-hosted media orchestration service. It runs quietly in the background on a homelab server (in Docker) and coordinates the movement of completed downloads from a **remote seedbox** (qBittorrent) to a **local media library** (Sonarr / Radarr), transferring files over SFTP while preserving seeding on the seedbox.

Harrbor needs a **web UI that is both the window into the service and the way it is configured.** The UI has two jobs of roughly equal importance:

1. **Operate** — let the operator *see what the service is doing, catch when something is stuck, and step in when needed*, from a UI rather than raw server logs.
2. **Configure** — let the operator set up and manage **service connections and jobs entirely in the UI.**

The name is a pun on **harbor / port**: media arrives from remote waters and is brought safely into dock. That maritime idea is available as a light thematic anchor, but the product should read as a serious, modern infrastructure tool — not a themed novelty.

---

## 2. The Core Mental Model (read this before designing anything)

Everything Harrbor does is a **release** (a TV episode or movie download) flowing through a fixed **six-phase pipeline**:

1. **Discover** — found in the Sonarr/Radarr queue, now being tracked
2. **Download** — waiting for the torrent to finish on qBittorrent
3. **Transfer** — copying files from seedbox to local staging over SFTP (this is the slow, progress-bearing step; failures auto-retry)
4. **Import** — waiting for Sonarr/Radarr to import from staging into the library
5. **Cleanup** — deleting staging files after import is confirmed
6. **Archival** — moving the torrent to a "completed" category so it keeps seeding but isn't re-processed

Releases are grouped into **Jobs** (e.g. `tv-shows`, `movies`). Each job watches one category, runs on its own polling interval, and has its own transfer/retry settings. Think of jobs as **parallel lanes**, and each release as an item traveling left-to-right through the six phases within its lane.

Two things this configuration depends on:
- **Connections** — the external systems Harrbor talks to: **qBittorrent** (the seedbox client), **Sonarr** and **Radarr** (the library managers), and **Remote Storage / SFTP** (the seedbox filesystem). Nothing works until these are configured and reachable.
- **Jobs** — the per-category pipelines the operator defines on top of those connections.

**The two questions the UI must answer at a glance:** while operating — *is everything flowing, and if not, what is stuck and where?*; while configuring — *are my connections healthy and are my jobs set up the way I intend?* Design around those first.

---

## 3. Who Uses It

- **Primary (and essentially only) user: the homelab operator / self-hoster.** Technically confident, often runs 10–30 other containerized services, and mentally benchmarks Harrbor against apps they already use daily: **Sonarr, Radarr, qBittorrent, Overseerr, Tautulli, Portainer, Dozzle**.
- Usage splits into two modes: **setup/reconfiguration** (occasional, deliberate — connecting services, adding or tuning a job) and **glance-driven monitoring** (frequent, brief — confirming things are healthy or diagnosing why a title didn't appear).
- **Single-user, trusted-network tool.** No multi-tenancy, no roles/permissions, no marketing surfaces. Assume the person already knows what a seedbox and an *arr app are.
- Frequently checked **from a phone** (often through a reverse proxy while away from home), so mobile is a first-class case for monitoring. Heavy configuration is expected to happen on desktop, but connection forms should remain usable on mobile.
- **A first-run setup experience is in scope.** A fresh install starts empty — no connections, no jobs — and must guide the operator through connecting services and creating a first job before Harrbor can do anything. This is a technical setup flow, not a marketing onboarding funnel — keep it lean and let steps be revisited.

**Design implication:** this audience rewards **information density, honest status, and speed** over hand-holding. But configuration is the one place to allow a little more breathing room and guidance, because a wrong value here silently breaks the whole service. Match the polish of the best *arr-ecosystem apps — their settings pages are strong references for this exact work — and aim to feel a touch calmer and more considered.

---

## 4. Design Goals

1. **Observability first (operate mode).** Health of the whole system readable in under 3 seconds from the landing view.
2. **Make "stuck" obvious.** Stalled transfers, exhausted retries, and import timeouts must surface above healthy, flowing items — never buried in a uniform list.
3. **Support targeted intervention.** Drill into a single release, understand *why* it's stuck, and act (retry, cancel, re-run a phase).
4. **Make configuration safe and confidence-building (configure mode).** Configuration happens entirely in the UI, so config screens must prevent invalid states, validate against the real services before saving, protect secrets, and make it clear when and how changes take effect. A misconfiguration should be caught in the form, not discovered later in the logs.
5. **Respect density without becoming noise.** Show a lot, with clear hierarchy.
6. **Feel native to a dark homelab dashboard,** while being cleaner and more legible than the average one.

---

## 5. Scope — Screens & Views

Design these as the v1 set. Ranked by priority; if effort must be cut, cut from the bottom. Configuration (5.4–5.6) is core scope alongside monitoring.

### 5.1 Dashboard / Overview *(highest priority)*
The home view. At-a-glance system health.
- **Connection status** for each dependency: qBittorrent, Sonarr, Radarr, Seedbox/SFTP — each clearly up / down / degraded, with a quick path to its settings.
- **Pipeline summary**: how many releases sit in each of the six phases right now.
- **Attention panel**: anything failed, retrying, or timed out, pulled to the top — including **misconfiguration / unreachable-connection** warnings.
- **Throughput / activity**: recent completions, current aggregate transfer activity.
- **Per-job snapshot**: each job's name, enabled state, item count, time until next poll.

### 5.2 Pipeline / Active Queue *(highest priority — the heart of the app)*
The live view of releases moving through the six phases.
- Needs a layout that expresses **left-to-right phase progression** and **per-job lanes**. A **column-per-phase (Kanban-style) board** and a **dense filterable table/list** are both plausible; please explore both and recommend one (or a toggle). The board tells the flow story; the table scales better. Consider 5 items vs. 200.
- Per release: title, job/lane, current phase, time in phase, and — during **Transfer** — a **progress indicator** (% and/or speed).
- Failing/retrying/stuck items must be visually distinct.
- Filter by job, phase, and status.

### 5.3 Release Detail *(high priority)*
Drill-down for a single release.
- A **phase timeline** showing which phases are done, current, and pending, with timestamps.
- Transfer detail (files, size, speed, retry count / next retry).
- Relevant **log excerpt** scoped to this release.
- **Actions**: retry, cancel, force-advance / re-run a phase (final action set TBC with engineering).

### 5.4 Jobs — List & Editor *(high priority — full CRUD)*
Manage the per-category pipelines.
- **Jobs list**: each job's name, enabled/disabled **toggle**, category filter, polling interval, current item count, and next run. Row actions to **edit, clone, delete**.
- **Create / edit job form**, covering every setting a job supports: name, watched category, enabled state, polling interval, transfer settings, retry policy, and staging/destination paths. (Get the authoritative field list and validation rules from engineering — the form must expose all of them.)
- **Clone** is a first-class action: operators commonly duplicate an existing job (e.g. `tv-shows` → `movies`) and tweak it. Design for that, not just blank creation.
- **Delete** needs a clear confirmation, with a specific warning if the job currently has in-flight releases (what happens to them must be spelled out — TBC with engineering).
- A job references connections — surface which connections it uses and warn if a referenced connection is unhealthy or missing.

### 5.5 Connections / Settings *(high priority — editable, with test-before-save)*
Configure the external systems Harrbor depends on: **qBittorrent, Sonarr, Radarr, Remote Storage / SFTP.**
- An **editable form per connection** with the real fields for each type (host/URL, port, credentials or API keys, base paths, etc. — get the authoritative field list from engineering).
- A **"Test connection"** action on every form that hits the real dependency and returns a clear pass/fail with a **useful error** (auth failed vs. host unreachable vs. wrong port). Strongly prefer a flow where the operator tests before saving; at minimum, warn when saving an untested or failing connection.
- **Live connection-health indicator** per connection (reachable / degraded / down), consistent with the Dashboard.
- **Secret handling is critical** — see Section 6.

### 5.6 First-Run Setup *(high priority — the first thing new users hit)*
A fresh install has no connections and no jobs, so nothing works until it's configured. Design a **guided setup**:
- A stepped **wizard**: connect qBittorrent → connect Sonarr/Radarr → connect Remote Storage/SFTP → create a first job. Each connection step is **testable inline** before proceeding; steps can be revisited.
- A clear **"you're done" / next steps** end state that lands the user on the Dashboard.
- Reuse the same connection and job forms from 5.4/5.5 inside the wizard rather than inventing separate ones.

### 5.7 History *(medium priority)*
Completed, archived, and permanently failed releases. Searchable/filterable (job, outcome, date). Rows link to the same detail view.

### 5.8 Logs *(lower priority)*
A filterable live log stream (structured logging with severity levels). Filter by level, search text, tail/pause. Reuse patterns this audience knows (e.g. Dozzle).

---

## 6. Configuration UX Principles (please treat as first-class)

Configuration is a load-bearing part of the product. These principles apply across Jobs (5.4), Connections (5.5), and Setup (5.6):

- **Secrets are write-only.** API keys and SFTP passwords/keys are never rendered back in plaintext after saving. Show a masked "•••• set" state with a **Replace** action that reveals an empty field for a new value. Never round-trip a secret to the browser just to display it.
- **Test against reality before committing.** Config is only trustworthy if validated against the actual qBittorrent / Sonarr / Radarr / SFTP endpoint. Every connection form needs a real "Test connection", and the design should make passing the test feel like part of saving.
- **Validate inline, field-level.** Required fields, URL format, port range (1–65535), interval bounds, path format — caught as the user types or on blur, with specific messages. The UI should make it hard to persist a config that would crash the service.
- **Be explicit about when changes take effect.** Saving may apply live, on the next poll, or require a service restart (TBC with engineering — see Section 12). Whichever it is, the UI must state it plainly (e.g. an inline note or post-save banner: "Applies on next poll" / "Restart required to take effect"). Never leave the operator guessing whether their change is active.
- **Protect unsaved work.** Explicit Save / Cancel, a visible **dirty state**, and a warning before navigating away with unsaved changes.
- **Fail safe.** If a save is rejected by the backend (validation or write failure), keep the user's input, explain what failed, and never leave config half-written.
- **Be complete.** Every setting the service supports is reachable somewhere in the UI, named consistently with how the community and docs already refer to it, so nothing forces the operator back to a config file.

---

## 7. Key States to Design (please don't skip these)

Design the full state matrix — this is where homelab tools usually fall short and where Harrbor can feel more finished.

**Operate states**
- **Empty** — nothing in the pipeline yet.
- **Loading** — first paint and background refreshes (data polls frequently; refreshes must not cause layout jumps).
- **Healthy / flowing** — the happy path.
- **Degraded** — a dependency is unreachable but the app itself is fine.
- **Error / stuck** — failed transfer, retries exhausted, import timeout. Most design care goes here.
- **Dense** — 100+ active releases across multiple jobs.

**Configure states**
- **Unconfigured / first run** — zero connections, zero jobs (leads into the setup wizard).
- **Form: pristine / editing / dirty / saving / saved / save-failed.**
- **Field validation errors** — inline and specific.
- **Connection test: idle / testing / passed / failed** (with an actionable error message).
- **Secret set vs. unset** — masked "set" state vs. empty field.
- **Destructive confirmations** — deleting a job (especially one with in-flight releases), replacing a secret, discarding unsaved changes.

---

## 8. Mood & Visual Direction

**Keywords:** *calm control tower · trustworthy · dark · dense-but-legible · engineered · quietly nautical.*

Imagine a port authority's operations screen: lots of live status, nothing frantic, everything where you expect it. The operator should feel **in command and reassured**.

- **Theme:** **Dark mode is the default and primary target.** A light theme is a nice-to-have; token things so both are feasible, but don't let light-mode compromise the dark-first experience.
- **Base palette:** deep desaturated slate / navy (leans into the harbor idea without costume-y nautical blues). Surfaces from a few elevation steps, not one flat black.
- **Accent:** a single warm **amber "harbor light"** accent, used sparingly for primary actions, active states, and highlights against the slate base. Amber over teal is a settled decision — the warm point of light in a cool, dark room is the intended feeling.
- **Semantic status system is critical.** Define a clear, colorblind-safe palette for the status vocabulary — *idle/waiting, in-progress/transferring, success/imported, retrying/degraded, failed/stuck* — and reuse it for **connection health** (reachable / degraded / down) and **form/validation states** (valid / warning / error). Don't rely on color alone; pair with icon and/or label. These states appear constantly and span both operate and configure surfaces, so nail them early.
- **Typography:** two families, no more. A clean, highly legible UI sans — **Inter** is the safe default; **IBM Plex Sans** or **Geist** if a little more character is wanted — paired with a **monospace** (**JetBrains Mono** or **IBM Plex Mono**) for file paths, sizes, IDs, speeds, keys, and logs. Because so many numbers update live (phase counts, percentages, transfer speeds), use **tabular / monospaced figures** for all numeric data so values don't jitter or shift width as they refresh. Prioritize small-size legibility and clear numerals over personality.
- **Iconography:** simple, consistent line icons. A restrained nautical motif (dock, buoy, anchor, waypoint) is welcome in the logo/empty-states; keep working and config screens literal and functional.
- **Motion:** subtle and purposeful — progress movement, phase transitions, gentle refresh, save/test feedback. Nothing distracting during a glance.

**Tone to avoid:** SaaS-marketing gradients and hero illustrations, gamified badges, heavy whitespace that sacrifices density, or leaning so hard into the nautical pun that it reads as a toy.

---

## 9. Reference Points

- **Direct genre peers (match/beat their polish):** Sonarr, Radarr, qBittorrent Web UI, Overseerr, Tautulli.
- **For configuration UX specifically:** the **Sonarr / Radarr settings pages** (connections, indexers, test buttons, secret fields) are the closest peer to what this project needs — study them. Also **Portainer**, **pfSense/OPNsense**, and **UniFi** for dense-but-usable infra config and setup wizards.
- **For calm operational dashboards:** Portainer, Dozzle, Grafana.
- **For pipeline/flow visualization:** Kanban boards, CI/CD pipeline views, shipment-tracking timelines.

Overseerr is a good north star for "an *arr-adjacent tool that feels genuinely designed." Aim there or better.

---

## 10. Technical Constraints (so designs land cleanly in build)

- The UI is a **React single-page app** served as static files from the existing .NET backend, all in one Docker image. Map designs onto a mainstream React component library (e.g. **shadcn/ui + Tailwind**, or **Mantine**) and lean on patterns those libraries express well — including **forms, inputs, selects, toggles, validation, modals/drawers, and toasts**, which matter as much as tables and cards.
- **Config is read and written through the API**, with save and validation round-trips — design forms assuming a save can succeed, fail validation, or fail to write.
- **Responsive** from ~360px up to wide desktop. Dashboard and Pipeline must stay useful on a phone; connection forms should remain usable there too, even if heavy setup is expected on desktop.
- Data is **near-real-time via polling** (possibly live push later); values tick and refresh frequently — keep layouts stable on update.
- **Accessibility:** WCAG AA contrast (easy to miss on dark themes), full keyboard navigation (including all forms), visible focus states, and status never conveyed by color alone.
- **Auth:** none required for v1 (single-user, trusted network) — no login beyond the config **setup wizard** in 5.6. This is a natural place a future auth step could slot in, so don't design in a way that would fight that later.

---

## 11. Deliverables Requested

1. **Lo-fi wireframes** for the views in Section 5, with a recommendation on the **Pipeline layout** (board vs. table vs. toggle) and on the **setup wizard** flow, each with rationale.
2. **A visual design system**: color tokens (dark-first, plus the shared semantic status/health/validation palette), typography, spacing, elevation, and core components — explicitly including **form controls** (text/number inputs, selects, toggles, secret/masked fields, inline validation, test-connection button states), cards, tables/rows, badges/status chips, progress indicators, tabs, drawers/modals, and toasts.
3. **High-fidelity mockups** of: Dashboard, Pipeline, and Release Detail (each in **healthy** and **error/stuck** states); the **Job editor** and a **Connection form** (each showing pristine, validation-error, testing, test-passed, and test-failed states); and the **first-run setup wizard**. Include at least one **mobile** frame of Dashboard/Pipeline and one of a connection form.
4. **Empty / loading / unconfigured (first-run)** states for Dashboard, Pipeline, Jobs, and Connections.
5. A **logo / wordmark** for "Harrbor" and a favicon (nautical motif welcome, restraint required).
6. Design-token values and specs in a **developer-friendly handoff** (Figma with named tokens/variables is ideal).

---

## 12. Open Questions to Resolve With the Owner / Engineering

- **Authoritative config schema:** the exact fields and validation rules for each connection type and for a job, so the forms expose all of them.
- **Apply semantics:** does saving config take effect live, on next poll, or require a service restart? This directly shapes the save/apply UI in Section 6.
- **Secret storage & handling:** how secrets are stored server-side and whether "Test connection" can run using a not-yet-saved secret.
- **Job deletion with in-flight releases:** what should happen to releases mid-pipeline when their job is deleted or disabled?
- **Operator actions per release:** final list (retry / cancel / re-run phase / ignore?).
- **Realistic scale:** typical and peak simultaneous releases, and number of jobs — to validate the Pipeline layout choice.
- **Nautical theme appetite:** subtle accent, or a fuller motif in branding and empty states?
