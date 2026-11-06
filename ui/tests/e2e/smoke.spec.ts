import { test, expect } from '@playwright/test'

test('Operate page renders without console errors', async ({ page }) => {
  const errors: string[] = []
  page.on('console', (msg) => {
    if (msg.type() === 'error') errors.push(msg.text())
  })
  page.on('pageerror', (err) => errors.push(err.message))

  await page.goto('/')

  // App shell should mount something into #root (SPA is client-rendered).
  await expect(page.locator('#root')).not.toBeEmpty()

  await page.screenshot({ path: 'test-results/operate.png', fullPage: true })

  expect(errors, `console/page errors:\n${errors.join('\n')}`).toEqual([])
})
