import { ADMIN_PASSWORD, ADMIN_USER_NAME, expect, test } from '../fixtures/boardOilTest';

for (const redirect of ['internal', 'external'] as const) {
  test(`login retains rejected credentials and handles an ${redirect} redirect safely`, async ({ api, page }) => {
    const board = await api.createBoard(`Login ${redirect} redirect`);
    const target = redirect === 'internal' ? `/boards/${board.id}` : 'https://example.com/';
    await page.goto(`/login?redirect=${encodeURIComponent(target)}`);
    const userName = page.getByLabel('Username', { exact: true });
    const password = page.getByLabel('Password', { exact: true });
    await userName.fill(ADMIN_USER_NAME);
    await password.fill('Wrong password');
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    await expect(page.getByText('Invalid username or password.', { exact: true })).toHaveCount(1);
    await expect(userName).toHaveValue(ADMIN_USER_NAME);
    await expect(password).toHaveValue('Wrong password');
    await password.fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: 'Login', exact: true }).click();
    if (redirect === 'internal') await expect(page).toHaveURL(`/boards/${board.id}`);
    else await expect(page).toHaveURL('/');
  });
}
