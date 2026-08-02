import { authenticatePage, expect, getBaseUrl, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('a card edit appears in a second browser context', async ({ api, browser, page }) => {
  const board = await api.createBoard('Smoke realtime');
  const originalTitle = 'Realtime original';
  const updatedTitle = 'Realtime updated';
  await api.createCard(board, 'Todo', originalTitle);

  const secondContext = await browser.newContext({ baseURL: getBaseUrl() });
  const secondPage = await secondContext.newPage();

  try {
    await authenticatePage(page);
    await authenticatePage(secondPage);

    const firstBoard = new BoardPage(page);
    const secondBoard = new BoardPage(secondPage);
    await firstBoard.open(board.id);
    await secondBoard.open(board.id);
    await expect(secondBoard.card('Todo', originalTitle)).toBeVisible();

    await firstBoard.openCard('Todo', originalTitle);
    await firstBoard.renameOpenCard(updatedTitle);
    await firstBoard.saveOpenCard();

    await expect(secondBoard.card('Todo', updatedTitle)).toBeVisible();
    await expect(secondBoard.card('Todo', originalTitle)).toHaveCount(0);
  } finally {
    await secondContext.close();
  }
});
