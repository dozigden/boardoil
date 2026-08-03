import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('edit and reload a card at a representative mobile viewport', async ({ api, authenticatedPage: page }) => {
  await page.setViewportSize({ width: 390, height: 844 });

  const board = await api.createBoard('Regression mobile card');
  const originalTitle = 'Mobile draft card';
  const updatedTitle = 'Mobile ready card';
  await api.createCard(board, 'In Progress', originalTitle);

  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);

  await test.step('reach and edit a card in a later column', async () => {
    await boardPage.bringColumnIntoView('In Progress');
    await expect(boardPage.card('In Progress', originalTitle)).toBeInViewport();
    await boardPage.openCard('In Progress', originalTitle);
    await boardPage.renameOpenCard(updatedTitle);
    await boardPage.saveOpenCard();
  });

  await test.step('reload and reach the persisted card again', async () => {
    await page.reload();
    await boardPage.bringColumnIntoView('In Progress');
    await expect(boardPage.card('In Progress', updatedTitle)).toBeInViewport();
    await expect(boardPage.card('In Progress', originalTitle)).toHaveCount(0);
  });
});
