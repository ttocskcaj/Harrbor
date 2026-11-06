import { defineConfig, devices } from '@playwright/test'

// Drives the UI in a real (headless) Chromium against the running dev server.
// Assumes `pnpm dev` (:5173) and the backend (:8080) are already up; if not,
// Playwright will start `pnpm dev` itself via the webServer block below.
export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: 'pnpm dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 30_000,
  },
})
