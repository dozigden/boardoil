import type { Page } from '@playwright/test';

export async function rejectNextCardTypeSave(
  page: Page,
  boardId: number,
  error: { message: string; validationErrors?: Record<string, string[]> }
) {
  const url = new RegExp(`/api/boards/${boardId}/card-types(?:/\\d+)?$`);
  await page.route(url, async route => {
    if (!['POST', 'PUT'].includes(route.request().method())) {
      await route.continue();
      return;
    }

    await route.fulfill({ status: 400, json: { success: false, ...error } });
    await page.unroute(url);
  });
}

export async function rejectNextCardTypeDefault(page: Page, boardId: number, cardTypeId: number) {
  const url = `**/api/boards/${boardId}/card-types/${cardTypeId}/default`;
  await page.route(url, async route => {
    await route.fulfill({ status: 400, json: {
      success: false,
      message: 'The default card type could not be changed. Please try again.'
    } });
    await page.unroute(url);
  });
}

export async function rejectNextCardTypeDelete(page: Page, boardId: number, cardTypeId: number) {
  const url = `**/api/boards/${boardId}/card-types/${cardTypeId}`;
  await page.route(url, async route => {
    if (route.request().method() !== 'DELETE') {
      await route.continue();
      return;
    }
    await route.fulfill({ status: 400, json: { success: false, message: 'This card type cannot be deleted yet.' } });
    await page.unroute(url);
  });
}
