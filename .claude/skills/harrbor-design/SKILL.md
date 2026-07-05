---
name: harrbor-design
description: Use this skill to generate well-branded interfaces and assets for Harrbor, either for production or throwaway prototypes/mocks/etc. Contains essential design guidelines, colors, type, fonts, assets, and UI kit components for prototyping.
user-invocable: true
---

Read the README.md file within this skill, and explore the other available files.
If creating visual artifacts (slides, mocks, throwaway prototypes, etc), copy assets out and create static HTML files for the user to view. If working on production code, you can copy assets and read the rules here to become an expert in designing with this brand.
If the user invokes this skill without any other guidance, ask them what they want to build or design, ask some questions, and act as an expert designer who outputs HTML artifacts _or_ production code, depending on the need.

Quick orientation:
- `styles.css` is the single global CSS entry point — link it and you get every token and font.
- Tokens live in `tokens/` (colors, typography, spacing, effects, fonts). Reach for semantic aliases (`--surface-card`, `--accent`, `--text-body`, `--phase-transfer`).
- Components are React (`.jsx`) under `components/` and exported on `window.HarrborDesignSystem_2a1653`. Each has a `.d.ts` (props) and `.prompt.md` (usage).
- `ui_kits/harrbor-app/` is a full interactive recreation of the product — read it to match layout and interaction patterns.
- Brand is dark-only, calm control-tower: navy-slate base, single teal accent, amber for attention, IBM Plex Sans + Mono, no emoji, monospace for all machine data.
