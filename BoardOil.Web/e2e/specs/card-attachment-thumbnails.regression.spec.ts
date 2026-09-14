import { expect, test } from '../fixtures/boardOilTest';
import { AttachmentPanel } from '../ui/AttachmentPanel';
import { BoardPage } from '../ui/BoardPage';

const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');

test('the first image stays on the card until deletion selects the next image', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression card thumbnail lifecycle');
  await api.createCard(board, 'Todo', 'Image card');
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Image card');

  await panel.upload('first.png', png, 'image/png');
  const firstAttachmentId = await panel.attachmentId('first.png');
  const thumbnail = boardPage.cardThumbnail('Todo', 'Image card');
  await expect(thumbnail).toBeVisible();
  await expect(thumbnail).toHaveAttribute('data-attachment-id', String(firstAttachmentId));

  await panel.upload('second.png', png, 'image/png');
  const secondAttachmentId = await panel.attachmentId('second.png');
  await expect(thumbnail).toHaveAttribute('data-attachment-id', String(firstAttachmentId));

  await panel.delete('first.png');
  await expect(thumbnail).toHaveAttribute('data-attachment-id', String(secondAttachmentId));
  await expect(thumbnail).toBeVisible();

  await page.getByRole('dialog').getByTitle('Cancel', { exact: true }).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(boardPage.cardThumbnail('Todo', 'Image card')).toBeVisible();
});

test('a thumbnail card remains selectable and draggable between columns', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression thumbnail card interactions');
  const card = await api.createCard(board, 'Todo', 'Movable image card');
  await api.createCard(board, 'In Progress', 'Drop anchor');
  await api.uploadAttachment(board.id, card.id, 'cover.png', 'image/png', png);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await expect(boardPage.cardThumbnail('Todo', 'Movable image card')).toBeVisible();

  await boardPage.enterCardSelectionMode();
  await boardPage.selectCard('Todo', 'Movable image card');
  const selectedCard = boardPage.selectionModeCard('Todo', 'Movable image card');
  await expect(selectedCard.getByTestId('card-attachment-thumbnail')).toBeVisible();
  await expect(selectedCard).toHaveAttribute('draggable', 'true');

  await selectedCard.dragTo(boardPage.selectionModeCard('In Progress', 'Drop anchor'));

  await expect(boardPage.card('In Progress', 'Movable image card')).toBeVisible();
  await expect(boardPage.cardThumbnail('In Progress', 'Movable image card')).toBeVisible();
});

test('a disabled board does not request or render card thumbnails', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression disabled card thumbnails');
  const card = await api.createCard(board, 'Todo', 'Image card');
  await api.uploadAttachment(board.id, card.id, 'cover.png', 'image/png', png);
  await api.setCardAttachmentThumbnailsEnabled(board, false);
  const thumbnailRequests: string[] = [];
  page.on('request', request => {
    const path = new URL(request.url()).pathname;
    if (path.includes('/cards/thumbnails') || path.includes('/attachments/image-content') || path.endsWith('/thumbnail')) {
      thumbnailRequests.push(path);
    }
  });
  const boardPage = new BoardPage(page);

  await boardPage.open(board.id);

  await expect(boardPage.card('Todo', 'Image card')).toBeVisible();
  await expect(boardPage.cardThumbnail('Todo', 'Image card')).toHaveCount(0);
  expect(thumbnailRequests).toEqual([]);
});
