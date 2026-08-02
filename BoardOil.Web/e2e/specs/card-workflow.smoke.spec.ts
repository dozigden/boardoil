import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('create, edit, move, and reload a card', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Smoke card workflow');
  const boardPage = new BoardPage(page);
  const originalTitle = 'Draft smoke card';
  const updatedTitle = 'Ready smoke card';

  await boardPage.open(board.id);

  await test.step('create the card', async () => {
    await boardPage.createCard('Todo', originalTitle);
    await expect(boardPage.card('Todo', originalTitle)).toBeVisible();
  });

  await test.step('edit and move the card', async () => {
    await boardPage.openCard('Todo', originalTitle);
    await boardPage.renameOpenCard(updatedTitle);
    await boardPage.moveOpenCardTo('In Progress');
    await boardPage.saveOpenCard();
    await expect(boardPage.card('In Progress', updatedTitle)).toBeVisible();
  });

  await test.step('reload the persisted result', async () => {
    await page.reload();
    await expect(boardPage.card('In Progress', updatedTitle)).toBeVisible();
    await expect(boardPage.card('Todo', originalTitle)).toHaveCount(0);
  });
});
