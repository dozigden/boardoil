import { defineConfig, devices, type ReporterDescription } from '@playwright/test';

const baseURL = process.env.BOARDOIL_E2E_BASE_URL ?? 'http://127.0.0.1:4173';
const junitOutput = process.env.BOARDOIL_E2E_JUNIT_OUTPUT;
const reporters: ReporterDescription[] = [['list']];

if (junitOutput) {
  reporters.push(['junit', { outputFile: junitOutput }]);
}

export default defineConfig({
  testDir: './e2e/specs',
  outputDir: './test-results/playwright',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 30_000,
  expect: {
    timeout: 5_000
  },
  reporter: reporters,
  use: {
    ...devices['Desktop Chrome'],
    baseURL,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    video: 'off'
  },
  projects: [
    {
      name: 'chromium',
      use: { browserName: 'chromium' }
    }
  ]
});
