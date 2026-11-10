import type { Page } from '@playwright/test';

export async function rejectNextBoardSettingsWrite(page: Page, boardId: number, method: 'PUT' | 'DELETE', ready = Promise.resolve()) {
  const url = `**/api/boards/${boardId}`;
  await page.route(url, async route => {
    if (route.request().method() !== method) return route.continue();
    await ready;
    await route.fulfill({ status: 400, json: { success: false, message: 'Board operation could not be completed.' } });
    await page.unroute(url);
  });
}
