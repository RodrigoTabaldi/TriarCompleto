import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  outputDir: '../artifacts/web-verification/playwright',
  timeout: 45000,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: process.env.TRIAR_TEST_URL ?? 'http://localhost:8080',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { browserName: 'chromium', launchOptions: process.env.TRIAR_TEST_BROWSER ? { executablePath: process.env.TRIAR_TEST_BROWSER } : {} } },
    { name: 'webkit', use: { browserName: 'webkit' } },
  ],
});
