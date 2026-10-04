import { expect, test } from '../fixtures/boardOilTest';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';
import { AttachmentPanel } from '../ui/AttachmentPanel';

test('comments can be edited, cancelled and saved without changing posting order', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Comment editing');
  const card = await api.createCard(board, 'Todo', 'Edit comments');
  await api.addCardComment(board.id, card.id, 'Original comment');
  await api.addCardComment(board.id, card.id, 'Newer comment');
  const editor = new CardEditorPage(page);
  await page.goto(`/boards/${board.id}/card/${card.id}`);

  await editor.editComment('Original comment');
  await editor.editedCommentEditor().fill('Cancelled draft');
  await editor.cancelCommentEdit();
  await expect(editor.commentContent()).toHaveText(['Newer comment', 'Original comment']);

  await editor.editComment('Original comment');
  await editor.saveCommentEdit();
  await expect(editor.editedCommentEditor()).toBeHidden();
  await expect(page.getByText('edited ·', { exact: true })).toHaveCount(0);

  await editor.editComment('Original comment');
  await editor.editedCommentEditor().fill('Corrected comment');
  await editor.formatAllText(editor.editedCommentEditor(), 'Bold');
  await page.screenshot({ path: testInfo.outputPath('comment-editing.png') });
  await editor.saveCommentEdit();
  await expect(editor.editedCommentEditor()).toBeHidden();
  await expect(editor.commentContent()).toHaveText(['Newer comment', 'Corrected comment']);
  await expect(editor.commentContent().filter({ hasText: 'Corrected comment' }).locator('strong')).toHaveText('Corrected comment');
  await expect(page.getByText('edited ·', { exact: true })).toBeVisible();
  await page.reload();
  await expect(editor.commentContent()).toHaveText(['Newer comment', 'Corrected comment']);
  await expect(page.getByText('edited ·', { exact: true })).toBeVisible();
});

test('remote edits and reconnect preserve a local draft and the last save wins', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Comment editing realtime');
  const card = await api.createCard(board, 'Todo', 'Realtime comment');
  const comment = await api.addCardComment(board.id, card.id, 'Original comment');
  const connection = await interceptBoardConnection(page);
  let loads = 0;
  page.on('request', request => {
    if (request.method() === 'GET' && request.url().endsWith(`/cards/${card.id}/comments`)) { loads++; }
  });
  const editor = new CardEditorPage(page);
  await page.goto(`/boards/${board.id}/card/${card.id}`);
  await editor.editComment('Original comment');
  await editor.editedCommentEditor().fill('Local draft saved last');

  await api.updateCardComment(board.id, card.id, comment.id, 'Remote change');
  await expect(page.getByText('edited ·', { exact: true })).toBeVisible();
  expect(loads).toBe(1);
  await expect(editor.editedCommentEditor()).toHaveText('Local draft saved last');
  await connection.disconnect();
  await expect.poll(() => loads).toBe(2);
  await expect(editor.editedCommentEditor()).toHaveText('Local draft saved last');
  await editor.saveCommentEdit();
  await expect(editor.editedCommentEditor()).toBeHidden();
  await expect(editor.commentContent()).toHaveText('Local draft saved last');
});

test('failed comment saves preserve the draft and navigation asks before discarding it', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Failed comment edit');
  const card = await api.createCard(board, 'Todo', 'Edit failure');
  const comment = await api.addCardComment(board.id, card.id, 'Original comment');
  const editor = new CardEditorPage(page);
  await page.goto(`/boards/${board.id}/card/${card.id}`);
  await editor.editComment('Original comment');
  await editor.editedCommentEditor().fill('Keep this draft');
  await page.route(`**/api/boards/${board.id}/cards/${card.id}/comments/${comment.id}`, route => route.fulfill({ status: 500, json: { success: false, statusCode: 500, message: 'Comment edit failed.' } }));

  await editor.saveCommentEdit();

  await expect(page.getByText('Comment edit failed.', { exact: true })).toBeVisible();
  await expect(editor.editedCommentEditor()).toHaveText('Keep this draft');
  await page.getByTitle('Cancel editing', { exact: true }).click();
  const prompt = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
  await expect(prompt).toBeVisible();
  await prompt.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(editor.editedCommentEditor()).toHaveText('Keep this draft');
  await editor.cancelCommentEdit();
  await expect(editor.commentContent()).toHaveText('Original comment');
});

test('typing during a comment save preserves the next draft and the keyboard shortcut saves only the comment', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Comment save completion');
  const card = await api.createCard(board, 'Todo', 'Pending edit');
  const comment = await api.addCardComment(board.id, card.id, 'Original comment');
  const editor = new CardEditorPage(page);
  await page.goto(`/boards/${board.id}/card/${card.id}`);
  await editor.editComment('Original comment');
  await editor.editedCommentEditor().fill('Submitted edit');
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  let saving = false;
  await page.route(`**/api/boards/${board.id}/cards/${card.id}/comments/${comment.id}`, async route => {
    saving = true;
    await gate;
    await route.continue();
  });
  try {
    await editor.saveCommentEdit();
    await expect.poll(() => saving).toBe(true);
    await editor.editedCommentEditor().fill('Next unfinished edit');
  } finally {
    release();
  }
  await expect(page.getByText('edited ·', { exact: true })).toBeVisible();
  await expect(editor.editedCommentEditor()).toHaveText('Next unfinished edit');
  await editor.editedCommentEditor().press('Control+s');
  await expect(editor.editedCommentEditor()).toBeHidden();
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(editor.commentContent()).toHaveText('Next unfinished edit');
});

test('removing an image while editing a comment preserves the card attachment', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Comment attachment references');
  const card = await api.createCard(board, 'Todo', 'Comment image');
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
  const attachment = await api.uploadAttachment(board.id, card.id, 'comment-image.png', 'image/png', png);
  await api.addCardComment(board.id, card.id, `Image comment\n\n![shared image](boardoil-attachment:${attachment.originalFileName})`);
  const editor = new CardEditorPage(page);
  const panel = new AttachmentPanel(page);
  await page.goto(`/boards/${board.id}/card/${card.id}`);
  await editor.editComment('Image comment');
  await editor.editedCommentEditor().getByRole('button', { name: 'Enlarge image: shared image' }).hover();
  await editor.editedCommentEditor().getByRole('button', { name: 'Remove image: shared image' }).click();
  await page.getByRole('dialog', { name: 'Remove image', exact: true }).getByRole('button', { name: 'Remove', exact: true }).click();
  await editor.saveCommentEdit();

  await expect(editor.commentContent()).toHaveText('Image comment');
  await expect(editor.commentContent().getByRole('img')).toHaveCount(0);
  expect(await panel.download(attachment.originalFileName)).toEqual({ name: attachment.originalFileName, bytes: png });
});

for (const changed of [false, true]) {
  test(`image alternative-text-only comment edits preserve drafts and persist correctly (changed: ${changed})`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard('Comment image alternative text');
    const card = await api.createCard(board, 'Todo', 'Alternative text comment');
    const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
    const attachment = await api.uploadAttachment(board.id, card.id, 'comment-image.png', 'image/png', png);
    await api.addCardComment(board.id, card.id, `Image comment\n\n![Original alt](boardoil-attachment:${attachment.originalFileName})`);
    const editor = new CardEditorPage(page);
    const expectedAlt = changed ? 'Corrected alt' : 'Original alt';
    await page.goto(`/boards/${board.id}/card/${card.id}`);
    await editor.editComment('Image comment');
    await editor.editCommentImageAlternativeText('Original alt', expectedAlt);

    if (changed) {
      await page.getByTitle('Cancel editing', { exact: true }).click();
      const prompt = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
      await expect(prompt).toBeVisible();
      await prompt.getByRole('button', { name: 'Cancel', exact: true }).click();
      await expect(editor.editedCommentEditor().getByRole('button', { name: `Enlarge image: ${expectedAlt}`, exact: true })).toBeVisible();
    }

    await editor.saveCommentEdit();
    await expect(editor.editedCommentEditor()).toBeHidden();
    await expect(editor.commentContent().getByRole('button', { name: `Enlarge image: ${expectedAlt}`, exact: true })).toBeVisible();
    await expect(page.getByText('edited ·', { exact: true })).toHaveCount(changed ? 1 : 0);
    await page.reload();
    await expect(editor.commentContent().getByRole('button', { name: `Enlarge image: ${expectedAlt}`, exact: true })).toBeVisible();
    await expect(page.getByText('edited ·', { exact: true })).toHaveCount(changed ? 1 : 0);
  });
}

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
