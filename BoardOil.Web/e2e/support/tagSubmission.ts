import type { Page } from '@playwright/test';

export async function rejectNextTagSubmission(
  page: Page,
  boardId: number,
  method: 'POST' | 'PUT' | 'DELETE',
  error: { message: string; validationErrors?: Record<string, string[]> }
) {
  const url = new RegExp(`/api/boards/${boardId}/tags(?:/\\d+)?$`);
  await page.route(url, async route => {
    if (route.request().method() !== method) {
      await route.continue();
      return;
    }
    await route.fulfill({ status: 400, json: { success: false, ...error } });
    await page.unroute(url);
  });
}

export async function holdNextTagDefaultStyle(page: Page, boardId: number) {
  let release!: () => void;
  let requested!: () => void;
  let finished!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const request = new Promise<void>(resolve => { requested = resolve; });
  const completion = new Promise<void>(resolve => { finished = resolve; });
  await page.route(`**/api/boards/${boardId}/tags/create-default-style`, async route => {
    requested();
    await gate;
    await route.fulfill({ json: { success: true, data: { styleName: 'presets', stylePropertiesJson: '{"presetIndex":8}' } } });
    finished();
  }, { times: 1 });
  return { release, request, completion };
}
