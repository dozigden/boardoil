import { expect, test } from '../fixtures/boardOilTest';
import { CustomFieldEditorPage } from '../ui/CustomFieldEditorPage';

test('board field modals support defaults, ordering, applicability and mobile layout', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Board catalogue');
  const editor = new CustomFieldEditorPage(page);
  await editor.open(board.id);
  await editor.fill('Estimate', 'Points', 'integer', '0');
  await page.getByLabel('Required', { exact: true }).check();
  await page.getByLabel('Show on cards', { exact: true }).check();
  await page.getByLabel('Minimum value', { exact: true }).fill('0');
  await page.getByLabel('Maximum value', { exact: true }).fill('20');
  await expect(page.getByLabel('All cards', { exact: true })).toBeChecked();
  await page.screenshot({ path: testInfo.outputPath('custom-field-desktop.png') });
  await editor.save(true);
  const future = await api.createCardType(board.id, 'Future');
  expect(future.customFields.map(field => field.code)).toEqual(['Points']);
  await editor.open(board.id);
  await editor.fill('Due date', 'Due', 'date', '2026-10-07');
  await page.getByLabel('Selected card types', { exact: true }).check();
  await editor.save(true);
  await expect(page.getByText('Not assigned', { exact: true })).toBeVisible();
  const estimate = page.getByRole('row').filter({ has: page.getByRole('button', { name: 'Edit field Estimate', exact: true }) });
  await page.getByRole('button', { name: 'Drag to reorder Due date', exact: true }).dragTo(estimate);
  await expect.poll(async () => (await api.getCustomFields(board.id)).map(field => field.definition.code)).toEqual(['Due', 'Points']);
  await page.getByRole('button', { name: 'Edit field Estimate', exact: true }).click();
  const estimateField = (await api.getCustomFields(board.id)).find(field => field.definition.code === 'Points')!;
  await expect(page).toHaveURL(new RegExp(`/custom-fields/${estimateField.id}$`));
  await page.reload();
  await expect(page.getByLabel('Field code', { exact: true })).toHaveAttribute('readonly');
  await expect(page.getByLabel('Maximum value', { exact: true })).toHaveValue('20');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.getByLabel('Maximum value', { exact: true }).scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('custom-field-mobile.png') });
  const bounds = await page.getByRole('dialog').boundingBox();
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(390);
  await page.getByRole('button', { name: 'Cancel', exact: true }).last().click();
  await page.getByRole('button', { name: 'Remove Due date', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('0 populated values');
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  expect(await api.getCustomFields(board.id)).toHaveLength(2);
});

test('field edits retain invalid drafts, preview losses and preserve values in comments', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Field validation');
  await api.configureDefaultFields(board.id, [
    { dataType: 'text', code: 'Ref', name: 'Reference', emoji: null, nullable: true, showOnCard: true, defaultValue: 'Populated', minLength: null, maxLength: 80 },
    { dataType: 'text', code: 'Other', name: 'Other', emoji: null, nullable: true, showOnCard: false, defaultValue: null, minLength: null, maxLength: 80 }
  ]);
  const card = await api.createCard(board, 'Todo', 'Populated card');
  const editor = new CustomFieldEditorPage(page);
  const reference = (await api.getCustomFields(board.id)).find(field => field.definition.code === 'Ref')!;
  await editor.open(board.id, reference.id);
  await page.getByLabel('Display name', { exact: true }).fill('Other');
  await page.getByRole('button', { name: 'Save field' }).click();
  await expect(page.getByLabel('Display name', { exact: true })).toHaveAccessibleDescription('Name already used');
  await page.getByLabel('Display name', { exact: true }).fill('Reference');
  await page.getByRole('combobox', { name: 'Type', exact: true }).selectOption('integer');
  await page.getByLabel('Default value', { exact: true }).fill('1e-400');
  await page.getByRole('button', { name: 'Save field' }).click();
  await expect(page.getByLabel('Default value', { exact: true })).toHaveAccessibleDescription('Whole number required');
  await expect(page.getByLabel('Default value', { exact: true })).toHaveValue('1e-400');
  await page.getByLabel('Default value', { exact: true }).fill('2');
  await expect(page.getByRole('status')).toContainText('1 populated value will be replaced');
  await editor.save();
  expect((await api.getCard(board.id, card.id)).customFieldValues.Ref).toBe(2);
  await page.getByRole('button', { name: 'Remove Reference', exact: true }).click();
  await expect(page.getByRole('dialog')).toContainText('1 populated value');
  await page.getByRole('button', { name: 'Remove', exact: true }).click();
  await expect.poll(async () => (await api.getCustomFields(board.id)).map(field => field.definition.code)).toEqual(['Other']);
  expect((await api.getCard(board.id, card.id)).customFieldValues).toEqual({ Other: null });
});

test('an incomplete default date blocks submission without erasing the saved date', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Date field');
  await api.configureDefaultFields(board.id, [
    { dataType: 'date', code: 'Due', name: 'Due date', emoji: null, nullable: true, showOnCard: true, defaultValue: '2026-10-07' }
  ]);
  const editor = new CustomFieldEditorPage(page);
  const due = (await api.getCustomFields(board.id))[0];
  await editor.open(board.id, due.id);
  const date = page.getByLabel('Default value', { exact: true });
  await date.focus();
  await date.press('ArrowRight');
  await date.press('Backspace');
  expect(await date.evaluate((input: HTMLInputElement) => input.validity.badInput)).toBe(true);
  await page.getByRole('button', { name: 'Save field' }).click();
  await expect(page.getByRole('dialog')).toBeVisible();
  expect((await api.getCustomFields(board.id))[0].definition.defaultValue).toBe('2026-10-07');
  await date.fill('2026-12-25');
  await editor.save();
});

test('failed writes retain drafts and ordering, and drag ordering saves successfully', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Resilient catalogue');
  await api.configureDefaultFields(board.id, ['First', 'Second'].map(name => ({
    dataType: 'text' as const, code: name, name, emoji: null, nullable: true, showOnCard: false,
    defaultValue: null, minLength: null, maxLength: 80
  })));
  const editor = new CustomFieldEditorPage(page);
  const first = (await api.getCustomFields(board.id)).find(field => field.definition.code === 'First')!;
  await editor.open(board.id, first.id);
  await page.getByLabel('Display name', { exact: true }).fill('Renamed');
  await page.route(`**/api/boards/${board.id}/custom-fields/${first.id}`, route => route.fulfill({
    status: 500, contentType: 'application/json', body: JSON.stringify({ message: 'Please retry' })
  }), { times: 1 });
  await page.getByRole('button', { name: 'Save field' }).click();
  await expect(page.getByRole('alert')).toContainText('Please retry');
  await expect(page.getByLabel('Display name', { exact: true })).toHaveValue('Renamed');
  await editor.save();
  await page.route(`**/api/boards/${board.id}/custom-field-order`, route => route.fulfill({
    status: 500, contentType: 'application/json', body: JSON.stringify({ message: 'Order not saved' })
  }), { times: 1 });
  const renamed = page.getByRole('row').filter({ has: page.getByRole('button', { name: 'Edit field Renamed', exact: true }) });
  await page.getByRole('button', { name: 'Drag to reorder Second', exact: true }).dragTo(renamed);
  await expect(page.getByRole('alert')).toContainText('Order not saved');
  expect((await api.getCustomFields(board.id)).map(field => field.definition.code)).toEqual(['First', 'Second']);
  const second = page.getByRole('row').filter({ has: page.getByRole('button', { name: 'Edit field Second', exact: true }) });
  await page.getByRole('button', { name: 'Drag to reorder Renamed', exact: true }).dragTo(second);
  await expect.poll(async () => (await api.getCustomFields(board.id)).map(field => field.definition.code)).toEqual(['Second', 'First']);
});
