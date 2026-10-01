import { expect, test } from '../fixtures/boardOilTest';

for (const operation of ['create', 'update', 'delete'] as const) {
  test(`a pending card-type ${operation} does not close a different editor`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Card type ${operation} completion`);
    const previous = await api.createCardType(board.id, 'Keep this editor');
    await api.createCardType(board.id, 'Pending type');
    const previousEditorUrl = `/boards/${board.id}/admin/card-types/${previous.id}`;
    await page.goto(previousEditorUrl);
    await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toHaveValue('Keep this editor');
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();
    if (operation === 'create') {
      await page.getByRole('button', { name: 'Add card type', exact: true }).click();
    } else {
      await page.getByRole('button', { name: 'Edit card type Pending type', exact: true }).click();
    }
    if (operation !== 'delete') {
      await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Saved type');
    }

    let release!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    let pending = false;
    await page.route(`**/api/boards/${board.id}/card-types{,/**}`, async route => {
      if (route.request().method() === 'GET') {
        await route.continue();
        return;
      }
      pending = true;
      await gate;
      await route.continue();
    });

    try {
      if (operation === 'delete') {
        await page.getByRole('button', { name: 'Delete card type', exact: true }).click();
        await page.getByRole('dialog', { name: 'Delete card type', exact: true })
          .getByRole('button', { name: 'Delete', exact: true }).click();
      } else {
        const buttonName = operation === 'create' ? 'Create card type' : 'Save card type';
        await page.getByRole('button', { name: buttonName, exact: true }).click();
      }
      await expect.poll(() => pending).toBe(true);
      await page.goBack();
      await page.goBack();
      await expect(page).toHaveURL(previousEditorUrl);
      await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toHaveValue('Keep this editor');
      await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toBeDisabled();
    } finally {
      release();
    }

    await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toBeEnabled();
    await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toHaveValue('Keep this editor');
    await expect(page).toHaveURL(previousEditorUrl);
  });
}

test('accepting deletion after leaving the card-type editor sends no delete or client error', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card type confirmation');
  await api.createCardType(board.id, 'Keep this type');
  const requests: string[] = [];
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => {
    if (request.method() === 'DELETE' && request.url().includes(`/boards/${board.id}/card-types/`)) {
      requests.push(request.url());
    }
    if (request.method() === 'POST' && request.url().endsWith('/api/system/error-logs:report-client-error')) {
      errors.push(request.postDataJSON().message);
    }
  });
  await page.goto(`/boards/${board.id}/admin/card-types`);
  await page.getByRole('button', { name: 'Edit card type Keep this type', exact: true }).click();
  await page.getByRole('button', { name: 'Delete card type', exact: true }).click();
  const confirmation = page.getByRole('dialog', { name: 'Delete card type', exact: true });
  await expect(confirmation).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types`);
  await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(confirmation).toBeHidden();
  await expect(page.getByRole('button', { name: 'Edit card type Keep this type', exact: true })).toBeEnabled();
  expect(requests).toEqual([]);
  expect(errors).toEqual([]);
});

test('a pending missing-card-type lookup does not redirect after leaving the editor', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card type lookup navigation');
  let loads = 0;
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  const completions: Promise<void>[] = [];
  await page.route(`**/api/boards/${board.id}/card-types`, async route => {
    loads++;
    if (loads === 1) {
      await route.continue();
      return;
    }
    const completion = (async () => {
      await gate;
      await route.fulfill({ json: { success: true, data: [] } });
    })();
    completions.push(completion);
    await completion;
  });
  try {
    await page.goto(`/boards/${board.id}/admin/card-types/2147483647`);
    await expect.poll(() => loads).toBeGreaterThan(1);
    await page.getByRole('link', { name: 'Open current board', exact: true }).click();
    await expect(page).toHaveURL(`/boards/${board.id}`);
  } finally {
    release();
  }
  await Promise.all(completions);
  await expect(page.getByRole('article', { name: 'Todo column' })).toBeVisible();
  await expect(page).toHaveURL(`/boards/${board.id}`);
});
