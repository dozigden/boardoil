import { expect, test } from '../fixtures/boardOilTest';

test('external URL edits can be cancelled or applied before saving the card', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression card external URL');
  const card = await api.createCard(board, 'Todo', 'External URL card');
  const cancelledUrl = 'https://example.test/cancelled';
  const savedUrl = 'https://example.test/saved';

  await page.goto(`/boards/${board.id}/card/${card.id}`);
  const dialog = page.getByRole('dialog');
  const externalLink = dialog.getByRole('group', { name: 'External link' });

  await test.step('cancel an external URL draft', async () => {
    await externalLink.getByRole('button', { name: 'Add', exact: true }).click();
    await externalLink.getByRole('textbox', { name: 'External URL' }).fill(cancelledUrl);
    await externalLink.getByRole('button', { name: 'Cancel external URL edit' }).click();

    await expect(externalLink.getByRole('link')).toHaveCount(0);
    await expect(externalLink.getByRole('button', { name: 'Add', exact: true })).toBeVisible();
  });

  await test.step('apply and persist a different external URL', async () => {
    await externalLink.getByRole('button', { name: 'Add', exact: true }).click();
    await externalLink.getByRole('textbox', { name: 'External URL' }).fill(savedUrl);
    await externalLink.getByRole('button', { name: 'Apply external URL' }).click();
    await expect(externalLink.getByRole('link')).toHaveAttribute('href', savedUrl);
    await dialog.getByRole('button', { name: 'Save card' }).click();

    await page.goto(`/boards/${board.id}/card/${card.id}`);
    await expect(page.getByRole('dialog').getByRole('group', { name: 'External link' }).getByRole('link'))
      .toHaveAttribute('href', savedUrl);
  });
});
