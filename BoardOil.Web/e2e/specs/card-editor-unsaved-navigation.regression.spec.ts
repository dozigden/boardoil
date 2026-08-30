import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('card navigation asks before discarding an edited description', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression unsaved card navigation');
  const cardTitle = 'Guarded card draft';
  const originalDescription = 'Persisted description';
  const unsavedDescription = 'Unsaved description';
  const card = await api.createCard(board, 'Todo', cardTitle, originalDescription);
  const boardPage = new BoardPage(page);

  await boardPage.open(board.id);
  await boardPage.openCard('Todo', cardTitle);
  const cardDialog = page.getByRole('dialog').filter({
    has: page.getByRole('heading', { name: new RegExp(`#${card.id}`) })
  });
  const descriptionEditor = cardDialog.getByLabel('Card description', { exact: true });
  await descriptionEditor.fill(unsavedDescription);

  await test.step('keep the draft when discard is cancelled', async () => {
    await page.evaluate(() => window.history.back());
    const confirmDialog = page.getByRole('dialog').filter({
      has: page.getByRole('heading', { name: 'Discard unsaved changes' })
    });
    await expect(confirmDialog).toBeVisible();
    await confirmDialog.getByRole('button', { name: 'Cancel', exact: true }).click();

    await expect(confirmDialog).toBeHidden();
    await expect(page).toHaveURL(`/boards/${board.id}/card/${card.id}`);
    await expect(descriptionEditor).toHaveText(unsavedDescription);
  });

  await test.step('leave without changing the persisted card when discard is accepted', async () => {
    await page.evaluate(() => window.history.back());
    const confirmDialog = page.getByRole('dialog').filter({
      has: page.getByRole('heading', { name: 'Discard unsaved changes' })
    });
    await expect(confirmDialog).toBeVisible();
    await confirmDialog.getByRole('button', { name: 'Discard', exact: true }).click();

    await expect(page).toHaveURL(`/boards/${board.id}`);
    await boardPage.openCard('Todo', cardTitle);
    await expect(page.getByRole('dialog').getByLabel('Card description', { exact: true }))
      .toHaveText(originalDescription);
  });
});
