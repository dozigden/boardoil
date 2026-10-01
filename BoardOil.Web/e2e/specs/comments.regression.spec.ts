import { expect, test } from '../fixtures/boardOilTest';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';

for (const editWhilePosting of [false, true]) {
  test(`posting clears only the submitted draft (edited while posting: ${editWhilePosting})`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard('Comment draft completion');
    const card = await api.createCard(board, 'Todo', 'Comment draft');
    const editor = new CardEditorPage(page);
    await page.goto(`/boards/${board.id}/card/${card.id}`);
    await editor.commentEditor().fill('Submitted comment');
    let release!: () => void;
    const gate = new Promise<void>(resolve => { release = resolve; });
    let posting = false;
    await page.route(`**/api/boards/${board.id}/cards/${card.id}/comments`, async route => {
      if (route.request().method() === 'POST') {
        posting = true;
        await gate;
      }
      await route.continue();
    });
    const add = page.getByRole('region', { name: 'Card comments' }).getByRole('button', { name: 'Add', exact: true });
    try {
      await add.click();
      await expect.poll(() => posting).toBe(true);
      if (editWhilePosting) {
        await editor.commentEditor().fill('Next unfinished comment');
      }
    } finally {
      release();
    }
    await expect(editor.commentContent()).toHaveText('Submitted comment');
    if (editWhilePosting) {
      await expect(add).toBeEnabled();
      await expect(editor.commentEditor()).toHaveText('Next unfinished comment');
    } else {
      await expect(editor.commentEditor()).toHaveText('');
    }
  });
}

test('a post completing after changing cards preserves even an identical new draft', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Comment editor navigation');
  const first = await api.createCard(board, 'Todo', 'First comment card');
  await api.createCard(board, 'Todo', 'Second comment card');
  const boardPage = new BoardPage(page);
  const editor = new CardEditorPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'First comment card');
  await editor.commentEditor().fill('Same draft text');
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  let posting = false;
  await page.route(`**/api/boards/${board.id}/cards/${first.id}/comments`, async route => {
    if (route.request().method() === 'POST') {
      posting = true;
      await gate;
    }
    await route.continue();
  });
  const add = page.getByRole('region', { name: 'Card comments' }).getByRole('button', { name: 'Add', exact: true });
  const response = page.waitForResponse(result => result.request().method() === 'POST'
    && result.url().endsWith(`/cards/${first.id}/comments`));
  try {
    await add.click();
    await expect.poll(() => posting).toBe(true);
    await page.getByTitle('Cancel editing', { exact: true }).click();
    await page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true })
      .getByRole('button', { name: 'Discard', exact: true }).click();
    await expect(page.getByRole('dialog')).toBeHidden();
    await boardPage.openCard('Todo', 'Second comment card');
    await editor.commentEditor().fill('Same draft text');
  } finally {
    release();
  }
  expect((await response).ok()).toBe(true);
  await expect(editor.commentEditor()).toHaveText('Same draft text');
  await expect(editor.commentContent()).toHaveCount(0);
});

test('comments use realtime updates without reloading and reload after reconnect', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Comment loading lifecycle');
  const card = await api.createCard(board, 'Todo', 'Open comment card');
  const connection = await interceptBoardConnection(page);
  let loads = 0;
  page.on('request', request => {
    if (request.method() === 'GET' && request.url().endsWith(`/cards/${card.id}/comments`)) {
      loads++;
    }
  });
  const boardPage = new BoardPage(page);
  const editor = new CardEditorPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Open comment card');
  await editor.commentEditor().fill('Posted comment');
  await editor.addComment();
  await expect(editor.commentContent()).toHaveText('Posted comment');
  expect(loads).toBe(1);

  await api.createCard(board, 'Todo', 'Unrelated new card');
  await expect(boardPage.card('Todo', 'Unrelated new card')).toBeVisible();
  expect(loads).toBe(1);
  await editor.commentEditor().fill('Keep unfinished draft through reconnect');
  await connection.disconnect();
  await expect.poll(() => loads).toBe(2);
  await expect(editor.commentContent()).toHaveText('Posted comment');
  await expect(editor.commentEditor()).toHaveText('Keep unfinished draft through reconnect');
});
