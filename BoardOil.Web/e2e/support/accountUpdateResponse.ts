import type { Page } from '@playwright/test';

export async function holdRejectedAccountUpdate(page: Page, id: number) {
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  const url = `**/api/system/users/${id}`;
  await page.route(url, async route => {
    if (route.request().method() !== 'PUT') {
      await route.continue();
      return;
    }
    await ready;
    await route.fulfill({ status: 400, json: { success: false, message: 'Account update is temporarily unavailable.' } });
    await page.unroute(url);
  });
  return release;
}
