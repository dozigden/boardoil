import { authenticatePage, expect, getBaseUrl, test } from '../fixtures/boardOilTest';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
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

for (const reconnect of [false, true]) {
  test(`a deleted board closes in another session ${reconnect ? 'after reconnect' : 'over realtime'}`, async ({
    api, browser, authenticatedPage: owner
  }) => {
    const board = await api.createBoard(reconnect ? 'Smoke missed board deletion' : 'Smoke realtime board deletion');
    const viewerContext = await browser.newContext({ baseURL: getBaseUrl() });

    try {
      const viewer = await viewerContext.newPage();
      await authenticatePage(viewer);
      const connection = await interceptBoardConnection(viewer);
      await new BoardPage(viewer).open(board.id);
      await owner.goto(`/boards/${board.id}/admin/delete`);
      await owner.getByRole('textbox', { name: 'Confirm board name' }).fill(board.name);

      if (reconnect) connection.pauseEvents();
      await owner.getByRole('button', { name: 'Delete board', exact: true }).click();
      await expect(owner).toHaveURL('/');
      if (reconnect) await connection.disconnect();

      await expect(viewer).toHaveURL('/');
      await expect(viewer.getByRole('heading', { name: 'Boards', exact: true })).toBeVisible();
      await expect(viewer.getByRole('button', { name: `Configure board ${board.name}`, exact: true })).toHaveCount(0);

      await viewer.goto(`/boards/${board.id}`);
      await expect(viewer).toHaveURL('/');
    } finally {
      await viewerContext.close();
    }
  });
}
