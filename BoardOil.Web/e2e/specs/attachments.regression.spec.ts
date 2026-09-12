import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { AttachmentPanel } from '../ui/AttachmentPanel';
import { ArchivedCardsPage } from '../ui/ArchivedCardsPage';

const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');

test('cancelling attachment selection leaves the card dialog open', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression attachment picker cancellation');
  await api.createCard(board, 'Todo', 'Card');
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Card');

  await panel.cancelFileSelection();

  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(panel.region().getByText('No attachments.', { exact: true })).toBeVisible();
  await panel.upload('after-cancel.txt', Buffer.from('Still editing'));
  await panel.downloadLink('after-cancel.txt').focus();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog')).toBeHidden();
});

test('attachments persist independently of description save and download unchanged', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression attachments');
  await api.createCard(board, 'Todo', 'Original card');
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  const bytes = Buffer.from([0, 255, 13, 10, 42]);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Original card');
  await panel.upload('unchanged.bin', bytes);

  const downloadLink = panel.downloadLink('unchanged.bin');
  await expect(downloadLink).toHaveAttribute('download', 'unchanged.bin');
  await expect(downloadLink).toHaveAttribute('href', /\/api\/boards\/\d+\/attachments\/\d+\/download$/);
  const directDownload = await page.request.get((await downloadLink.getAttribute('href'))!);
  expect(directDownload.ok()).toBe(true);
  expect(directDownload.headers()['content-disposition']).toContain('attachment;');
  expect(await directDownload.body()).toEqual(bytes);

  await page.reload();
  expect(await panel.download('unchanged.bin')).toEqual({ name: 'unchanged.bin', bytes });

  await page.getByRole('button', { name: 'Card actions' }).click();
  await page.getByRole('menu', { name: 'Card actions' }).getByRole('button', { name: 'Duplicate', exact: true }).click();
  await expect(panel.region().getByText('Attachments will be copied when you create the duplicate.')).toBeVisible();
  await expect(panel.region().getByRole('button', { name: 'Upload', exact: true })).toHaveCount(0);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Original card', exact: true }).click();
  await dialog.getByRole('textbox', { name: 'Card title' }).fill('Independent copy');
  await dialog.getByRole('textbox', { name: 'Card title' }).press('Enter');
  await dialog.getByRole('button', { name: 'Create duplicate card' }).click();
  await expect(dialog).toBeHidden();

  await boardPage.openCard('Todo', 'Original card');
  await panel.delete('unchanged.bin');
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Independent copy');
  expect(await panel.download('unchanged.bin')).toEqual({ name: 'unchanged.bin', bytes });
});

test('archived attachments remain downloadable and become editable after restore', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression archived attachments');
  await api.createCard(board, 'Todo', 'Archive with file');
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  const archive = new ArchivedCardsPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Archive with file');
  await panel.upload('empty.txt', Buffer.alloc(0));
  await boardPage.open(board.id);
  await boardPage.enterCardSelectionMode();
  await boardPage.selectCard('Todo', 'Archive with file');
  await boardPage.archiveSelectedCards(1);
  await boardPage.openArchivedCards();
  await archive.openCard('Archive with file');
  expect(await panel.download('empty.txt')).toEqual({ name: 'empty.txt', bytes: Buffer.alloc(0) });
  await expect(panel.region().getByRole('button', { name: 'Upload', exact: true })).toHaveCount(0);
  await archive.unarchiveOpenCard('Archive with file');
  await archive.goBackToBoard();
  await boardPage.openCard('Todo', 'Archive with file');
  await panel.delete('empty.txt');
});

test('attachment additions and deletions update another open card through realtime', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression realtime attachments');
  await api.createCard(board, 'Todo', 'Shared card');
  const otherPage = await page.context().newPage();
  try {
    const firstBoard = new BoardPage(page);
    const otherBoard = new BoardPage(otherPage);
    await firstBoard.open(board.id);
    await firstBoard.openCard('Todo', 'Shared card');
    await otherBoard.open(board.id);
    await otherBoard.openCard('Todo', 'Shared card');
    const firstPanel = new AttachmentPanel(page);
    const otherPanel = new AttachmentPanel(otherPage);
    await expect(otherPanel.region().getByText('No attachments.', { exact: true })).toBeVisible();

    await firstPanel.upload('shared.bin', Buffer.from([1, 2, 3]));
    await expect(otherPanel.downloadLink('shared.bin')).toBeVisible();
    await firstPanel.delete('shared.bin');
    await expect(otherPanel.downloadLink('shared.bin')).toBeHidden();
  } finally { await otherPage.close(); }
});

test('a lost upload response reconciles the saved file without offering a conflicting retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression lost attachment response');
  await api.createCard(board, 'Todo', 'Card');
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  const bytes = Buffer.from([1, 2, 3]);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Card');
  await page.route('**/cards/*/attachments', async route => {
    if (route.request().method() !== 'POST') { await route.continue(); return; }
    const response = await route.fetch();
    expect(response.ok()).toBe(true);
    await route.abort('failed');
  });

  await panel.upload('saved.bin', bytes);

  const warning = page.getByRole('dialog', { name: 'Attachment warning', exact: true });
  await expect(warning).toBeVisible();
  await expect(warning.getByText('saved.bin: The upload could not be confirmed. Check the attachment list before uploading again.')).toBeVisible();
  await expect(panel.region().getByRole('button', { name: 'Retry', exact: true })).toHaveCount(0);
  await warning.getByRole('button', { name: 'OK', exact: true }).click();
  await expect(warning).toBeHidden();
  await expect(panel.region().getByRole('button', { name: 'Dismiss', exact: true })).toHaveCount(0);
  expect(await panel.download('saved.bin')).toEqual({ name: 'saved.bin', bytes });
});

test('a browser image upload includes and displays its thumbnail', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression uploaded thumbnail');
  await api.createCard(board, 'Todo', 'Card');
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Card');

  await panel.upload('new-image.png', png, 'image/png');

  await expect(panel.thumbnail('new-image.png')).toBeVisible();
});

test('an image uploaded without a thumbnail is backfilled only once', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression thumbnail backfill');
  const card = await api.createCard(board, 'Todo', 'Card');
  const attachment = await api.uploadAttachment(board.id, card.id, 'backfill.png', 'image/png', png);
  expect(attachment.hasThumbnail).toBe(false);
  let putCount = 0;
  await page.route('**/attachments/*/thumbnail', async route => {
    if (route.request().method() === 'PUT') { putCount++; }
    await route.continue();
  });
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Card');

  await expect(panel.thumbnail('backfill.png')).toBeVisible();
  expect(putCount).toBe(1);
  await page.getByRole('dialog').getByTitle('Cancel', { exact: true }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await boardPage.openCard('Todo', 'Card');
  await expect(panel.thumbnail('backfill.png')).toBeVisible();
  expect(putCount).toBe(1);
});

test('a non-image attachment displays the default icon without requesting a thumbnail', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression non-image thumbnail');
  await api.createCard(board, 'Todo', 'Card');
  let thumbnailRequests = 0;
  await page.route('**/attachments/*/thumbnail', async route => {
    thumbnailRequests++;
    await route.continue();
  });
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Card');

  await panel.upload('notes.txt', Buffer.from('No preview'));

  await expect(panel.thumbnail('notes.txt')).toHaveCount(0);
  await expect(panel.defaultIcon('notes.txt')).toBeVisible();
  expect(thumbnailRequests).toBe(0);
});
