import type { Page } from '@playwright/test';

export async function offerApplicationUpdate(page: Page) {
  await page.route('**/api/version', route => route.fulfill({
    json: {
      success: true,
      statusCode: 200,
      data: { version: '1.8.0', channel: 'nightly', build: 'update-regression', commit: 'different-build' }
    },
    headers: { 'Cache-Control': 'no-store' }
  }));
}

export async function holdBoardSaveResponse(page: Page, boardId: number) {
  let release!: () => void;
  let responseReady!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const ready = new Promise<void>(resolve => { responseReady = resolve; });
  await page.route(`**/api/boards/${boardId}`, async route => {
    if (route.request().method() !== 'PUT') {
      await route.continue();
      return;
    }
    const response = await route.fetch();
    responseReady();
    await gate;
    await route.fulfill({ response });
  });
  return { release, ready };
}
