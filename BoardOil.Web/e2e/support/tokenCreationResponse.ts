import type { Page } from '@playwright/test';

export async function rejectNextTokenCreation(
  page: Page,
  endpoint: string,
  error: { message: string; validationErrors?: Record<string, string[]> },
  responseReady: Promise<void> = Promise.resolve()
) {
  const url = `**${endpoint}`;
  await page.route(url, async route => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }
    await responseReady;
    await route.fulfill({ status: 400, json: { success: false, ...error } });
    await page.unroute(url);
  });
}
