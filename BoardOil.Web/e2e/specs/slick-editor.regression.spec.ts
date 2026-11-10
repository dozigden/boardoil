import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { holdNextSlickDefaultStyle, rejectNextSlickSubmission } from '../support/slickSubmission';

for (const operation of ['create', 'update'] as const) {
  test(`slick ${operation} uses restricted styles and retains a rejected draft for correction`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Slick ${operation} form`);
    const slick = await api.createSlick(board, 'Existing slick');
    await page.goto(`/boards/${board.id}/admin/slicks/${operation === 'create' ? 'new' : slick.id}`);
    const dialog = page.getByRole('dialog');
    const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
    const style = dialog.getByRole('combobox', { name: 'Style', exact: true });
    const save = dialog.getByRole('button', { name: operation === 'create' ? 'Create slick' : 'Save slick', exact: true });
    await expect(style.getByRole('option')).toHaveText(['Solid', 'Presets']);
    await style.selectOption('solid');
    await dialog.getByLabel('Background Color', { exact: true }).fill('#123456');
    await dialog.getByRole('combobox', { name: 'Text Color Mode', exact: true }).selectOption('custom');
    await dialog.getByLabel('Text Color', { exact: true }).fill('#abcdef');
    await dialog.getByRole('combobox', { name: 'Border', exact: true }).selectOption('custom');
    await dialog.getByLabel('Border Color', { exact: true }).fill('#654321');
    await name.fill('');
    await save.click();
    await expect(name).toHaveAccessibleDescription('Slick name is required.');
    await expect(name).toBeFocused();
    await expect(page.getByText('Slick name is required.', { exact: true })).toHaveCount(1);
    await expect(dialog.getByLabel('Background Color', { exact: true })).toHaveValue('#123456');

    await name.fill('Saved slick');
    await expect(name).not.toHaveAttribute('aria-invalid', 'true');
    await save.click();
    await expect(dialog).toBeHidden();
    await page.getByRole('button', { name: 'Edit slick Saved slick', exact: true }).click();
    await expect(style).toHaveValue('solid');
    await expect(dialog.getByLabel('Background Color', { exact: true })).toHaveValue('#123456');
    await expect(dialog.getByLabel('Text Color', { exact: true })).toHaveValue('#abcdef');
    await expect(dialog.getByLabel('Border Color', { exact: true })).toHaveValue('#654321');
    await style.selectOption('presets');
    await dialog.getByRole('button', { name: 'Preset 8', exact: true }).click();
    await dialog.getByRole('button', { name: 'Save slick', exact: true }).click();
    await expect(dialog).toBeHidden();
    await page.getByRole('button', { name: 'Edit slick Saved slick', exact: true }).click();
    await expect(style).toHaveValue('presets');
    await expect(dialog.getByRole('button', { name: 'Preset 8', exact: true })).toHaveAttribute('aria-pressed', 'true');
    for (const width of [1280, 390]) {
      await page.setViewportSize({ width, height: 900 });
      await page.screenshot({ path: test.info().outputPath(`slick-presets-${width}.png`), fullPage: true });
    }
  });
}

test('slick style and deletion failures stay in the dialog and allow retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Slick errors');
  const slick = await api.createSlick(board, 'Retain this slick');
  await page.goto(`/boards/${board.id}/admin/slicks/${slick.id}`);
  const editor = page.getByRole('dialog', { name: 'Edit Slick: Retain this slick', exact: true });
  const style = editor.getByRole('group', { name: 'Style', exact: true });
  await style.getByRole('button', { name: 'Preset 6', exact: true }).click();
  await rejectNextSlickSubmission(page, board.id, 'PUT', {
    message: 'Validation failed.', validationErrors: { stylePropertiesJson: ['Style could not be saved.'] }
  });
  await editor.getByRole('button', { name: 'Save slick', exact: true }).click();
  await expect(style).toHaveAccessibleDescription('Style could not be saved.');
  await expect(style).toBeFocused();
  await expect(style.getByRole('button', { name: 'Preset 6', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.getByText('Style could not be saved.', { exact: true })).toHaveCount(1);
  await style.getByRole('button', { name: 'Preset 7', exact: true }).click();
  await expect(style).not.toHaveAttribute('aria-invalid', 'true');

  await editor.getByRole('button', { name: 'Delete slick', exact: true }).click();
  const confirmation = page.getByRole('dialog', { name: 'Delete slick', exact: true });
  await confirmation.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(editor).toBeVisible();
  await rejectNextSlickSubmission(page, board.id, 'DELETE', { message: 'Deletion temporarily unavailable.' });
  await editor.getByRole('button', { name: 'Delete slick', exact: true }).click();
  await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(editor.getByRole('alert')).toHaveText('Deletion temporarily unavailable.');
  await expect(editor.getByRole('alert')).toBeFocused();
  await expect(page.getByText('Deletion temporarily unavailable.', { exact: true })).toHaveCount(1);
  await editor.getByRole('button', { name: 'Delete slick', exact: true }).click();
  await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(editor).toBeHidden();
  await expect(page.getByRole('button', { name: 'Edit slick Retain this slick', exact: true })).toHaveCount(0);
});

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

for (const cancel of [false, true]) {
  test(`slick creation opens while defaults load and ${cancel ? 'can be cancelled' : 'unlocks with the loaded style'}`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard('Slick creation loading');
    const pending = await holdNextSlickDefaultStyle(page, board.id);
    try {
      await page.goto(`/boards/${board.id}/admin/slicks/new`);
      await pending.request;
      const dialog = page.getByRole('dialog', { name: 'Add Slick', exact: true });
      const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
      const save = dialog.getByRole('button', { name: 'Create slick', exact: true });
      const cancelButton = dialog.getByRole('button', { name: 'Cancel editing', exact: true });
      await expect(dialog).toBeVisible();
      await expect(name).toBeDisabled();
      await expect(dialog.getByRole('combobox', { name: 'Style', exact: true })).toBeDisabled();
      await expect(save).toBeDisabled();
      await expect(cancelButton).toBeEnabled();
      if (cancel) {
        await cancelButton.click();
        await expect(page).toHaveURL(`/boards/${board.id}/admin/slicks`);
        await expect(dialog).toBeHidden();
      }
      pending.release();
      await pending.completion;
      if (cancel) {
        await expect(dialog).toBeHidden();
        await expect(page).toHaveURL(`/boards/${board.id}/admin/slicks`);
      } else {
        await expect(name).toBeEnabled();
        await expect(save).toBeEnabled();
        await expect(dialog.getByRole('button', { name: 'Preset 9', exact: true })).toHaveAttribute('aria-pressed', 'true');
      }
    } finally {
      pending.release();
    }
  });
}
