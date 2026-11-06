// Shared bootstrap for ad-hoc driving of the Harrbor UI in headless Chromium.
//
// This exists so a throwaway driver script is ~5 lines instead of ~30: it
// resolves Playwright out of ui/node_modules (wherever the driver script itself
// lives), preflights the dev server, wires up console/page-error capture, and
// hands you a `page` plus a `shot()` helper that writes a PNG to a predictable
// path you can then Read back.
//
// Usage (from a scratch .mjs file, run with `node`):
//
//   import { drive } from '<repo>/.claude/skills/test-ui/scripts/harness.mjs'
//   await drive(async (page, { shot }) => {
//     await page.getByRole('button', { name: 'List' }).click()
//     await shot('list-view')
//   })
//
// Options (2nd arg to drive): { baseURL, viewport, shotDir, headed,
// failOnConsoleError }. shotDir also reads the SHOT_DIR env var; it defaults to
// the current working directory. Set it to your scratchpad so PNGs land
// somewhere you can Read them.

import { fileURLToPath, pathToFileURL } from 'node:url'
import { dirname, join, isAbsolute } from 'node:path'
import { createRequire } from 'node:module'
import { mkdirSync } from 'node:fs'

const here = dirname(fileURLToPath(import.meta.url))
// scripts -> test-ui -> skills -> .claude -> <repo>, then /ui
const uiDir = join(here, '..', '..', '..', '..', 'ui')

async function loadChromium() {
  // Resolve @playwright/test relative to ui/ so the driver script can live
  // anywhere (e.g. the scratchpad) and still find the project's Playwright.
  const req = createRequire(join(uiDir, 'package.json'))
  let entry
  try {
    entry = req.resolve('@playwright/test')
  } catch {
    throw new Error(
      `@playwright/test not found under ${uiDir}. Install it (see SKILL.md): ` +
        `cd ui && PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1 pnpm add -D @playwright/test@1.59.0`,
    )
  }
  // @playwright/test is CJS; under ESM interop its exports land on `default`.
  const mod = await import(pathToFileURL(entry).href)
  const chromium = mod.chromium ?? mod.default?.chromium
  if (!chromium) throw new Error('Could not find `chromium` export in @playwright/test')
  return chromium
}

async function preflight(baseURL) {
  try {
    const res = await fetch(baseURL, { signal: AbortSignal.timeout(3000) })
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
  } catch (err) {
    throw new Error(
      `Dev server not reachable at ${baseURL} (${err.message}).\n` +
        `Start it first — from ui/: pnpm dev  (and the backend on :8080: ` +
        `dotnet run --project src/Harrbor/Harrbor.csproj).`,
    )
  }
}

export async function drive(fn, opts = {}) {
  const {
    baseURL = 'http://localhost:5173',
    viewport = { width: 1280, height: 720 },
    shotDir = process.env.SHOT_DIR || process.cwd(),
    headed = false,
    failOnConsoleError = false,
  } = opts

  await preflight(baseURL)
  const chromium = await loadChromium()

  const outDir = isAbsolute(shotDir) ? shotDir : join(process.cwd(), shotDir)
  mkdirSync(outDir, { recursive: true })

  let browser
  try {
    browser = await chromium.launch({ headless: !headed })
  } catch (err) {
    if (/Executable doesn't exist|download new browsers/.test(err.message)) {
      throw new Error(
        `Playwright's browser build doesn't match the cache. Pin the ` +
          `@playwright/test version whose chromium revision is already in ` +
          `~/.cache/ms-playwright (currently 1.59.0 -> chromium 1217).\n` +
          `Original error: ${err.message}`,
      )
    }
    throw err
  }

  const errors = []
  const page = await browser.newPage({ viewport })
  page.on('console', (m) => {
    if (m.type() === 'error') errors.push(`console.error: ${m.text()}`)
  })
  page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`))

  const shots = []
  const shot = async (name, shotOpts = {}) => {
    const file = name.endsWith('.png') ? name : `${name}.png`
    const path = join(outDir, file)
    await page.screenshot({ path, fullPage: true, ...shotOpts })
    shots.push(path)
    console.log(`📸 ${path}`)
    return path
  }

  try {
    await page.goto(baseURL)
    await fn(page, { shot, browser, baseURL, errors })
  } finally {
    if (errors.length) {
      console.log(`\n⚠ ${errors.length} console/page error(s):`)
      for (const e of errors) console.log(`   ${e}`)
    } else {
      console.log('\n✓ no console/page errors')
    }
    if (shots.length) console.log(`\n${shots.length} screenshot(s) written to ${outDir}`)
    await browser.close()
  }

  if (failOnConsoleError && errors.length) {
    process.exitCode = 1
  }
  return { errors, shots }
}
