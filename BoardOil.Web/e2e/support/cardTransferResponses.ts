import type { Page } from '@playwright/test';

export async function delayColumns(page: Page, boardId: number) {
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  let started!: () => void;
  const requested = new Promise<void>(resolve => { started = resolve; });
  let completed!: () => void;
  const finished = new Promise<void>(resolve => { completed = resolve; });
  const url = `**/api/boards/${boardId}/columns`;
  await page.route(url, async route => {
    const response = await route.fetch();
    started();
    await ready;
    await route.fulfill({ response });
    await page.unroute(url);
    completed();
  });
  return { requested, release, finished };
}

export async function showContributorDestination(page: Page, boardId: number) {
  await page.route('**/api/boards', async route => {
    const response = await route.fetch();
    const body = await response.json();
    body.data = body.data.map((board: { id: number }) => board.id === boardId ? { ...board, currentUserRole: 'Contributor' } : board);
    await route.fulfill({ response, json: body });
  });
}

export async function failNextLookup(page: Page, endpoint: string, message: string) {
  const url = `**${endpoint}`;
  await page.route(url, async route => {
    await route.fulfill({ status: 503, json: { success: false, message } });
    await page.unroute(url);
  });
}

export async function rejectNextTransfer(page: Page, boardId: number, cardId: number) {
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  const url = `**/api/boards/${boardId}/cards/${cardId}/transfer`;
  await page.route(url, async route => {
    await ready;
    await route.fulfill({ status: 409, json: { success: false, message: 'Destination changed. Please try again.' } });
    await page.unroute(url);
  });
  return release;
}
