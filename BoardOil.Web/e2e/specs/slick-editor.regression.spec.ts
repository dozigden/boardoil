import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('deleting a slick clears card membership before the background resync completes', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Slick deletion cleanup');
  const slick = await api.createSlick(board, 'Remove membership');
  await api.createCard(board, 'Todo', 'Previously slicked', '', [], slick.name);
  await page.goto(`/boards/${board.id}/admin/slicks/${slick.id}`);
  await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toHaveValue(slick.name);
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route(`**/api/boards/${board.id}`, async route => {
    await gate;
    await route.continue();
  });
  try {
    await page.getByRole('button', { name: 'Delete slick', exact: true }).click();
    const response = page.waitForResponse(result => result.request().method() === 'DELETE'
      && result.url().endsWith(`/slicks/${slick.id}`));
    await page.getByRole('dialog', { name: 'Delete slick', exact: true })
      .getByRole('button', { name: 'Delete', exact: true }).click();
    expect((await response).ok()).toBe(true);
    await expect(page.getByRole('dialog')).toBeHidden();
    await page.getByRole('link', { name: 'Open current board', exact: true }).click();
    await new BoardPage(page).openCard('Todo', 'Previously slicked');
    await expect(page.getByTitle('Select slick', { exact: true })).toHaveText('No slick');
  } finally {
    release();
  }
});
