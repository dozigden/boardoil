import { expect, test } from '../fixtures/boardOilTest';
import { holdNextTagDefaultStyle, rejectNextTagSubmission } from '../support/tagSubmission';

for (const operation of ['create', 'update'] as const) {
  test(`tag ${operation} shows backend name validation and preserves the draft`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Tag ${operation} validation`);
    await api.createTag(board.id, { name: 'Existing' });
    const editable = await api.createTag(board.id, { name: 'Editable', emoji: '🚀' });
    await page.goto(`/boards/${board.id}/admin/tags/${operation === 'create' ? 'new' : editable.id}`);
    const dialog = page.getByRole('dialog');
    const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
    const save = dialog.getByRole('button', { name: operation === 'create' ? 'Create tag' : 'Save tag style', exact: true });
    const style = dialog.getByRole('combobox', { name: 'Style', exact: true });
    await style.selectOption('gradient');
    await dialog.getByLabel('Left Color', { exact: true }).fill('#123456');
    await dialog.getByLabel('Right Color', { exact: true }).fill('#654321');
    await name.fill('');
    await save.click();
    await expect(name).toHaveAccessibleDescription('Tag name is required.');
    await expect(name).toBeFocused();

    const longName = 'x'.repeat(41);
    await name.fill(longName);
    await save.click();
    await expect(name).toHaveValue(longName);
    await expect(name).toHaveAccessibleDescription(`Tag '${longName}' must be 40 characters or fewer.`);
    await name.fill('EXISTING');
    await save.click();
    await expect(name).toHaveAccessibleDescription("Tag 'EXISTING' already exists.");
    await expect(page.getByText("Tag 'EXISTING' already exists.", { exact: true })).toHaveCount(1);
    await expect(style).toHaveValue('gradient');
    await expect(dialog.getByLabel('Left Color', { exact: true })).toHaveValue('#123456');

    await name.fill('Saved tag');
    await expect(name).not.toHaveAttribute('aria-invalid', 'true');
    await save.click();
    await expect(dialog).toBeHidden();
    await page.getByRole('button', { name: 'Edit tag Saved tag', exact: true }).click();
    await expect(style).toHaveValue('gradient');
    await expect(dialog.getByLabel('Left Color', { exact: true })).toHaveValue('#123456');
    await expect(dialog.getByLabel('Right Color', { exact: true })).toHaveValue('#654321');
    if (operation === 'update') await expect(dialog.getByRole('button', { name: 'Emoji', exact: true })).toHaveText('🚀');
  });
}

test('tag emoji, style and general errors stay in the dialog and permit correction', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Tag mapped feedback');
  const tag = await api.createTag(board.id, { name: 'Release', emoji: '🚀' });
  await page.goto(`/boards/${board.id}/admin/tags/${tag.id}`);
  const dialog = page.getByRole('dialog', { name: 'Edit Tag: Release', exact: true });
  const save = dialog.getByRole('button', { name: 'Save tag style', exact: true });
  await rejectNextTagSubmission(page, board.id, 'PUT', {
    message: 'Validation failed.', validationErrors: { emoji: ['Choose another emoji.'] }
  });
  await save.click();
  const emoji = dialog.getByRole('group', { name: 'Emoji', exact: true });
  await expect(emoji).toHaveAccessibleDescription('Choose another emoji.');
  await expect(emoji).toBeFocused();
  await dialog.getByRole('button', { name: 'Emoji', exact: true }).click();
  await page.getByRole('button', { name: 'Clear', exact: true }).click();
  await expect(emoji).not.toHaveAttribute('aria-invalid', 'true');

  await rejectNextTagSubmission(page, board.id, 'PUT', {
    message: 'Validation failed.', validationErrors: { stylePropertiesJson: ['Choose another style.'] }
  });
  await save.click();
  const styleGroup = dialog.getByRole('group', { name: 'Style', exact: true });
  await expect(styleGroup).toHaveAccessibleDescription('Choose another style.');
  await expect(styleGroup).toBeFocused();
  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`tag-validation-${width}.png`) });
  }
  await dialog.getByRole('combobox', { name: 'Style', exact: true }).selectOption('auto');
  await expect(styleGroup).not.toHaveAttribute('aria-invalid', 'true');

  await rejectNextTagSubmission(page, board.id, 'PUT', { message: 'Saving is temporarily unavailable.' });
  await save.click();
  await expect(dialog.getByRole('alert')).toHaveText('Saving is temporarily unavailable.');
  await expect(page.getByText('Saving is temporarily unavailable.', { exact: true })).toHaveCount(1);
  await save.click();
  await expect(dialog).toBeHidden();
  expect(await api.getTags(board.id)).toEqual([expect.objectContaining({ name: 'Release', emoji: null, styleName: 'auto' })]);
});

test('tag deletion cancellation and rejection preserve the draft until successful retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Tag deletion feedback');
  const tag = await api.createTag(board.id, { name: 'Release' });
  await page.goto(`/boards/${board.id}/admin/tags/${tag.id}`);
  const dialog = page.getByRole('dialog', { name: 'Edit Tag: Release', exact: true });
  const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
  await name.fill('');
  await dialog.getByRole('button', { name: 'Save tag style', exact: true }).click();
  await expect(name).toHaveAccessibleDescription('Tag name is required.');
  const remove = dialog.getByRole('button', { name: 'Delete tag', exact: true });
  await remove.click();
  const confirmation = page.getByRole('dialog', { name: 'Delete tag', exact: true });
  await confirmation.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(name).toHaveAccessibleDescription('Tag name is required.');

  await rejectNextTagSubmission(page, board.id, 'DELETE', { message: 'This tag cannot be deleted yet.' });
  await remove.click();
  await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(dialog.getByRole('alert')).toHaveText('This tag cannot be deleted yet.');
  await expect(name).toHaveValue('');
  await remove.click();
  await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(dialog).toBeHidden();
  expect(await api.getTags(board.id)).toEqual([]);
});

for (const cancel of [false, true]) {
  test(`tag creation locks fields while loading defaults and ${cancel ? 'allows cancellation' : 'uses the returned colour'}`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard('Tag default loading');
    const pending = await holdNextTagDefaultStyle(page, board.id);
    try {
      await page.goto(`/boards/${board.id}/admin/tags/new`);
      await pending.request;
      const dialog = page.getByRole('dialog', { name: 'Add Tag', exact: true });
      const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
      const create = dialog.getByRole('button', { name: 'Create tag', exact: true });
      await expect(name).toBeDisabled();
      await expect(dialog.getByRole('button', { name: 'Emoji', exact: true })).toBeDisabled();
      await expect(dialog.getByRole('combobox', { name: 'Style', exact: true })).toBeDisabled();
      await expect(create).toBeDisabled();
      if (cancel) await dialog.getByRole('button', { name: 'Cancel editing', exact: true }).click();
      pending.release();
      await pending.completion;
      if (cancel) {
        await expect(dialog).toBeHidden();
        await expect(page).toHaveURL(`/boards/${board.id}/admin/tags`);
      } else {
        await expect(name).toBeEnabled();
        await expect(create).toBeEnabled();
        await expect(dialog.getByRole('button', { name: 'Preset 9', exact: true })).toHaveAttribute('aria-pressed', 'true');
      }
    } finally {
      pending.release();
    }
  });
}
