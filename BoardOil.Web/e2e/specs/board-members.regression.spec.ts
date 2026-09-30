import { expect, test } from '../fixtures/boardOilTest';

test('a rejected last-owner demotion restores the role control and keeps the error visible', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Member role recovery');
  let memberLoads = 0;
  page.on('request', request => {
    if (request.method() === 'GET' && request.url().endsWith(`/boards/${board.id}/members`)) {
      memberLoads++;
    }
  });
  await page.goto(`/boards/${board.id}/admin/members`);
  const role = page.getByRole('combobox');
  await expect(role).toBeEnabled();
  await expect(role).toHaveValue('Owner');

  await role.selectOption('Contributor');

  await expect(page.getByText('Board must have at least one owner.', { exact: true })).toBeVisible();
  await expect(role).toBeEnabled();
  await expect(role).toHaveValue('Owner');
  expect(memberLoads).toBe(1);
});
