import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('deleting a tag clears the catalogue and cached card tags before resync', async ({ api, authenticatedPage: page }) => {
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
  let release!: () => void;
  const gate = new Promise<void>(resolve => { release = resolve; });
  await page.route(`**/api/boards/${board.id}`, async route => {
    await gate;
    await route.continue();
  });
  try {
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
    await page.getByRole('link', { name: 'Open current board', exact: true }).click();
    await new BoardPage(page).openCard('Todo', 'Tagged card');
    await expect(page.getByRole('button', { name: 'Remove Release', exact: true })).toHaveCount(0);
  } finally {
    release();
  }
});
