import type { Page } from '@playwright/test';

export async function rejectNextBoardImport(page: Page) {
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  const url = '**/api/boards/import';
  await page.route(url, async route => {
    await ready;
    await route.fulfill({ status: 503, json: { success: false, message: 'Import is temporarily unavailable.' } });
    await page.unroute(url);
  });
  return release;
}
