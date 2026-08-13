import { defineConfig, devices } from '@playwright/test';

const DemoPort = 4174;
const DemoBaseUrl = `http://127.0.0.1:${DemoPort}`;

export default defineConfig({
  testDir: './e2e/demo',
  outputDir: './test-results/playwright-demo',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 30_000,
  expect: {
    timeout: 5_000
  },
  reporter: [['list']],
  use: {
    ...devices['Desktop Chrome'],
    baseURL: DemoBaseUrl,
    colorScheme: 'light',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'off'
  },
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' }
    }
  ],
  webServer: {
    command: `npm run preview:demo -- --host 127.0.0.1 --port ${DemoPort} --strictPort`,
    url: DemoBaseUrl,
    reuseExistingServer: false,
    timeout: 30_000
  }
});
