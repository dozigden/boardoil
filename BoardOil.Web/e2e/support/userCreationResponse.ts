import type { Page } from '@playwright/test';

export async function rejectNextUserCreation(
  page: Page,
  error: { message: string; validationErrors?: Record<string, string[]> }
) {
  const url = '**/api/system/users';
  await page.route(url, async route => {
    if (route.request().method() !== 'POST') {
      await route.continue();
      return;
    }
    await route.fulfill({ status: 400, json: { success: false, ...error } });
    await page.unroute(url);
  });
}
