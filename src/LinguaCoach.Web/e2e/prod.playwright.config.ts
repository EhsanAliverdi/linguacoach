import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: 'prod-admin-screenshots.spec.ts',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  use: {
    baseURL: 'https://speakpath.app',
    viewport: { width: 1440, height: 900 },
    trace: 'off',
  },
  // No webServer — hits production directly
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
