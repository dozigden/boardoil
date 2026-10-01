import { expect, test } from '../fixtures/boardOilTest';

test('a rejected system-admin role change restores the role and retains the error without reloading', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('System member role recovery');
  let memberLoads = 0;
  page.on('request', request => {
    if (request.method() === 'GET' && new URL(request.url()).pathname === `/api/system/boards/${board.id}/members`) {
      memberLoads++;
    }
  });
  await page.goto('/');
  await expect(page.getByRole('button', { name: `Configure board ${board.name}`, exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'System admin', exact: true }).click();
  await page.getByRole('link', { name: 'System Settings', exact: true }).click();
  await page.getByRole('button', { name: `Manage members for board ${board.name}`, exact: true }).click();
  const role = page.getByRole('combobox');
  await expect(role).toBeEnabled();
  await expect(role).toHaveValue('Owner');

  await role.selectOption('Contributor');

  await expect(page.getByText('Board must have at least one owner.', { exact: true })).toBeVisible();
  await expect(role).toBeEnabled();
  await expect(role).toHaveValue('Owner');
  expect(memberLoads).toBe(1);
});

test('system member removal confirmation cannot delete from a different board', async ({ api, authenticatedPage: page }) => {
  const previousBoard = await api.createBoard('Previous system member board');
  const removalBoard = await api.createBoard('System member confirmation board');
  await page.goto(`/admin/system/boards/${previousBoard.id}/members`);
  await expect(page.getByRole('combobox')).toBeEnabled();
  await page.getByRole('heading').getByRole('link', { name: 'Boards', exact: true }).click();
  await page.getByRole('button', { name: `Manage members for board ${removalBoard.name}`, exact: true }).click();
  await expect(page.getByRole('combobox')).toBeEnabled();

  const deletions: string[] = [];
  page.on('request', request => {
    if (request.method() === 'DELETE' && /\/api\/system\/boards\/\d+\/members\//.test(request.url())) {
      deletions.push(request.url());
    }
  });
  await page.getByRole('button', { name: 'Remove', exact: true }).click();
  const confirmation = page.getByRole('dialog', { name: 'Remove board member', exact: true });
  await expect(confirmation).toBeVisible();
  await page.goBack();
  await page.goBack();
  await expect(page).toHaveURL(`/admin/system/boards/${previousBoard.id}/members`);
  await expect(page.getByRole('combobox')).toBeEnabled();

  await confirmation.getByRole('button', { name: 'Remove', exact: true }).click();
  await expect(confirmation).toBeHidden();
  expect(deletions).toEqual([]);
});
