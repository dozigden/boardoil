import { expect, test } from '../fixtures/boardOilTest';

test('deleting a tag completes after the tag disappears from the catalogue', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Tag deletion regression');
  await api.createCard(board, 'Todo', 'Tagged card', '', ['Release']);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/api/system/error-logs:report-client-error')) {
      errors.push(request.postDataJSON().message);
    }
  });

  await page.goto(`/boards/${board.id}/admin/tags`);
  await page.getByRole('button', { name: 'Edit tag Release', exact: true }).click();
  await page.getByRole('button', { name: 'Delete tag', exact: true }).click();
  const deletion = page.waitForResponse(response => response.request().method() === 'DELETE'
    && response.url().includes(`/api/boards/${board.id}/tags/`));
  await page.getByRole('dialog', { name: 'Delete tag', exact: true })
    .getByRole('button', { name: 'Delete', exact: true }).click();

  expect((await deletion).ok()).toBe(true);
  await expect(page).toHaveURL(`/boards/${board.id}/admin/tags`);
  await expect(page.getByRole('button', { name: 'Edit tag Release', exact: true })).toHaveCount(0);
  await expect(page.getByText('No tags yet. Add one to get started.', { exact: true })).toBeVisible();
  expect(errors).toEqual([]);
});
