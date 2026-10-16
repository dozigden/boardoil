import type { Page } from '@playwright/test';

// Let the server publish its realtime event, but hold the HTTP response until
// the scenario has checked the surface after realtime delivery.
export async function holdCardCreationResponse(page: Page, boardId: number) {
  let release!: () => void;
  let responseReady!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const ready = new Promise<void>(resolve => { responseReady = resolve; });
  await page.route(`**/api/boards/${boardId}/cards`, async route => {
    const response = await route.fetch();
    responseReady();
    await gate;
    await route.fulfill({ response });
  });
  return { release, ready };
}
