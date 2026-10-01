import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

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

test('accepting member removal after browser Back to another board sends no deletion', async ({ api, authenticatedPage: page }) => {
  const previousBoard = await api.createBoard('Previous member board');
  const removalBoard = await api.createBoard('Member confirmation board');
  await new BoardPage(page).open(previousBoard.id);
  await page.getByRole('button', { name: 'Switch board', exact: true }).click();
  await page.getByRole('link', { name: `${removalBoard.name} #${removalBoard.id}`, exact: true }).click();
  await expect(page).toHaveURL(`/boards/${removalBoard.id}`);
  await page.getByRole('button', { name: 'System admin', exact: true }).click();
  await page.getByRole('link', { name: 'Board Configuration', exact: true }).click();
  await page.getByRole('link', { name: 'Members', exact: true }).click();
  await expect(page.getByRole('combobox')).toBeEnabled();

  const deletions: string[] = [];
  page.on('request', request => {
    if (request.method() === 'DELETE' && /\/boards\/\d+\/members\//.test(request.url())) {
      deletions.push(request.url());
    }
  });
  await page.getByRole('button', { name: 'Remove', exact: true }).click();
  const confirmation = page.getByRole('dialog', { name: 'Remove board member', exact: true });
  await expect(confirmation).toBeVisible();
  await page.goBack();
  await page.goBack();
  await page.goBack();
  await expect(page).toHaveURL(`/boards/${previousBoard.id}`);
  await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveText(previousBoard.name);

  await confirmation.getByRole('button', { name: 'Remove', exact: true }).click();
  await expect(confirmation).toBeHidden();
  expect(deletions).toEqual([]);
});

test('reopening a card retries a failed member lookup', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Member lookup retry');
  await api.createCard(board, 'Todo', 'Retry member lookup');
  let memberLoads = 0;
  await page.route(`**/api/boards/${board.id}/members`, async route => {
    memberLoads++;
    if (memberLoads === 1) {
      await route.fulfill({ status: 503, json: { success: false, message: 'Member lookup unavailable.' } });
      return;
    }
    await route.continue();
  });
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Retry member lookup');
  await expect(page.getByText('Member lookup unavailable.', { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Cancel editing', exact: true }).and(page.getByTitle('Cancel editing', { exact: true })).click();
  await expect(page.getByRole('dialog')).toBeHidden();
  await boardPage.openCard('Todo', 'Retry member lookup');
  await page.getByTitle('Select assigned user', { exact: true }).click();
  await expect(page.getByRole('menu', { name: 'Select assigned user' }).getByRole('button', { name: /smoke-admin/ })).toBeVisible();
  expect(memberLoads).toBe(2);
});
