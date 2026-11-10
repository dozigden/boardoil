import { rejectNextBoardSettingsWrite } from '../support/boardSettingsResponse';
import { authenticatePage, expect, test } from '../fixtures/boardOilTest';

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

test('board details retain validation, reset to saved values and export without saving a draft', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Board settings form');
  await page.goto(`/boards/${board.id}/admin/details`);
  const name = page.getByRole('textbox', { name: 'Board name', exact: true });
  const description = page.getByRole('textbox', { name: 'Description (optional)', exact: true });
  const save = page.getByRole('button', { name: 'Save details', exact: true });
  const reset = page.getByRole('button', { name: 'Reset', exact: true });
  await name.fill('   ');
  await description.fill('Retained description');
  await save.click();
  await expect(name).toHaveAccessibleDescription('Board name is required.');
  await expect(name).toBeFocused();
  await expect(description).toHaveValue('Retained description');
  await page.screenshot({ path: test.info().outputPath('board-details-validation.png') });
  await name.fill('  Canonical board  ');
  await description.fill('  Saved description  ');
  await page.getByRole('checkbox', { name: 'Enable slick cohesion mode', exact: true }).uncheck();
  await page.getByRole('checkbox', { name: 'Show attachment thumbnails on cards', exact: true }).uncheck();
  await save.click();
  await expect(save).toBeDisabled();
  await expect(name).toHaveValue('Canonical board');
  await expect(description).toHaveValue('Saved description');
  await name.fill('Unsaved name');
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export', exact: true }).click();
  expect((await download).suggestedFilename()).toMatch(/\.zip$/);
  await expect(name).toHaveValue('Unsaved name');
  await expect(save).toBeEnabled();
  await reset.click();
  await expect(name).toHaveValue('Canonical board');
  await expect(save).toBeDisabled();
  await expect(page.getByRole('checkbox', { name: 'Enable slick cohesion mode', exact: true })).not.toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'Show attachment thumbnails on cards', exact: true })).not.toBeChecked();
  await page.reload();
  await expect(name).toHaveValue('Canonical board');
  await expect(description).toHaveValue('Saved description');
});

test('pending board saves block navigation and general rejections retain edits for retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Pending board settings');
  await page.goto(`/boards/${board.id}/admin/details`);
  const name = page.getByRole('textbox', { name: 'Board name', exact: true });
  await name.fill('Retained board edit');
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  await rejectNextBoardSettingsWrite(page, board.id, 'PUT', ready);
  try {
    await page.getByRole('button', { name: 'Save details', exact: true }).click();
    await expect(name).toBeDisabled();
    await expect(page.getByRole('button', { name: 'Reset', exact: true })).toBeDisabled();
    await page.getByRole('link', { name: 'Columns', exact: true }).click();
    await expect(page).toHaveURL(`/boards/${board.id}/admin/details`);
  } finally {
    release();
  }
  await expect(page.getByRole('alert')).toContainText('Board operation could not be completed.');
  await expect(page.getByText('Board operation could not be completed.', { exact: true })).toHaveCount(1);
  await expect(name).toHaveValue('Retained board edit');
  await page.getByRole('button', { name: 'Save details', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Save details', exact: true })).toBeDisabled();
});

test('board deletion requires its name and retains confirmation after rejection', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Delete confirmation');
  await page.goto(`/boards/${board.id}/admin/delete`);
  const name = page.getByRole('textbox', { name: 'Confirm board name', exact: true });
  const remove = page.getByRole('button', { name: 'Delete board', exact: true });
  await name.fill('Wrong name');
  await expect(remove).toBeDisabled();
  await name.fill(board.name);
  await rejectNextBoardSettingsWrite(page, board.id, 'DELETE');
  await remove.click();
  await expect(page.getByRole('alert')).toContainText('Board operation could not be completed.');
  await expect(name).toHaveValue(board.name);
  await expect(remove).toBeEnabled();
  await remove.click();
  await expect(page).toHaveURL('/');
  await expect(page.getByRole('link', { name: 'Open current board', exact: true })).toHaveCount(0);
});

test('contributors can read board details but cannot edit or export them', async ({ api, page }) => {
  const board = await api.createBoard('Read only settings');
  const user = await api.createUser(`settings-reader-${board.id}`, 'Password1234!');
  await api.addBoardMember(board.id, user.id, 'Contributor');
  await authenticatePage(page, user.userName, 'Password1234!');
  await page.goto(`/boards/${board.id}/admin/details`);
  await expect(page.getByRole('textbox', { name: 'Board name', exact: true })).toHaveValue(board.name);
  await expect(page.getByRole('textbox', { name: 'Board name', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Save details', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Export', exact: true })).toBeDisabled();
});
