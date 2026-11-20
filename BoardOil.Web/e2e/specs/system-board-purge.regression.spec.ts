import { expect, test } from '../fixtures/boardOilTest';

test('queues a purge for one board from Deleted Boards', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Manual purge board');

  await page.goto(`/boards/${board.id}/admin/delete`);
  await page.getByRole('textbox', { name: 'Confirm board name' }).fill(board.name);
  await page.getByRole('button', { name: 'Delete board', exact: true }).click();
  await expect(page).toHaveURL('/');

  await page.goto('/admin/system/boards/deleted');
  const purge = page.getByRole('button', { name: `Purge board ${board.name}`, exact: true });
  await purge.click();
  const confirmation = page.getByRole('dialog', { name: 'Permanently delete board' });
  await expect(confirmation.getByText(board.name, { exact: false })).toBeVisible();
  await confirmation.getByRole('button', { name: 'Purge board', exact: true }).click();

  await expect(page.getByText('Board purge queued.', { exact: true })).toBeVisible();
  await expect(page.getByText('Board purged.', { exact: true })).toBeVisible();
  await expect(page).toHaveURL('/admin/system/boards/deleted');
  await expect(purge).toHaveCount(0);
});
