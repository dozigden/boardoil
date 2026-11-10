import type { Page } from '@playwright/test';

export async function rejectNextSystemInfoMessageSave(
  page: Page,
  error: { message: string; validationErrors?: Record<string, string[]> }
) {
  const url = '**/api/system/system-info-message';
  await page.route(url, async route => {
    if (route.request().method() !== 'PUT') return route.continue();
    await route.fulfill({ status: 400, json: { success: false, ...error } });
    await page.unroute(url);
  });
}
