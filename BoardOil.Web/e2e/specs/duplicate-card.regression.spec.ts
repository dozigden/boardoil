import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('duplicate a card as an editable draft before creating it', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression duplicate card');
  const sourceTitle = 'Duplicate source';
  const duplicateTitle = 'Duplicate result';
  const description = 'Description copied to the duplicate';
  const tagName = 'Duplicate tag';
  await api.createCard(board, 'Todo', sourceTitle, description, [tagName]);
  const boardPage = new BoardPage(page);

  await boardPage.open(board.id);
  await boardPage.openCard('Todo', sourceTitle);

  await test.step('open a complete client-side duplicate draft', async () => {
    await page.getByRole('button', { name: 'Card actions' }).click();
    await page.getByRole('menu', { name: 'Card actions' })
      .getByRole('button', { name: 'Duplicate', exact: true })
      .click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { level: 3 })).toContainText('Duplicate');
    await expect(dialog.getByLabel('Card description', { exact: true })).toHaveText(description);
    await expect(dialog.getByLabel(tagName, { exact: true })).toBeVisible();
    await expect(boardPage.card('Todo', sourceTitle)).toHaveCount(1);
  });

  await test.step('edit and create the duplicate without changing the source', async () => {
    const dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: sourceTitle, exact: true }).click();
    const titleInput = dialog.getByRole('textbox', { name: 'Card title' });
    await titleInput.fill(duplicateTitle);
    await titleInput.press('Enter');

    await dialog.getByTitle('Select column').click();
    await dialog.getByRole('menu', { name: 'Select column' })
      .getByRole('button', { name: 'In Progress', exact: true })
      .click();
    await dialog.getByRole('button', { name: 'Create duplicate card' }).click();

    await expect(dialog).toBeHidden();
    await expect(boardPage.card('Todo', sourceTitle)).toBeVisible();
    await expect(boardPage.card('In Progress', duplicateTitle)).toBeVisible();
  });

  await test.step('reload the independently persisted cards', async () => {
    await page.reload();
    await expect(boardPage.card('Todo', sourceTitle)).toBeVisible();
    await expect(boardPage.card('In Progress', duplicateTitle)).toBeVisible();

    await boardPage.openCard('In Progress', duplicateTitle);
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Card description', { exact: true })).toHaveText(description);
    await expect(dialog.getByLabel(tagName, { exact: true })).toBeVisible();
  });
});
