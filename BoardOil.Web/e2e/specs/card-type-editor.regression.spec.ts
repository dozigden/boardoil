import { expect, test } from '../fixtures/boardOilTest';
import { rejectNextCardTypeDefault, rejectNextCardTypeDelete, rejectNextCardTypeSave } from '../support/cardTypeSubmission';

for (const operation of ['create', 'update'] as const) {
  test(`card-type ${operation} keeps validation beside the name and permits correction and retry`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Card type ${operation} validation`);
    await api.createCardType(board.id, 'café');
    const editable = await api.createCardType(board.id, 'Editable type');
    const route = `/boards/${board.id}/admin/card-types/`;
    await page.goto(route + (operation === 'create' ? 'new' : editable.id));
    const dialog = page.getByRole('dialog');
    const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
    const save = dialog.getByRole('button', { name: operation === 'create' ? 'Create card type' : 'Save card type', exact: true });
    const style = dialog.getByRole('combobox', { name: 'Style', exact: true });
    await style.selectOption('solid');
    await dialog.getByLabel('Background Color', { exact: true }).fill('#123456');
    await name.fill('CAFÉ');
    await save.click();

    await expect(dialog.getByRole('alert')).toContainText("Card type 'CAFÉ' already exists.");
    await expect(name).toHaveValue('CAFÉ');
    await expect(name).toHaveAttribute('aria-invalid', 'true');
    await expect(name).toHaveAccessibleDescription("Card type 'CAFÉ' already exists.");
    await expect(name).toBeFocused();
    await expect(style).toHaveValue('solid');
    await expect(dialog.getByLabel('Background Color', { exact: true })).toHaveValue('#123456');
    await expect(page.getByText("Card type 'CAFÉ' already exists.", { exact: true })).toHaveCount(1);
    await page.screenshot({ path: test.info().outputPath('card-type-validation.png') });

    await name.fill('café');
    await expect(dialog.getByRole('alert')).toHaveCount(0);
    await expect(name).not.toHaveAttribute('aria-invalid', 'true');
    await save.click();
    await expect(dialog.getByRole('alert')).toContainText("Card type 'café' already exists.");

    await name.fill('Corrected type');
    await save.click();
    await expect(dialog).toBeHidden();
    await expect(page.getByRole('button', { name: 'Edit card type Corrected type', exact: true })).toBeVisible();
    await page.getByRole('button', { name: 'Edit card type Corrected type', exact: true }).click();
    await expect(dialog.getByRole('alert')).toHaveCount(0);
    await expect(name).toHaveValue('Corrected type');
    await expect(style).toHaveValue('solid');
    await expect(dialog.getByLabel('Background Color', { exact: true })).toHaveValue('#123456');
  });
}

test('card-type summary retains unmapped and general errors, replaces them on retry and resets on reopening', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card type summary validation');
  await page.goto(`/boards/${board.id}/admin/card-types/new`);
  const dialog = page.getByRole('dialog', { name: 'Add Card Type', exact: true });
  const name = dialog.getByRole('textbox', { name: 'Name', exact: true });
  const save = dialog.getByRole('button', { name: 'Create card type', exact: true });
  await name.fill('Retained draft');
  await rejectNextCardTypeSave(page, board.id, {
    message: 'Validation failed.',
    validationErrors: { futureField: ['Additional field needs attention.'], '': ['This combination cannot be saved.'] }
  });
  await save.click();
  await expect(dialog.getByRole('alert')).toContainText('Additional field needs attention.');
  await expect(dialog.getByRole('alert')).toContainText('This combination cannot be saved.');
  await expect(dialog.getByRole('alert')).toBeFocused();
  await expect(name).toHaveValue('Retained draft');

  await rejectNextCardTypeSave(page, board.id, { message: 'Saving is temporarily unavailable.' });
  await save.click();
  await expect(dialog.getByRole('alert')).toHaveText('Saving is temporarily unavailable.');
  await expect(dialog.getByRole('alert')).toBeFocused();
  await expect(name).toHaveValue('Retained draft');
  await expect(page.getByText('Saving is temporarily unavailable.', { exact: true })).toHaveCount(1);

  await dialog.getByRole('button', { name: 'Cancel editing', exact: true }).click();
  await expect(dialog).toBeHidden();
  await page.getByRole('button', { name: 'Add card type', exact: true }).click();
  await expect(name).toHaveValue('');
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await name.fill('Fresh type');
  await save.click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('button', { name: 'Edit card type Fresh type', exact: true })).toBeVisible();
});

for (const operation of ['create', 'update', 'delete'] as const) {
  test(`card-type ${operation} updates the catalogue and closes the editor`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Card type ${operation}`);
    await api.createCardType(board.id, 'Existing type');
    await page.goto(`/boards/${board.id}/admin/card-types`);
    if (operation === 'create') {
      await page.getByRole('button', { name: 'Add card type', exact: true }).click();
    } else {
      await page.getByRole('button', { name: 'Edit card type Existing type', exact: true }).click();
    }

    if (operation === 'delete') {
      await page.getByRole('button', { name: 'Delete card type', exact: true }).click();
      await page.getByRole('dialog', { name: 'Delete card type', exact: true })
        .getByRole('button', { name: 'Delete', exact: true }).click();
    } else {
      await page.getByRole('textbox', { name: 'Name', exact: true }).fill('Saved type');
      const buttonName = operation === 'create' ? 'Create card type' : 'Save card type';
      await page.getByRole('button', { name: buttonName, exact: true }).click();
    }

    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types`);
    if (operation === 'delete') {
      await expect(page.getByRole('button', { name: 'Edit card type Existing type', exact: true })).toHaveCount(0);
    } else {
      await expect(page.getByRole('button', { name: 'Edit card type Saved type', exact: true })).toBeVisible();
    }
  });
}

test('cancelling card-type deletion keeps the type and editor', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card type confirmation');
  const cardType = await api.createCardType(board.id, 'Keep this type');
  await page.goto(`/boards/${board.id}/admin/card-types/${cardType.id}`);
  await page.getByRole('button', { name: 'Delete card type', exact: true }).click();
  const confirmation = page.getByRole('dialog', { name: 'Delete card type', exact: true });
  await confirmation.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(confirmation).toBeHidden();
  await expect(page.getByRole('textbox', { name: 'Name', exact: true })).toHaveValue('Keep this type');
  await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types/${cardType.id}`);
});

test('the card-type list reuses its catalogue after a missing type redirect and a same-board remount', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Missing card type');
  await api.createCardType(board.id, 'Retained type');
  let catalogueLoads = 0;
  page.on('request', request => {
    if (request.method() === 'GET' && request.url().endsWith(`/api/boards/${board.id}/card-types`)) {
      catalogueLoads++;
    }
  });
  await page.goto(`/boards/${board.id}/admin/card-types/2147483647`);
  await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types`);
  await expect(page.getByRole('button', { name: 'Edit card type Retained type', exact: true })).toBeVisible();
  expect(catalogueLoads).toBe(1);

  await page.getByRole('link', { name: 'Open current board', exact: true }).click();
  await expect(page.getByRole('article', { name: 'Todo column', exact: true })).toBeVisible();
  await page.goBack();
  await expect(page).toHaveURL(`/boards/${board.id}/admin/card-types`);
  await expect(page.getByRole('button', { name: 'Edit card type Retained type', exact: true })).toBeVisible();
  expect(catalogueLoads).toBe(1);
});

test('default selection confirms before saving and keeps failures in its dialog for retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card type default dialog');
  const feature = await api.createCardType(board.id, 'Feature');
  await api.createCardType(board.id, 'Bug');
  await page.goto(`/boards/${board.id}/admin/card-types`);
  const row = page.getByRole('article', { name: 'Feature', exact: true });
  const originalDefault = page.getByRole('article').filter({ has: page.getByText('Default', { exact: true }) });
  const originalDefaultName = await originalDefault.getAttribute('aria-label');
  const dialog = page.getByRole('dialog', { name: 'Set default card type', exact: true });
  const message = 'The default card type could not be changed. Please try again.';
  let submissions = 0;
  page.on('request', request => {
    if (request.method() === 'PATCH' && request.url().endsWith(`/card-types/${feature.id}/default`)) {
      submissions++;
    }
  });
  const open = async () => {
    await row.getByRole('button', { name: 'Card type actions', exact: true }).click();
    await page.getByRole('button', { name: 'Set as default', exact: true }).click();
    await expect(dialog).toBeVisible();
  };

  await open();
  await expect(dialog).toContainText('Set Feature as the default card type?');
  await page.screenshot({ path: test.info().outputPath('default-card-type-dialog.png'), fullPage: true });
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(dialog).toBeHidden();
  expect(submissions).toBe(0);
  await expect(originalDefault).toHaveAttribute('aria-label', originalDefaultName!);

  await open();
  await rejectNextCardTypeDefault(page, board.id, feature.id);
  await dialog.getByRole('button', { name: 'OK', exact: true }).click();
  await expect(dialog.getByRole('alert')).toHaveText(message);
  await expect(dialog.getByRole('alert')).toBeFocused();
  await expect(page.getByText(message, { exact: true })).toHaveCount(1);
  await expect(row.getByText('Default', { exact: true })).toHaveCount(0);
  await page.screenshot({ path: test.info().outputPath('default-card-type-dialog-failure.png'), fullPage: true });
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(page.getByText(message, { exact: true })).toHaveCount(0);
  await expect(originalDefault).toHaveAttribute('aria-label', originalDefaultName!);

  await open();
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await rejectNextCardTypeDefault(page, board.id, feature.id);
  await dialog.getByRole('button', { name: 'OK', exact: true }).click();
  await expect(dialog.getByRole('alert')).toHaveText(message);
  await dialog.getByRole('button', { name: 'OK', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(row.getByText('Default', { exact: true })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});

test('custom controls retain their drafts, show mapped errors and update the live preview', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Compound card type form');
  await page.goto(`/boards/${board.id}/admin/card-types/new`);
  const dialog = page.getByRole('dialog', { name: 'Add Card Type', exact: true });
  await dialog.getByRole('textbox', { name: 'Name', exact: true }).fill('Compound type');
  const emoji = dialog.getByRole('group', { name: 'Emoji', exact: true });
  const style = dialog.getByRole('group', { name: 'Style', exact: true });
  const emojiTrigger = emoji.getByRole('button', { name: 'Emoji', exact: true });
  await emojiTrigger.click();
  const picker = dialog.getByRole('dialog', { name: 'Emoji picker', exact: true });
  await picker.getByRole('menuitem').first().click();
  await expect(picker).toBeHidden();
  await expect(emojiTrigger).not.toHaveText('Select emoji');
  const selectedEmoji = (await emojiTrigger.textContent())!.trim();
  const preview = dialog.getByRole('article', { name: 'Card type preview', exact: true });
  await expect(preview).toContainText(`${selectedEmoji} Compound type`);

  await style.getByRole('combobox', { name: 'Style', exact: true }).selectOption('gradient');
  await style.getByLabel('Left Color', { exact: true }).fill('#112233');
  await style.getByLabel('Right Color', { exact: true }).fill('#445566');
  await style.getByRole('combobox', { name: 'Text Color Mode', exact: true }).selectOption('custom');
  await style.getByLabel('Text Color', { exact: true }).fill('#ffffff');
  await style.getByRole('combobox', { name: 'Border', exact: true }).selectOption('custom');
  await style.getByLabel('Border Color', { exact: true }).fill('#aabbcc');
  await expect(preview).toHaveCSS('background-image', /rgb\(17, 34, 51\)/);
  await expect(preview).toHaveCSS('color', 'rgb(255, 255, 255)');

  await rejectNextCardTypeSave(page, board.id, { message: 'Invalid appearance', validationErrors: {
    emoji: ['Choose another emoji.'], styleName: ['Choose another style.'], stylePropertiesJson: ['Change the colours.']
  } });
  await dialog.getByRole('button', { name: 'Create card type', exact: true }).click();
  await expect(emoji).toHaveAccessibleDescription('Choose another emoji.');
  await expect(emoji).toBeFocused();
  await expect(style).toHaveAccessibleDescription('Choose another style. Change the colours.');
  await expect(dialog.getByRole('alert')).toHaveCount(2);
  await expect(style.getByLabel('Left Color', { exact: true })).toHaveValue('#112233');
  await page.screenshot({ path: test.info().outputPath('card-type-custom-validation.png') });

  await emojiTrigger.click();
  await picker.getByRole('button', { name: 'Clear', exact: true }).click();
  await expect(emoji).not.toHaveAttribute('aria-invalid', 'true');
  await expect(style).toHaveAttribute('aria-invalid', 'true');
  await style.getByLabel('Left Color', { exact: true }).fill('#223344');
  await expect(style).not.toHaveAttribute('aria-invalid', 'true');
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await expect(preview).toHaveCSS('background-image', /rgb\(34, 51, 68\)/);
  await dialog.getByRole('button', { name: 'Create card type', exact: true }).click();
  await expect(dialog).toBeHidden();
  await page.getByRole('button', { name: 'Edit card type Compound type', exact: true }).click();
  const edit = page.getByRole('dialog', { name: 'Edit Card Type: Compound type', exact: true });
  await expect(edit.getByLabel('Left Color', { exact: true })).toHaveValue('#223344');
  await expect(edit.getByLabel('Right Color', { exact: true })).toHaveValue('#445566');
  await expect(edit.getByLabel('Text Color', { exact: true })).toHaveValue('#ffffff');
  await expect(edit.getByLabel('Border Color', { exact: true })).toHaveValue('#aabbcc');
  await expect(edit.getByRole('button', { name: 'Emoji', exact: true })).toHaveText('Select emoji');
  await edit.getByRole('combobox', { name: 'Style', exact: true }).selectOption('presets');
  await edit.getByRole('button', { name: 'Preset 4', exact: true }).click();
  await edit.getByRole('button', { name: 'Save card type', exact: true }).click();
  await expect(edit).toBeHidden();
  await page.getByRole('button', { name: 'Edit card type Compound type', exact: true }).click();
  await expect(edit.getByRole('button', { name: 'Preset 4', exact: true })).toHaveAttribute('aria-pressed', 'true');
});

test('a failed delete keeps the editor draft and allows a confirmed retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card type delete failure');
  const cardType = await api.createCardType(board.id, 'Delete later');
  await page.goto(`/boards/${board.id}/admin/card-types/${cardType.id}`);
  const editor = page.getByRole('dialog', { name: 'Edit Card Type: Delete later', exact: true });
  await editor.getByRole('textbox', { name: 'Name', exact: true }).fill('');
  await rejectNextCardTypeDelete(page, board.id, cardType.id);
  await editor.getByRole('button', { name: 'Delete card type', exact: true }).click();
  const confirmation = page.getByRole('dialog', { name: 'Delete card type', exact: true });
  await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(editor.getByRole('alert')).toHaveText('This card type cannot be deleted yet.');
  await expect(editor.getByRole('alert')).toBeFocused();
  await expect(editor.getByRole('textbox', { name: 'Name', exact: true })).toHaveValue('');
  await editor.getByRole('button', { name: 'Delete card type', exact: true }).click();
  await confirmation.getByRole('button', { name: 'Delete', exact: true }).click();
  await expect(editor).toBeHidden();
  await expect(page.getByRole('button', { name: 'Edit card type Delete later', exact: true })).toHaveCount(0);
});
