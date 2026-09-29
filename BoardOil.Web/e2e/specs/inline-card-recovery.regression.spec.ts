import { expect, test } from '../fixtures/boardOilTest';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { BoardPage } from '../ui/BoardPage';

test('an unfinished card title keeps its focus and caret through a failed reconnect refresh', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression inline draft recovery');
  const connection = await interceptBoardConnection(page);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const column = boardPage.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  const title = column.getByPlaceholder('Title');
  const draftTitle = 'Keep this unfinished title';
  await title.fill(draftTitle);
  await title.press('Home');
  await title.press('ArrowRight');
  const originalInput = await title.elementHandle();

  let refreshRequests = 0;
  let releaseRecovery!: () => void;
  const recoveryGate = new Promise<void>(resolve => { releaseRecovery = resolve; });
  await page.route(`**/api/boards/${board.id}`, async route => {
    refreshRequests++;
    if (refreshRequests === 1) {
      await route.fulfill({ status: 503, json: { success: false, message: 'Temporarily unavailable.' } });
      return;
    }
    await recoveryGate;
    await route.continue();
  });

  try {
    await connection.disconnect();
    await expect.poll(() => refreshRequests).toBeGreaterThan(0);
    await expect(title).toHaveValue(draftTitle);
    await expect(page.getByRole('status')).toContainText('Board updates are delayed. Retrying…');
    await expect(title).toBeFocused();
    expect(await title.evaluate(element => (element as HTMLTextAreaElement).selectionStart)).toBe(1);
    await expect.poll(() => refreshRequests).toBeGreaterThan(1);
  } finally {
    releaseRecovery();
  }

  await expect(page.getByRole('status')).toContainText('Realtime updates restored.');
  expect(await originalInput!.evaluate(element => element.isConnected)).toBe(true);
  await expect(title).toHaveValue(draftTitle);
  await expect(title).toBeFocused();
  expect(await title.evaluate(element => (element as HTMLTextAreaElement).selectionStart)).toBe(1);
  await title.press('End');
  await title.press('Enter');
  await expect(boardPage.card('Todo', draftTitle)).toBeVisible();
});
