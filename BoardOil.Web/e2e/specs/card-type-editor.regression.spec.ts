import { expect, test } from '../fixtures/boardOilTest';

for (const operation of ['create', 'update', 'delete'] as const) {
  test(`card-type ${operation} updates the catalogue and closes the editor`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Card type ${operation}`);
    await api.createCardType(board.id, 'Existing type');
    await page.goto(`/boards/${board.id}/admin/card-types`);
    if (operation === 'create') {
      await page.getByRole('button', { name: 'Add card type', exact: true }).click();
    } else {
      await page.getByRole('button', { name: 'Edit card type Existing type', exact: true }).click();
    }

    if (operation === 'delete') {
      await page.getByRole('button', { name: 'Delete card type', exact: true }).click();
      await page.getByRole('dialog', { name: 'Delete card type', exact: true })
        .getByRole('button', { name: 'Delete', exact: true }).click();
    } else {
      await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Saved type');
      const buttonName = operation === 'create' ? 'Create card type' : 'Save card type';
      await page.getByRole('button', { name: buttonName, exact: true }).click();
    }

    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types`);
    if (operation === 'delete') {
      await expect(page.getByRole('button', { name: 'Edit card type Existing type', exact: true })).toHaveCount(0);
    } else {
      await expect(page.getByRole('button', { name: 'Edit card type Saved type', exact: true })).toBeVisible();
    }
  });
}

test('cancelling card-type deletion keeps the type and editor', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card type confirmation');
  const cardType = await api.createCardType(board.id, 'Keep this type');
  await page.goto(`/boards/${board.id}/admin/card-types/${cardType.id}`);
  await page.getByRole('button', { name: 'Delete card type', exact: true }).click();
  const confirmation = page.getByRole('dialog', { name: 'Delete card type', exact: true });
  await confirmation.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(confirmation).toBeHidden();
  await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toHaveValue('Keep this type');
  await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types/${cardType.id}`);
});

test('the card-type list reuses its catalogue after a missing type redirect and a same-board remount', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Missing card type');
  await api.createCardType(board.id, 'Retained type');
  let catalogueLoads = 0;
  page.on('request', request => {
    if (request.method() === 'GET' && request.url().endsWith(`/api/boards/${board.id}/card-types`)) {
      catalogueLoads++;
    }
  });
  await page.goto(`/boards/${board.id}/admin/card-types/2147483647`);
  await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types`);
  await expect(page.getByRole('button', { name: 'Edit card type Retained type', exact: true })).toBeVisible();
  expect(catalogueLoads).toBe(1);

  await page.getByRole('link', { name: 'Open current board', exact: true }).click();
  await expect(page.getByRole('article', { name: 'Todo column', exact: true })).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types`);
  await expect(page.getByRole('button', { name: 'Edit card type Retained type', exact: true })).toBeVisible();
  expect(catalogueLoads).toBe(1);
});
