import { expect, test } from '../fixtures/boardOilTest';

test('owners can inspect and download live and archived attachments and follow card links', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Attachment inventory');
  const live = await api.createCard(board, 'Todo', 'Live attachment owner');
  const archived = await api.createCard(board, 'Todo', 'Archived attachment owner');
  await api.uploadAttachment(board.id, live.id, 'notes.txt', 'text/plain', Buffer.from('hello'));
  await api.uploadAttachment(board.id, archived.id, 'report.pdf', 'application/pdf', Buffer.from('report'));
  await api.archiveCard(board.id, archived.id);
  await page.goto(`/boards/${board.id}/admin`);

  await page.getByRole('link', { name: 'Attachments', exact: true }).click();

  const inventory = page.getByRole('region', { name: 'Board attachments' });
  await expect(inventory.getByLabel('Attachment totals')).toContainText('2 attachments');
  await expect(inventory.getByLabel('Attachment totals')).toContainText('11 B uploaded files');
  await expect(inventory.getByRole('row').filter({ hasText: 'notes.txt' })).toContainText('Live');
  await expect(inventory.getByRole('row').filter({ hasText: 'report.pdf' })).toContainText('Archived');
  await expect(inventory.getByRole('row').filter({ hasText: 'report.pdf' })).toContainText('application/pdf');
  const pendingDownload = page.waitForEvent('download');
  await inventory.getByRole('link', { name: 'Download report.pdf' }).press('Enter');
  expect((await pendingDownload).suggestedFilename()).toBe('report.pdf');

  await inventory.getByRole('link', { name: `#${archived.id}: Archived attachment owner`, exact: true }).press('Enter');
  await expect(page.getByRole('dialog')).toContainText('Archived attachment owner');
  await page.reload();
  await expect(page.getByRole('dialog')).toContainText('Archived attachment owner');
  await page.getByRole('button', { name: 'Close archived card', exact: true }).click();
  await expect(page.getByRole('searchbox')).toBeVisible();
  const archivedRow = page.getByRole('row').filter({ hasText: 'Archived attachment owner' });
  await archivedRow.press('Enter');
  await expect(page.getByRole('dialog')).toContainText('Archived attachment owner');
  await page.getByRole('button', { name: 'Close archived card', exact: true }).click();
  await archivedRow.press('Space');
  await expect(page.getByRole('dialog')).toContainText('Archived attachment owner');
  await page.getByRole('button', { name: 'Close archived card', exact: true }).click();
  // Row activation pushes archive history entries; return directly to the inventory.
  await page.goto(`/boards/${board.id}/admin/attachments`);
  await expect(inventory).toBeVisible();
  await inventory.getByRole('link', { name: `#${live.id}: Live attachment owner`, exact: true }).press('Enter');
  await expect(page.getByRole('dialog')).toContainText('Live attachment owner');

  await page.goBack();
  await api.uploadAttachment(board.id, live.id, 'extra.bin', 'application/octet-stream', Buffer.from('new'));
  await page.reload();
  await expect(inventory.getByLabel('Attachment totals')).toContainText('3 attachments');
  await expect(inventory.getByLabel('Attachment totals')).toContainText('14 B uploaded files');
});

test('returning to a page that lost its last match recovers the last valid page', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Attachment page recovery');
  const first = await api.createCard(board, 'Todo', 'First card');
  const last = await api.createCard(board, 'Todo', 'Last card');
  for (let index = 0; index < 50; index++) {
    await api.uploadAttachment(board.id, first.id, `a-${index}.txt`, 'text/plain', Buffer.from('a'));
  }
  await api.uploadAttachment(board.id, last.id, 'z.txt', 'text/plain', Buffer.from('z'));
  await page.goto(`/boards/${board.id}/admin/attachments?offset=50&state=live&sort=name&direction=asc`);
  const inventory = page.getByRole('region', { name: 'Board attachments' });
  await expect(inventory).toContainText('Showing 51-51 of 51');
  await inventory.getByRole('link', { name: `#${last.id}: Last card`, exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('Last card');
  await api.archiveCard(board.id, last.id);

  await page.goBack();

  await expect(inventory).toContainText('Showing 1-50 of 50');
  await expect(inventory.getByRole('link', { name: /^Download / })).toHaveCount(50);
  await expect(page).toHaveURL(/offset=0/);
  await expect(inventory.getByRole('combobox', { name: 'State', exact: true })).toHaveValue('live');
  await expect(inventory.getByRole('combobox', { name: 'Sort', exact: true })).toHaveValue('name:asc');
  await expect(inventory.getByRole('button', { name: 'Previous', exact: true })).toBeDisabled();
  await expect(inventory.getByRole('button', { name: 'Next', exact: true })).toBeDisabled();

  await api.archiveCard(board.id, first.id);
  await page.goto(`/boards/${board.id}/admin/attachments?offset=50&state=live&sort=name&direction=asc`);
  await expect(page).toHaveURL(/offset=0/);
  await expect(inventory).toContainText('No attachments on live cards.');
  await expect(inventory.getByRole('button', { name: 'Previous', exact: true })).toBeDisabled();
  await expect(inventory.getByRole('button', { name: 'Next', exact: true })).toBeDisabled();
});

test('inventory pages are fetched from the server and filters reset the page', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Paged attachment inventory');
  const live = await api.createCard(board, 'Todo', 'Live files');
  const archived = await api.createCard(board, 'Todo', 'Archived files');
  for (let index = 1; index <= 51; index++) {
    await api.uploadAttachment(board.id, live.id, `live-${String(index).padStart(2, '0')}.bin`, 'application/octet-stream', Buffer.alloc(index));
  }
  await api.uploadAttachment(board.id, archived.id, 'archive.bin', 'application/octet-stream', Buffer.alloc(100));
  await api.archiveCard(board.id, archived.id);
  await page.goto(`/boards/${board.id}/admin/attachments`);
  const inventory = page.getByRole('region', { name: 'Board attachments' });
  await expect(inventory).toContainText('Showing 1-50 of 52');
  await expect(inventory.getByRole('link', { name: /^Download / })).toHaveCount(50);

  await inventory.getByRole('combobox', { name: 'Sort', exact: true }).selectOption('name:asc');
  await expect(page).toHaveURL(/sort=name/);
  await expect(inventory.getByRole('link', { name: /^Download / }).first()).toHaveText('archive.bin');
  await inventory.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(inventory).toContainText('Showing 51-52 of 52');
  await expect(inventory.getByRole('link', { name: /^Download / })).toHaveText(['live-50.bin', 'live-51.bin']);
  await page.reload();
  await expect(inventory).toContainText('Showing 51-52 of 52');

  await inventory.getByRole('combobox', { name: 'State', exact: true }).selectOption('archived');
  await expect(inventory).toContainText('Showing 1-1 of 1');
  await expect(inventory.getByRole('link', { name: /^Download / })).toHaveText(['archive.bin']);
  await expect(inventory.getByLabel('Attachment totals')).toContainText('52 attachments');
  await expect(inventory.getByRole('button', { name: 'Previous', exact: true })).toBeDisabled();
  await expect(inventory.getByRole('button', { name: 'Next', exact: true })).toBeDisabled();

  await inventory.getByRole('combobox', { name: 'State', exact: true }).selectOption('live');
  await inventory.getByRole('combobox', { name: 'Sort', exact: true }).selectOption('size:desc');
  await expect(inventory).toContainText('Showing 1-50 of 51');
  await expect(inventory.getByRole('link', { name: /^Download / }).first()).toHaveText('live-51.bin');
  await inventory.getByRole('button', { name: 'Next', exact: true }).click();
  await expect(inventory.getByRole('link', { name: /^Download / })).toHaveText(['live-01.bin']);
  await inventory.getByRole('button', { name: 'Previous', exact: true }).click();
  await expect(inventory).toContainText('Showing 1-50 of 51');
});
