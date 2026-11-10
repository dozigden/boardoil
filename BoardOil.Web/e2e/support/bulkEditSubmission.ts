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
