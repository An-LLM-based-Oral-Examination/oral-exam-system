import { defineConfig, devices } from '@playwright/test'

const port = process.env.E2E_PORT || '4173'
const isLiveApi = process.env.E2E_LIVE_API === '1'
const baseURL = process.env.E2E_BASE_URL || `http://127.0.0.1:${port}`

const webServerEnv: Record<string, string> = {}
for (const [key, value] of Object.entries(process.env)) {
  if (value !== undefined) {
    webServerEnv[key] = value
  }
}
if (!isLiveApi && !webServerEnv.VITE_GOOGLE_CLIENT_ID) {
  webServerEnv.VITE_GOOGLE_CLIENT_ID = 'e2e-google-client-id.apps.googleusercontent.com'
}

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: {
    timeout: 15_000,
  },
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : 3,
  reporter: process.env.CI ? 'line' : 'list',
  use: {
    baseURL,
    navigationTimeout: 45_000,
    trace: 'retain-on-failure',
    ...devices['Desktop Chrome'],
    launchOptions: {
      args: [
        '--use-fake-ui-for-media-stream',
        '--use-fake-device-for-media-stream',
        '--autoplay-policy=no-user-gesture-required',
      ],
    },
  },
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: `npm run dev -- --host 127.0.0.1 --port ${port}`,
        url: `http://127.0.0.1:${port}`,
        reuseExistingServer: !process.env.CI,
        timeout: 120_000,
        env: webServerEnv,
      },
})
