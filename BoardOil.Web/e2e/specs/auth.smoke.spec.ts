import { ADMIN_PASSWORD, ADMIN_USER_NAME, authenticatePage, expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('login survives a protected-page reload and logout protects the board', async ({ api, page }) => {
  const board = await api.createBoard('Smoke authentication');
  const boardPage = new BoardPage(page);

  await test.step('sign in through the browser', async () => {
    await page.goto('/login');
    await page.getByLabel('Username').fill(ADMIN_USER_NAME);
    await page.getByLabel('Password').fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: 'Login' }).click();
    await expect(page.getByRole('heading', { name: 'Boards' })).toBeVisible();
  });

  await test.step('reload a protected page', async () => {
    await boardPage.open(board.id);
    await page.reload();
    await expect(boardPage.column('Todo')).toBeVisible();
  });

  await test.step('log out and confirm the board is protected', async () => {
    await page.getByRole('button', { name: 'User menu' }).click();
    await page.getByRole('button', { name: 'Logout' }).click();
    await expect(page).toHaveURL(/\/login$/);

    await page.goto(`/boards/${board.id}`);
    await expect(page).toHaveURL(/\/login\?redirect=/);
  });
});
