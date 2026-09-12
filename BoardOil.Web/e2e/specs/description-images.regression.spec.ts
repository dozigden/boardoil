import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

const onePixelPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=',
  'base64'
);
const onePixelGif = Buffer.from('R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==', 'base64');
const encodedImageFileName = 'Architecture (final) #1.png';
const encodedImageAlt = 'Architecture (final) #1';

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
  await expect(dialog.getByRole('link', { name: `Download ${encodedImageFileName}` })).toBeVisible();
  await dialog.getByLabel('Choose an image for Card description').setInputFiles({
    name: encodedImageFileName.toUpperCase(),
    mimeType: 'image/png',
    buffer: onePixelPng
  });
  await expect(reopenedDescription.locator(`img[alt="${encodedImageAlt}"]`)).toHaveCount(2);
  expect(uploadRequests).toBe(1);

  await dialog.getByRole('button', { name: 'Save card' }).click();
  await expect(dialog).toBeHidden();
  await boardPage.openCard('Todo', 'Image card');
  await expect(dialog.getByLabel('Card description', { exact: true })
    .locator(`img[alt="${encodedImageAlt}"]`)).toHaveCount(2);
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
