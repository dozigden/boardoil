import type { Page } from '@playwright/test';

export async function holdCardMoveResponse(page: Page, boardId: number, cardId: number) {
  let release!: () => void;
  let responseReady!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const ready = new Promise<void>(resolve => { responseReady = resolve; });
  await page.route(`**/api/boards/${boardId}/cards/${cardId}/move`, async route => {
    const response = await route.fetch();
    responseReady();
    await gate;
    await route.fulfill({ response });
  });
  return { release, ready };
}
