import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  testIgnore: ['**/prod-admin-screenshots.spec.ts'],
  timeout: 30_000,
  expect: {
    timeout: 5_000,
  },
  use: {
    baseURL: 'http://127.0.0.1:4300',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'npm start -- --host 127.0.0.1 --port 4300',
    url: 'http://127.0.0.1:4300',
    reuseExistingServer: true,
    timeout: 60_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
