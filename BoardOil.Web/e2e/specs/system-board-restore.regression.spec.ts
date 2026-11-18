import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('system administrators can restore a board and reopen its original card link', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('System restore board');
  const otherBoard = await api.createBoard('System restore current board');
  const card = await api.createCard(board, 'Todo', 'Card before deletion');
  const cardPath = `/boards/${board.id}/card/${card.id}`;

  await page.goto(`/boards/${board.id}/admin/delete`);
  await page.getByRole('textbox', { name: 'Confirm board name' }).fill(board.name);
  await page.getByRole('button', { name: 'Delete board', exact: true }).click();
  await expect(page).toHaveURL('/');

  await new BoardPage(page).open(otherBoard.id);
  await page.getByRole('button', { name: 'System admin', exact: true }).click();
  await page.getByRole('link', { name: 'System Settings', exact: true }).click();
  const restore = page.getByRole('button', { name: `Restore board ${board.name}`, exact: true });
  await expect(restore).toHaveCount(0);
  await page.getByRole('tab', { name: 'Deleted Boards', exact: true }).click();
  await expect(page).toHaveURL('/admin/system/boards/deleted');
  await expect(restore).toBeEnabled();
  await restore.click();
  await expect(restore).toHaveCount(0);
  await page.getByRole('tab', { name: 'Boards', exact: true }).click();
  await expect(page).toHaveURL('/admin/system/boards');
  await expect(page.getByRole('button', { name: `Manage members for board ${board.name}`, exact: true })).toBeVisible();

  await page.getByRole('button', { name: 'Switch board', exact: true }).click();
  await expect(page.getByRole('link', { name: `${board.name} #${board.id}`, exact: true })).toBeVisible();

  await page.goto(cardPath);
  await expect(page.getByRole('dialog', { name: `#${card.id} Card before deletion`, exact: true })).toBeVisible();
  await expect(page).toHaveURL(cardPath);
});
