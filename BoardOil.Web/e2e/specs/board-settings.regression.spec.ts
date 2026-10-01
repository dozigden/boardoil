import { expect, test } from '../fixtures/boardOilTest';

test('saving board details updates the header and deletion clears the selected board', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Board settings ownership');
  await page.goto(`/boards/${board.id}/admin/details`);
  const renamed = `${board.name} renamed`;
  await page.getByRole('textbox', { name: 'Board name', exact: true }).fill(renamed);
  await page.getByRole('button', { name: 'Save details', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveText(renamed);
  await expect(page.getByRole('button', { name: 'Save details', exact: true })).toBeDisabled();

  await page.getByRole('link', { name: 'Delete board', exact: true }).click();
  await page.getByRole('textbox', { name: 'Confirm board name', exact: true }).fill(renamed);
  await page.getByRole('button', { name: 'Delete board', exact: true }).click();
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('heading', { name: 'Boards', exact: true })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: `Configure board ${renamed}`, exact: true })).toHaveCount(0);
});
