---
name: test-ui
description: Drive the Harrbor React UI in a real headless Chromium (via Playwright) with throwaway scripts — click, type, navigate, read live DOM, and capture screenshots you can look at. Use this whenever you need to actually SEE or interact with the rendered UI: verifying a frontend change looks/behaves right, reproducing a UI bug, checking layout or an empty/loading/error state, confirming a component renders without console errors, or grabbing a screenshot of a page. This is the ad-hoc counterpart to committed e2e specs — reach for it instead of writing test files when you just need to poke the running app once.
---

# Ad-hoc UI testing for Harrbor

The Harrbor UI (`ui/`) is a client-rendered React SPA (Vite + React Router).
Because it renders in the browser, `curl`/WebFetch only see the empty HTML shell
— to observe the *actual* app you have to run it in a real browser. This skill
drives a headless Chromium against the running dev server with a small
throwaway script, so you can interact with the app and pull screenshots back
into the conversation without authoring committed test files.

For committed, re-runnable regression specs use Playwright's test runner in
`ui/tests/e2e/` instead. This skill is the exploratory, "just look at it once"
path — nothing is left behind.

## How the loop works

You can't watch the browser live. You work in discrete steps: **act →
screenshot → Read the PNG → decide the next action**. Write a driver script that
performs a sequence of actions and takes screenshots at the points you care
about, run it, then Read the resulting PNGs to see what happened. Iterate by
editing the script.

## Prerequisites (check first)

1. **Dev server + backend up.** The UI runs on `:5173` and proxies API calls to
   the backend on `:8080`. Check both:
   ```bash
   curl -s -o /dev/null -w "ui:%{http_code}\n" http://localhost:5173/
   curl -s -o /dev/null -w "api:%{http_code}\n" http://localhost:8080/openapi/v1.json
   ```
   If either are down, stop and ask the user to start them.

## Writing a driver script

Put the script in your scratchpad (not the repo), use the `drive()` helper from
`scripts/harness.mjs`, and set `SHOT_DIR` so screenshots land somewhere you can
Read. Delete the script when done.

```js
// /path/to/scratchpad/drive.mjs
import { drive } from '/home/jack/Code/Harrbor/.claude/skills/test-ui/scripts/harness.mjs'

await drive(async (page, { shot }) => {
  // The helper already navigated to the baseURL (:5173) for you.
  await shot('01-initial')                                    // capture landing state

  await page.getByRole('button', { name: 'List' }).click()   // toggle Rail -> List
  await shot('02-list')

  // Read live DOM facts straight back to the terminal — often faster than a screenshot.
  console.log('view buttons:', await page.locator('button:has-text("Rail"), button:has-text("List")').allTextContents())
})
```

Run it (point `SHOT_DIR` at your scratchpad), then Read the PNGs:
```bash
SHOT_DIR=<scratchpad> node /path/to/scratchpad/drive.mjs 2>&1 | grep -vi deprecation
```

`drive(fn, opts)` gives the callback `(page, { shot, browser, baseURL, errors })`:
- `page` — a normal Playwright [Page](https://playwright.dev/docs/api/class-page). Prefer role-based locators (`getByRole`, `getByText`) and Playwright's auto-waiting over `waitForTimeout`.
- `shot(name)` — full-page screenshot to `$SHOT_DIR/<name>.png`; logs the path; returns it. Prefix names with a step number so ordering is obvious.
- Console errors and uncaught page errors are collected automatically and printed at the end (a clean run prints `✓ no console/page errors`). Pass `{ failOnConsoleError: true }` in `opts` to make the process exit non-zero if any occur.

`opts`: `baseURL` (default `http://localhost:5173`), `viewport` (default 1280×720), `shotDir` (default `$SHOT_DIR` or cwd), `headed` (default false), `failOnConsoleError` (default false).

## Knowing what to click (current UI state)

Prefer discovering the live navigation and structure
```js
for (const b of await page.locator('nav button, nav a').all())
  console.log((await b.textContent()).trim(), 'disabled=', await b.isDisabled().catch(() => false))
```

## Common actions

- **Empty vs populated states:** with no connections/jobs configured the app
  shows empty states. To see populated states either seed the backend via its
  API (`:8080`) first, or use Playwright route mocking (`page.route(...)`) to
  stub the `/api/*` responses the page fetches.
- **Different viewport / mobile:** pass `opts.viewport`, e.g. `{ width: 390, height: 844 }`.
- **Watch it render for yourself while debugging:** `headed: true` (needs a
  display; usually leave it headless).

## Cleanup

Delete the scratch driver script and any screenshots you don't need when
finished — this method is meant to leave nothing behind in the repo.
