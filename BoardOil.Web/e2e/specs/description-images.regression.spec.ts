import { expect, test } from '../fixtures/boardOilTest';
import type { Locator, Page } from '@playwright/test';
import { AttachmentPanel } from '../ui/AttachmentPanel';
import { ArchivedCardsPage } from '../ui/ArchivedCardsPage';
import { BoardPage } from '../ui/BoardPage';

const onePixelPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64'
);
const onePixelGif = Buffer.from('R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==', 'base64');
const encodedImageFileName = 'Architecture (final) #1.png';
const encodedImageAlt = 'Architecture (final) #1';

type BrowserImageFile = {
  name: string;
  mimeType: string;
  buffer: Buffer;
};

test('an existing attachment image renders when dragged into a newly opened description', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression existing attachment drag');
  const card = await api.createCard(board, 'Todo', 'Existing attachment card');
  await api.uploadAttachment(board.id, card.id, 'existing.png', 'image/png', onePixelPng);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Existing attachment card');
  const dialog = page.getByRole('dialog');
  let uploadRequests = 0;
  page.on('request', request => {
    if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/attachments')) {
      uploadRequests++;
    }
  });

  await dialog.getByRole('img', { name: 'Preview of existing.png' })
    .dragTo(dialog.getByRole('group', { name: 'Card description editor' }));

  await expect(dialog.getByLabel('Card description', { exact: true })
    .getByRole('img', { name: 'existing' })).toBeVisible();
  await expect(dialog.locator('.md-image-node-placeholder[aria-label="existing"]')).toBeHidden();
  expect(uploadRequests).toBe(0);

  await dialog.getByTitle('Cancel editing', { exact: true }).click();
  const discardDialog = page.getByRole('dialog').filter({
    has: page.getByRole('heading', { name: 'Discard unsaved changes' })
  });
  await expect(discardDialog).toBeVisible();
  await discardDialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(discardDialog).toBeHidden();
});

test('a newly uploaded attachment image renders when dragged in the same dialog', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression same-dialog attachment drag');
  await api.createCard(board, 'Todo', 'New attachment card');
  const boardPage = new BoardPage(page);
  const panel = new AttachmentPanel(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'New attachment card');
  const dialog = page.getByRole('dialog');
  await panel.upload('new.png', onePixelPng, 'image/png');

  await panel.thumbnail('new.png')
    .dragTo(dialog.getByRole('group', { name: 'Card description editor' }));

  await expect(dialog.getByLabel('Card description', { exact: true })
    .getByRole('img', { name: 'new' })).toBeVisible();
  await expect(dialog.locator('.md-image-node-placeholder[aria-label="new"]')).toBeHidden();
});

test('a picked card image persists as a protected owner-relative Markdown reference', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression description image');
  await api.createCard(board, 'Todo', 'Image card');
  let uploadRequests = 0;
  page.on('request', request => {
    if (request.method() === 'POST' && new URL(request.url()).pathname.endsWith('/attachments')) {
      uploadRequests++;
    }
  });
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const attachmentListPattern = `**/api/boards/${board.id}/cards/*/attachments`;
  let releaseAttachmentList!: () => void;
  const attachmentListGate = new Promise<void>(resolve => {
    releaseAttachmentList = resolve;
  });
  await page.route(attachmentListPattern, async route => {
    if (route.request().method() === 'GET') {
      await attachmentListGate;
    }
    await route.continue();
  });
  await boardPage.openCard('Todo', 'Image card');
  const dialog = page.getByRole('dialog');

  const addImageButton = dialog.getByRole('toolbar', { name: 'Markdown formatting' })
    .getByRole('button', { name: 'Add image', exact: true });
  await expect(addImageButton).toBeVisible();
  await expect(addImageButton).toBeDisabled();
  releaseAttachmentList();
  await expect(addImageButton).toBeEnabled();
  await page.unroute(attachmentListPattern);
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: encodedImageFileName,
    mimeType: 'text/html',
    buffer: onePixelPng
  });

  await expect(dialog.getByText(`${encodedImageFileName} inserted.`, { exact: true })).toBeVisible();
  const richEditor = dialog.getByLabel('Card description', { exact: true });
  const image = richEditor.locator(`img[alt="${encodedImageAlt}"]`);
  await expect(image).toBeVisible();
  const imageUrl = await image.getAttribute('src');
  expect(imageUrl).toContain(`/api/boards/${board.id}/cards/`);
  expect(imageUrl).toContain('/attachments/image-content?fileName=Architecture%20(final)%20%231.png');

  await dialog.getByRole('button', { name: 'Switch to markdown text editor' }).click();
  const markdown = await dialog.getByRole('textbox', { name: 'Card description markdown' }).inputValue();
  expect(markdown.trim()).toBe(
    '![Architecture (final) #1](boardoil-attachment:Architecture%20%28final%29%20%231.png)');
  await dialog.getByRole('button', { name: 'Switch to rich editor' }).click();
  await dialog.getByRole('button', { name: 'Save card' }).click();
  await expect(dialog).toBeHidden();

  await boardPage.openCard('Todo', 'Image card');
  const reopenedDescription = dialog.getByLabel('Card description', { exact: true });
  await expect(reopenedDescription.locator(`img[alt="${encodedImageAlt}"]`)).toBeVisible();
  const attachmentLink = dialog.getByRole('link', { name: `Download ${encodedImageFileName}` });
  await expect(attachmentLink).toBeVisible();
  await attachmentLink.dragTo(dialog.getByRole('group', { name: 'Card description editor' }));
  await expect(reopenedDescription.locator(`img[alt="${encodedImageAlt}"]`)).toHaveCount(2);
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: encodedImageFileName.toUpperCase(),
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  await expect(reopenedDescription.locator(`img[alt="${encodedImageAlt}"]`)).toHaveCount(3);
  expect(uploadRequests).toBe(1);

  await dialog.getByRole('button', { name: 'Save card' }).click();
  await expect(dialog).toBeHidden();
  await boardPage.openCard('Todo', 'Image card');
  await expect(dialog.getByLabel('Card description', { exact: true })
    .locator(`img[alt="${encodedImageAlt}"]`)).toHaveCount(3);
  const response = await page.request.get(imageUrl!);
  expect(response.ok()).toBe(true);
  expect(response.headers()['content-type']).toContain('image/png');
  expect(response.headers()['cache-control']).toBe('private, no-store');
  expect(response.headers()['x-content-type-options']).toBe('nosniff');
  expect(response.headers()['content-disposition']).toBeUndefined();
  expect(await response.body()).toEqual(onePixelPng);
});

test('all supported picked image formats decode and use canonical inline types', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression supported image formats');
  await api.createCard(board, 'Todo', 'Format card');
  const generatedImages = await createBrowserImageBuffers(page);
  const images = [
    { name: 'picked-png.png', contentType: 'image/png', buffer: generatedImages.png },
    { name: 'picked-jpeg.jpg', contentType: 'image/jpeg', buffer: generatedImages.jpeg },
    { name: 'picked-webp.webp', contentType: 'image/webp', buffer: generatedImages.webp },
    { name: 'picked-gif.gif', contentType: 'image/gif', buffer: onePixelGif }
  ];
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Format card');
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('button', { name: 'Add image', exact: true })).toBeEnabled();

  for (const candidate of images) {
    await dialog.getByLabel('Choose an image for Card description').setInputFiles({
      name: candidate.name,
      mimeType: 'application/octet-stream',
      buffer: candidate.buffer
    });
    const alt = candidate.name.slice(0, candidate.name.lastIndexOf('.'));
    const image = dialog.getByLabel('Card description', { exact: true }).locator(`img[alt="${alt}"]`);
    await expect(image).toBeVisible();
    const response = await page.request.get((await image.getAttribute('src'))!);
    expect(response.ok()).toBe(true);
    expect(response.headers()['content-type']).toContain(candidate.contentType);
    expect(await response.body()).toEqual(candidate.buffer);
  }
});

test('a disguised non-image remains downloadable but cannot render inline', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression disguised image');
  await api.createCard(board, 'Todo', 'Disguised image card');
  const disguisedBytes = Buffer.from('<script>document.body.textContent = "unsafe";</script>');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Disguised image card');
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('button', { name: 'Add image', exact: true })).toBeEnabled();

  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'malicious.png',
    mimeType: 'image/png',
    buffer: disguisedBytes
  });

  await expect(dialog.locator('.md-image-node-placeholder[aria-label="malicious"]')).toHaveText('Image unavailable');
  const inlineImage = dialog.locator('img[alt="malicious"]');
  const inlineResponse = await page.request.get((await inlineImage.getAttribute('src'))!);
  expect(inlineResponse.status()).toBe(415);
  expect(inlineResponse.headers()['cache-control']).toBe('private, no-store');
  expect(inlineResponse.headers()['x-content-type-options']).toBe('nosniff');
  const downloadLink = dialog.getByRole('link', { name: 'Download malicious.png' });
  const downloadResponse = await page.request.get((await downloadLink.getAttribute('href'))!);
  expect(downloadResponse.ok()).toBe(true);
  expect(downloadResponse.headers()['content-type']).toContain('application/octet-stream');
  expect(await downloadResponse.body()).toEqual(disguisedBytes);
  await expect(dialog.locator('script')).toHaveCount(0);
});

test('external and missing card images use safe rendering and fallbacks', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression safe image placeholders');
  const description = [
    '![Remote](https://example.test/remote.png)',
    '',
    '![Broken](https://example.test/broken.png)',
    '',
    '![Missing](boardoil-attachment:missing.png)'
  ].join('\n');
  await api.createCard(board, 'Todo', 'Placeholder card', description);
  let externalRequests = 0;
  await page.route('https://example.test/**', async route => {
    externalRequests++;
    if (route.request().url().endsWith('/remote.png')) {
      await route.fulfill({ status: 200, contentType: 'image/png', body: onePixelPng });
      return;
    }
    await route.abort();
  });
  const boardPage = new BoardPage(page);

  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Placeholder card');

  const dialog = page.getByRole('dialog');
  const remoteImage = dialog.locator('img[alt="Remote"]');
  await expect(remoteImage).toBeVisible();
  await expect(remoteImage).toHaveAttribute('loading', 'lazy');
  await expect(remoteImage).toHaveAttribute('referrerpolicy', 'no-referrer');
  await expect(dialog.locator('.md-image-node-placeholder').filter({ hasText: 'Broken: External image unavailable' }))
    .toBeVisible();
  await expect(dialog.getByRole('link', { name: 'Open external image', exact: true }))
    .toHaveAttribute('href', 'https://example.test/broken.png');
  await expect(dialog.locator('.md-image-node-placeholder[aria-label="Missing"]')).toHaveText('Image unavailable');
  await expect.poll(() => externalRequests).toBe(2);
});

test('an image dragged from another browser page inserts without navigating', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression browser image drag');
  await api.createCard(board, 'Todo', 'Browser drag card');
  const boardPage = new BoardPage(page);
  await page.route('https://example.test/dragged.png', route =>
    route.fulfill({ status: 200, contentType: 'image/png', body: onePixelPng }));
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Browser drag card');

  const dialog = page.getByRole('dialog');
  const dropArea = dialog.getByRole('group', { name: 'Card description editor' });
  await dispatchBrowserImageDrop(dropArea, 'https://example.test/dragged.png', 'Dragged browser image');

  await expect(page).toHaveURL(new RegExp(`/boards/${board.id}/card/\\d+$`));
  const image = dialog.getByLabel('Card description', { exact: true })
    .locator('img[alt="Dragged browser image"]');
  await expect(image).toBeVisible();
  await expect(image).toHaveAttribute('loading', 'lazy');
  await expect(image).toHaveAttribute('referrerpolicy', 'no-referrer');
  await dialog.getByRole('button', { name: 'Switch to markdown text editor' }).click();
  const markdown = await dialog.getByRole('textbox', { name: 'Card description markdown' }).inputValue();
  expect(markdown.trim()).toBe('![Dragged browser image](https://example.test/dragged.png)');
});

test('markdown mode cannot persist an embedded data image', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression embedded image validation');
  const card = await api.createCard(board, 'Todo', 'Validated card');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Validated card');
  const dialog = page.getByRole('dialog');

  await dialog.getByRole('button', { name: 'Switch to markdown text editor' }).click();
  await dialog.getByRole('textbox', { name: 'Card description markdown' })
    .fill('![Embedded](data:image/png;base64,AAAA)');
  const responsePromise = page.waitForResponse(response =>
    response.request().method() === 'PUT'
    && new URL(response.url()).pathname === `/api/boards/${board.id}/cards/${card.id}`);
  await dialog.getByRole('button', { name: 'Save card' }).click();
  const response = await responsePromise;

  expect(response.status()).toBe(400);
  const payload = await response.json() as { validationErrors: Record<string, string[]> };
  expect(payload.validationErrors.description).toContain(
    'Card descriptions cannot contain data or blob image URLs. Upload the image as a card attachment instead.');
  await expect(dialog).toBeVisible();
});

test('pasted and dropped images keep their editor position and file order', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression image paste and drop');
  await api.createCard(board, 'Todo', 'Paste and drop card', 'Before\n\nAfter');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Paste and drop card');

  const dialog = page.getByRole('dialog');
  const richEditor = dialog.getByLabel('Card description', { exact: true });
  await expect(dialog.getByRole('button', { name: 'Add image', exact: true })).toBeEnabled();
  let releaseFirstUpload!: () => void;
  const firstUploadGate = new Promise<void>(resolve => {
    releaseFirstUpload = resolve;
  });
  let uploadCount = 0;
  await page.route('**/cards/*/attachments', async route => {
    if (route.request().method() === 'POST' && ++uploadCount === 1) {
      await firstUploadGate;
    }
    await route.continue();
  });

  await richEditor.locator('p').first().click();
  await page.keyboard.press('End');
  await dispatchImageEvent(richEditor, 'paste', [
    { name: 'image.png', mimeType: 'image/png', buffer: onePixelPng },
    { name: 'image.png', mimeType: 'image/png', buffer: onePixelPng }
  ]);
  await expect(dialog.locator('.md-image-upload')).toHaveCount(2);
  await expect(dialog.locator('.md-image-upload progress')).toHaveCount(2);
  await expect(dialog.getByRole('button', { name: 'Save card' })).toBeDisabled();

  releaseFirstUpload();
  await expect(richEditor.locator('img[alt="image"]')).toBeVisible();
  await expect(richEditor.locator('img[alt="image (2)"]')).toBeVisible();

  const lastParagraph = richEditor.locator('p').last();
  await lastParagraph.click();
  await page.keyboard.press('Home');
  await dispatchImageEvent(richEditor, 'paste', [
    { name: 'image.png', mimeType: 'image/png', buffer: onePixelPng }
  ]);
  await expect(richEditor.locator('img[alt="image (3)"]')).toBeVisible();

  await lastParagraph.click();
  await page.keyboard.press('Home');
  await dispatchImageEvent(dialog.getByRole('group', { name: 'Card description editor' }), 'drop', [
    { name: 'drop-three.png', mimeType: 'image/png', buffer: onePixelPng }
  ], lastParagraph);
  await expect(richEditor.locator('img[alt="drop-three"]')).toBeVisible();
  await page.unroute('**/cards/*/attachments');

  await dialog.getByRole('button', { name: 'Switch to markdown text editor' }).click();
  await expect(dialog.getByRole('textbox', { name: 'Card description markdown' })).toHaveValue([
    'Before',
    '',
    '![image](boardoil-attachment:image.png)',
    '',
    '![image (2)](boardoil-attachment:image%20%282%29.png)',
    '',
    '![image (3)](boardoil-attachment:image%20%283%29.png)',
    '',
    '![drop-three](boardoil-attachment:drop-three.png)',
    '',
    'After'
  ].join('\n'));
});

test('the image picker inserts portable Markdown at the caret in Markdown mode', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression markdown image insertion');
  await api.createCard(board, 'Todo', 'Markdown image card', 'AlphaOmega');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Markdown image card');

  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Switch to markdown text editor' }).click();
  const markdownEditor = dialog.getByRole('textbox', { name: 'Card description markdown' });
  await markdownEditor.evaluate((element: HTMLTextAreaElement) => element.setSelectionRange(5, 5));
  await expect(dialog.getByRole('button', { name: 'Add image', exact: true })).toBeEnabled();
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'plain-picker.png',
    mimeType: 'image/png',
    buffer: onePixelPng
  });

  await expect(markdownEditor).toHaveValue([
    'Alpha',
    '',
    '![plain-picker](boardoil-attachment:plain-picker.png)',
    '',
    'Omega'
  ].join('\n'));
  await expect(dialog.getByRole('button', { name: 'Save card' })).toBeEnabled();
});

test('image alternative text is editable and archived images enlarge with the keyboard', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression image editing');
  await api.createCard(board, 'Todo', 'Accessible image card');
  const boardPage = new BoardPage(page);
  const archivedCardsPage = new ArchivedCardsPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Accessible image card');

  const cardDialog = page.getByRole('dialog');
  await cardDialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'diagram.png',
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  const editableImage = cardDialog.getByRole('button', { name: 'Enlarge image: diagram' });
  await expect(editableImage).toBeVisible();
  await editableImage.click();
  const editImageDialog = page.getByRole('dialog', { name: 'Image preview' });
  await editImageDialog.getByRole('textbox', { name: 'Alternative text' }).fill('Release flow diagram');
  await editImageDialog.getByRole('button', { name: 'Save alternative text' }).click();
  await expect(editImageDialog).toBeHidden();
  await expect(cardDialog.getByRole('button', { name: 'Enlarge image: Release flow diagram' })).toBeVisible();

  await cardDialog.getByRole('button', { name: 'Save card' }).click();
  await expect(cardDialog).toBeHidden();
  await boardPage.enterCardSelectionMode();
  await boardPage.selectCard('Todo', 'Accessible image card');
  await boardPage.archiveSelectedCards(1);
  await boardPage.openArchivedCards();
  await archivedCardsPage.openCard('Accessible image card');

  const archivedImage = page.getByRole('button', { name: 'Enlarge image: Release flow diagram' });
  await archivedImage.focus();
  await page.keyboard.press('Enter');
  const viewImageDialog = page.getByRole('dialog', { name: 'Image preview' });
  const previewImage = viewImageDialog.getByRole('img', { name: 'Release flow diagram' });
  await expect(previewImage).toBeVisible();
  const inlineImageBox = await archivedImage.locator('img').boundingBox();
  const previewImageBox = await previewImage.boundingBox();
  expect(previewImageBox!.width).toBeGreaterThan(inlineImageBox!.width);
  await viewImageDialog.getByRole('button', { name: 'Close', exact: true }).click();
  await expect(viewImageDialog).toBeHidden();

  await archivedCardsPage.unarchiveOpenCard('Accessible image card');
  await archivedCardsPage.goBackToBoard();
  await boardPage.openCard('Todo', 'Accessible image card');
  await expect(page.getByRole('dialog').getByRole('button', {
    name: 'Enlarge image: Release flow diagram'
  })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Download diagram.png' })).toBeVisible();
});

test('a duplicated description image remains independent when the source attachment is deleted', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression duplicated description image');
  await api.createCard(board, 'Todo', 'Image source');
  const boardPage = new BoardPage(page);
  const attachmentPanel = new AttachmentPanel(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Image source');

  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'copied-diagram.png',
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  await dialog.getByRole('button', { name: 'Save card' }).click();
  await expect(dialog).toBeHidden();

  await boardPage.openCard('Todo', 'Image source');
  await dialog.getByRole('button', { name: 'Card actions' }).click();
  await dialog.getByRole('menu', { name: 'Card actions' })
    .getByRole('button', { name: 'Duplicate', exact: true })
    .click();
  await dialog.getByRole('button', { name: 'Image source', exact: true }).click();
  const titleInput = dialog.getByRole('textbox', { name: 'Card title' });
  await titleInput.fill('Image duplicate');
  await titleInput.press('Enter');
  await dialog.getByRole('button', { name: 'Create duplicate card' }).click();
  await expect(dialog).toBeHidden();

  await boardPage.openCard('Todo', 'Image source');
  await attachmentPanel.delete('copied-diagram.png');
  await expect(dialog.locator('.md-image-node-placeholder[aria-label="copied-diagram"]'))
    .toHaveText('Image unavailable');

  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Image duplicate');
  await expect(dialog.getByRole('button', { name: 'Enlarge image: copied-diagram' })).toBeVisible();
  expect(await attachmentPanel.download('copied-diagram.png')).toEqual({
    name: 'copied-diagram.png',
    bytes: onePixelPng
  });
});

test('a description image moves with its card to another board', async ({ api, authenticatedPage: page }) => {
  const sourceBoard = await api.createBoard('Regression image transfer source');
  const destinationBoard = await api.createBoard('Regression image transfer destination');
  await api.createCard(sourceBoard, 'Todo', 'Transferred image card');
  const boardPage = new BoardPage(page);
  await boardPage.open(sourceBoard.id);
  await boardPage.openCard('Todo', 'Transferred image card');

  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'moved-diagram.png',
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  await dialog.getByRole('button', { name: 'Save card' }).click();
  await expect(dialog).toBeHidden();

  await boardPage.openCard('Todo', 'Transferred image card');
  await dialog.getByRole('button', { name: 'Card actions' }).click();
  await dialog.getByRole('menu', { name: 'Card actions' })
    .getByRole('button', { name: 'Move to another board' })
    .click();
  const transferDialog = page.getByRole('dialog').filter({
    has: page.getByRole('heading', { name: 'Move card to another board' })
  });
  await transferDialog.getByLabel('Destination board', { exact: true })
    .selectOption(String(destinationBoard.id));
  await transferDialog.getByLabel('Destination column', { exact: true })
    .selectOption({ label: 'In Progress' });
  await transferDialog.getByRole('button', { name: 'Move card' }).click();

  await expect(page).toHaveURL(new RegExp(`/boards/${destinationBoard.id}/card/\\d+$`));
  const movedImage = dialog.getByRole('button', { name: 'Enlarge image: moved-diagram' }).locator('img');
  await expect(movedImage).toBeVisible();
  await expect(movedImage).toHaveAttribute(
    'src',
    new RegExp(`/api/boards/${destinationBoard.id}/cards/\\d+/attachments/image-content\\?fileName=moved-diagram.png`)
  );
  await expect(dialog.getByRole('link', { name: 'Download moved-diagram.png' })).toBeVisible();

  await dialog.getByTitle('Cancel editing', { exact: true }).click();
  await boardPage.open(sourceBoard.id);
  await expect(boardPage.card('Todo', 'Transferred image card')).toHaveCount(0);
});

test('realtime deletion and same-name re-upload refresh an open description image', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression realtime description image');
  const replacementPng = (await createBrowserImageBuffers(page)).png;
  await api.createCard(board, 'Todo', 'Shared image card');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Shared image card');

  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'shared-diagram.png',
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  await dialog.getByRole('button', { name: 'Save card' }).click();
  await expect(dialog).toBeHidden();
  await boardPage.openCard('Todo', 'Shared image card');
  await expect(dialog.getByRole('button', { name: 'Enlarge image: shared-diagram' })).toBeVisible();

  const otherPage = await page.context().newPage();
  try {
    const otherBoardPage = new BoardPage(otherPage);
    const otherAttachments = new AttachmentPanel(otherPage);
    await otherBoardPage.open(board.id);
    await otherBoardPage.openCard('Todo', 'Shared image card');

    await otherAttachments.delete('shared-diagram.png');
    await expect(dialog.locator('.md-image-node-placeholder[aria-label="shared-diagram"]'))
      .toHaveText('Image unavailable');

    await otherAttachments.upload('shared-diagram.png', replacementPng);
    const replacementImage = dialog.getByRole('button', { name: 'Enlarge image: shared-diagram' }).locator('img');
    await expect(replacementImage).toBeVisible();
    const replacementUrl = await replacementImage.getAttribute('src');
    expect(replacementUrl).toContain('&v=');
    const replacementResponse = await page.request.get(replacementUrl!);
    expect(await replacementResponse.body()).toEqual(replacementPng);
  } finally {
    await otherPage.close();
  }
});

test('failed and cancelled uploads stay explicit and successful insertion remains undoable', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression image upload recovery');
  await api.createCard(board, 'Todo', 'Image recovery card');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Image recovery card');

  const dialog = page.getByRole('dialog');
  const richEditor = dialog.getByLabel('Card description', { exact: true });
  let failNextUpload = true;
  await page.route('**/cards/*/attachments', async route => {
    if (route.request().method() === 'POST' && failNextUpload) {
      failNextUpload = false;
      await route.fulfill({ status: 500, contentType: 'application/problem+json', body: '{"title":"Test upload failure"}' });
      return;
    }
    await route.continue();
  });

  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'retry.png',
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  const failedUpload = dialog.locator('.md-image-upload').filter({ hasText: 'retry.png' });
  await expect(failedUpload).toContainText('Upload failed.');
  await expect(dialog.getByRole('button', { name: 'Save card' })).toBeDisabled();
  const warningDialog = page.getByRole('dialog', { name: 'Attachment warning' });
  if (await warningDialog.isVisible()) {
    await warningDialog.getByRole('button', { name: 'OK' }).click();
  }
  await failedUpload.getByRole('button', { name: 'Retry' }).click();
  await expect(richEditor.locator('img[alt="retry"]')).toBeVisible();
  await page.unroute('**/cards/*/attachments');

  await richEditor.focus();
  await page.keyboard.press('Control+z');
  await expect(richEditor.locator('img[alt="retry"]')).toHaveCount(0);
  await expect(dialog.getByRole('button', { name: 'Save card' })).toBeEnabled();

  let releaseCancelledUpload!: () => void;
  const cancelledUploadGate = new Promise<void>(resolve => {
    releaseCancelledUpload = resolve;
  });
  await page.route('**/cards/*/attachments', async route => {
    if (route.request().method() === 'POST') {
      await cancelledUploadGate;
      await route.continue().catch(() => undefined);
      return;
    }
    await route.continue();
  });
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: 'cancelled.png',
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  const cancelledUpload = dialog.locator('.md-image-upload').filter({ hasText: 'cancelled.png' });
  await expect(cancelledUpload).toBeVisible();
  await cancelledUpload.getByRole('button', { name: 'Cancel' }).click();
  await expect(cancelledUpload).toContainText('Upload cancelled.');
  releaseCancelledUpload();
  await page.unroute('**/cards/*/attachments');
  await cancelledUpload.getByRole('button', { name: 'Dismiss' }).click();
  await expect(cancelledUpload).toHaveCount(0);
  await expect(dialog.getByRole('button', { name: 'Save card' })).toBeEnabled();
});

async function dispatchImageEvent(
  editor: Locator,
  eventType: 'paste' | 'drop',
  files: BrowserImageFile[],
  dropTarget?: Locator
) {
  const dropBox = dropTarget ? await dropTarget.boundingBox() : null;
  await editor.evaluate((element, payload) => {
    const transfer = new DataTransfer();
    for (const file of payload.files) {
      transfer.items.add(new File([new Uint8Array(file.bytes)], file.name, { type: file.mimeType }));
    }
    if (payload.eventType === 'paste') {
      element.dispatchEvent(new ClipboardEvent('paste', {
        bubbles: true,
        cancelable: true,
        clipboardData: transfer
      }));
      return;
    }
    element.dispatchEvent(new DragEvent('drop', {
      bubbles: true,
      cancelable: true,
      dataTransfer: transfer,
      clientX: payload.dropPoint?.x ?? 0,
      clientY: payload.dropPoint?.y ?? 0
    }));
  }, {
    eventType,
    files: files.map(file => ({
      name: file.name,
      mimeType: file.mimeType,
      bytes: [...file.buffer]
    })),
    dropPoint: dropBox ? { x: dropBox.x + 4, y: dropBox.y + dropBox.height / 2 } : null
  });
}

async function dispatchBrowserImageDrop(editor: Locator, url: string, alt: string) {
  await editor.evaluate((element, image) => {
    const transfer = new DataTransfer();
    transfer.setData('text/html', `<img src="${image.url}" alt="${image.alt}">`);
    transfer.setData('text/uri-list', image.url);
    element.dispatchEvent(new DragEvent('drop', {
      bubbles: true,
      cancelable: true,
      dataTransfer: transfer
    }));
  }, { url, alt });
}

async function createBrowserImageBuffers(page: import('@playwright/test').Page) {
  const encoded = await page.evaluate(() => {
    const canvas = document.createElement('canvas');
    canvas.width = 2;
    canvas.height = 2;
    const context = canvas.getContext('2d')!;
    context.fillStyle = '#3584e4';
    context.fillRect(0, 0, 2, 2);
    return {
      png: canvas.toDataURL('image/png'),
      jpeg: canvas.toDataURL('image/jpeg'),
      webp: canvas.toDataURL('image/webp')
    };
  });

  return {
    png: decodeDataUrl(encoded.png, 'image/png'),
    jpeg: decodeDataUrl(encoded.jpeg, 'image/jpeg'),
    webp: decodeDataUrl(encoded.webp, 'image/webp')
  };
}

function decodeDataUrl(value: string, contentType: string): Buffer {
  const prefix = `data:${contentType};base64,`;
  expect(value.startsWith(prefix)).toBe(true);
  return Buffer.from(value.slice(prefix.length), 'base64');
}
