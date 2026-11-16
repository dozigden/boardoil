import type { Page } from '@playwright/test';

export async function holdRejectedBulkEdit(page: Page, boardId: number, message: string) {
  let release!: () => void;
  let requested!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const request = new Promise<void>(resolve => { requested = resolve; });
  await page.route(`**/api/boards/${boardId}/cards/edit`, async route => {
    requested();
    await gate;
    await route.fulfill({ status: 400, json: { success: false, message } });
  }, { times: 1 });
  return { release, request };
}

export function trackBulkEditWrites(page: Page, boardId: number) {
  const writes = { bulkEdits: 0, slickCreates: 0 };
  page.on('request', request => {
    const path = new URL(request.url()).pathname;
    if (request.method() === 'PATCH' && path === `/api/boards/${boardId}/cards/edit`) writes.bulkEdits++;
    if (request.method() === 'POST' && path === `/api/boards/${boardId}/slicks`) writes.slickCreates++;
  });
  return writes;
}
