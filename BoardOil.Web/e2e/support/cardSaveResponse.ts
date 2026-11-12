import type { Page } from '@playwright/test';

export async function holdCardSaveResponse(
  page: Page,
  boardId: number,
  cardId: number | null,
  options: { duplicate?: boolean; reject?: boolean } = {}
) {
  let path = `/api/boards/${boardId}/cards`;
  if (cardId !== null) path += `/${cardId}`;
  if (options.duplicate) path += '/duplicate';
  const url = `**${path}`;
  let release!: () => void;
  let responseReady!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const ready = new Promise<void>(resolve => { responseReady = resolve; });
  await page.route(url, async route => {
    const response = options.reject ? null : await route.fetch();
    responseReady();
    await gate;
    if (response) {
      await route.fulfill({ response });
    } else {
      await route.fulfill({ status: 400, json: {
        success: false,
        message: 'Validation failed.',
        validationErrors: { title: ['Please choose a different title.'] }
      } });
    }
    await page.unroute(url);
  });
  return { release, ready };
}
