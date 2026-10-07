import { expect, test } from '../fixtures/boardOilTest';
import { CardTypeEditorPage } from '../ui/CardTypeEditorPage';

test('duplicate a default card type with all settings and backend name validation', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Duplicate type');
  const source = await api.configureDefaultFields(board.id, [
    { type: 'text', fieldCode: 'Ref', displayName: 'Reference', emoji: '📎', nullable: true,
      showOnCard: true, defaultValue: 'Ready', minLength: 2, maxLength: 80 },
    { type: 'integer', fieldCode: 'Points', displayName: 'Estimate', emoji: null, nullable: false,
      showOnCard: false, defaultValue: 0, min: 0, max: 20 },
    { type: 'date', fieldCode: 'Due', displayName: 'Due date', emoji: '📅', nullable: true,
      showOnCard: true, defaultValue: '2026-10-07' }
  ], { emoji: '📦', styleName: 'presets', stylePropertiesJson: '{"presetIndex":2}' });
  const card = await api.createCard(board, 'Todo', 'Original card');
  const editor = new CardTypeEditorPage(page);
  await editor.duplicate(board.id, source.name);
  const name = page.getByRole('textbox', { name: 'Name', exact: true });
  await expect(name).toHaveValue('');
  await expect(name).toBeFocused();
  await expect(page.getByRole('button', { name: 'Delete card type', exact: true })).toHaveCount(0);
  await name.fill(` ${source.name.toLowerCase()} `);
  await editor.submit(true);
  await expect(name).toHaveAccessibleDescription('Name already used');
  await expect(page.getByRole('status', { name: 'Field change warnings' })).toHaveCount(0);
  await name.fill('Delivery');
  await editor.save(true);

  const types = await api.getCardTypes(board.id);
  const copy = types.find(type => type.name === 'Delivery')!;
  expect(copy.id).not.toBe(source.id);
  expect(copy).toMatchObject({ isSystem: false, emoji: source.emoji, styleName: source.styleName,
    stylePropertiesJson: source.stylePropertiesJson, fields: source.fields });
  expect(types.find(type => type.id === source.id)).toEqual(source);
  expect((await api.getCard(board.id, card.id)).cardTypeId).toBe(source.id);
});

test('duplicate drafts can be edited or cancelled independently of their source', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Independent duplicate');
  const source = await api.createCardType(board.id, 'Original', [
    { type: 'text', fieldCode: 'Ref', displayName: 'Reference', emoji: null, nullable: true,
      showOnCard: false, defaultValue: 'Ready', minLength: null, maxLength: 80 }
  ]);
  const editor = new CardTypeEditorPage(page);
  await editor.duplicate(board.id, source.name);
  await editor.expandField('Reference');
  await expect(page.getByLabel('Field code', { exact: true })).not.toHaveAttribute('readonly');
  await page.getByLabel('Display name', { exact: true }).fill('Customer reference');
  await expect(page.getByLabel('Field code', { exact: true })).toHaveValue('Ref');
  await page.getByLabel('Field code', { exact: true }).fill('CustomerRef');
  await page.getByLabel('Default value', { exact: true }).fill('Changed');
  await editor.cancel();
  expect((await api.getCardTypes(board.id)).find(type => type.id === source.id)).toEqual(source);
  expect((await api.getCardTypes(board.id))).toHaveLength(2);

  await editor.duplicate(board.id, source.name);
  await editor.expandField('Reference');
  await expect(page.getByLabel('Field code', { exact: true })).toHaveValue('Ref');
  await expect(page.getByLabel('Default value', { exact: true })).toHaveValue('Ready');
  await page.getByLabel('Name', { exact: true }).fill('Copy');
  await page.getByLabel('Field code', { exact: true }).fill('CopiedRef');
  await editor.save(true);
  const types = await api.getCardTypes(board.id);
  expect(types.find(type => type.name === 'Copy')?.fields).toEqual([{ ...source.fields[0], fieldCode: 'CopiedRef' }]);
  expect(types.find(type => type.id === source.id)).toEqual(source);
});

test('configure fields, reorder, reopen and cancel removal without changing saved definitions', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Field editor');
  const editor = new CardTypeEditorPage(page);
  await editor.open(board.id);
  await page.getByLabel('Name', { exact: true }).fill('Delivery');
  await editor.addField('Reference', 'Reference', 'text', 'Ready');
  await page.getByLabel('Maximum length', { exact: true }).fill('80');
  await page.getByLabel('Show on cards', { exact: true }).check();
  await page.getByRole('region', { name: 'Custom Fields', exact: true }).getByRole('button', { name: 'Emoji', exact: true }).click();
  const picker = page.getByRole('dialog', { name: 'Emoji picker', exact: true });
  await expect(picker).toBeVisible();
  const bounds = await picker.boundingBox();
  expect(bounds!.x).toBeGreaterThanOrEqual(0);
  expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(page.viewportSize()!.width);
  await picker.getByRole('menuitem').first().click();
  await expect(picker).toBeHidden();
  await editor.addField('Estimate', 'Points', 'integer', '0');
  await page.getByLabel('Required', { exact: true }).check();
  await page.getByLabel('Minimum value', { exact: true }).fill('0');
  await page.getByLabel('Maximum value', { exact: true }).fill('20');
  await editor.addField('Due date', 'Due', 'date', '2026-10-04');
  await page.getByRole('button', { name: 'Move Due date up', exact: true }).click();
  await editor.save(true);

  const saved = (await api.getCardTypes(board.id)).find(type => type.name === 'Delivery')!;
  expect(saved.fields.map(field => field.fieldCode)).toEqual(['Reference', 'Due', 'Points']);
  expect(saved.fields[0].emoji).toBeTruthy();
  expect(saved.fields[2]).toMatchObject({ type: 'integer', nullable: false, defaultValue: 0, min: 0, max: 20 });
  await editor.open(board.id, saved.id);
  await editor.expandField('Reference');
  await expect(page.getByLabel('Field code', { exact: true })).toHaveAttribute('readonly');
  await expect(page.getByLabel('Maximum length', { exact: true })).toHaveValue('80');
  await expect(page.getByLabel('Show on cards', { exact: true })).toBeChecked();
  await page.screenshot({ path: testInfo.outputPath('fields-desktop.png') });

  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: testInfo.outputPath('fields-mobile.png') });
  expect(await page.getByRole('dialog').evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
  await editor.removeField('Reference');
  await editor.cancel();
  expect((await api.getCardTypes(board.id)).find(type => type.id === saved.id)?.fields).toEqual(saved.fields);

  await editor.open(board.id, saved.id);
  await editor.expandField('Due date');
  await expect(page.getByLabel('Default value', { exact: true })).toHaveValue('2026-10-04');
  await editor.removeField('Due date');
  await editor.save();
  expect((await api.getCardTypes(board.id)).find(type => type.id === saved.id)?.fields.map(field => field.fieldCode)).toEqual(['Reference', 'Points']);
});

test('definition changes preview losses and preserve replaced values in comments', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Field impact');
  const type = await api.configureDefaultFields(board.id, [{
    type: 'integer', fieldCode: 'Points', displayName: 'Estimate', emoji: null,
    nullable: false, showOnCard: true, defaultValue: 10, min: 0, max: 20
  }, {
    type: 'date', fieldCode: 'Due', displayName: 'Due Date', emoji: null,
    nullable: true, showOnCard: false, defaultValue: '2026-10-04'
  }]);
  const card = await api.createCard(board, 'Todo', 'Existing estimate');
  const editor = new CardTypeEditorPage(page);
  await editor.open(board.id, type.id);
  const warning = page.getByRole('status', { name: 'Field change warnings' });
  await expect(warning).toHaveCount(0);
  await editor.expandField('Estimate');
  await page.getByLabel('Default value (required)', { exact: true }).fill('0');
  await page.getByLabel('Maximum value', { exact: true }).fill('5');
  await expect(warning).toContainText("Changes to 'Estimate' will replace values on 1 card.");
  expect((await api.getCard(board.id, card.id)).fields.Points).toBe(10);
  await editor.removeField('Due Date');
  await expect(warning).toContainText("Deleted field 'Due Date' will be removed from 1 card.");
  await expect(warning).toContainText('Removed values will be saved to comments.');
  await page.screenshot({ path: testInfo.outputPath('field-change-warnings.png') });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(warning).toBeVisible();
  await expect.poll(async () => (await page.getByRole('dialog').boundingBox())!.width).toBeLessThanOrEqual(390);
  expect(await page.getByRole('dialog').evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
  await page.screenshot({ path: testInfo.outputPath('field-change-warnings-mobile.png') });
  await editor.save();
  expect((await api.getCard(board.id, card.id)).fields).toEqual({ Points: 0 });
  await page.goto(`/boards/${board.id}/card/${card.id}`);
  const comment = page.getByRole('article').filter({ hasText: 'Estimate: 10' });
  await expect(comment).toContainText('Due Date: 2026-10-04');
  await expect(comment.getByLabel('System comment', { exact: true })).toBeVisible();
});

test('required defaults are validated and new fields stop at ten', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Required fields');
  const editor = new CardTypeEditorPage(page);
  await editor.open(board.id);
  await page.getByLabel('Name', { exact: true }).fill('Checklist');
  await editor.addField('Owner', 'Owner', 'text');
  await page.getByLabel('Required', { exact: true }).check();
  await editor.submit(true);
  await expect(page.getByLabel('Default value (required)', { exact: true })).toHaveAccessibleDescription('Required');
  await expect(page.getByRole('alert')).toHaveCount(0);
  await expect(page.getByLabel('Default value (required)', { exact: true })).toBeFocused();
  await page.getByLabel('Default value (required)', { exact: true }).fill('Unassigned');
  for (let index = 2; index <= 10; index++) await editor.addField(`Field ${index}`, `Field${index}`, 'text');
  await expect(page.getByRole('button', { name: 'Add field', exact: true })).toBeDisabled();
  await editor.save(true);
});

test('invalid numbers retain backend errors on untouched fields without a page banner', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Number validation');
  const editor = new CardTypeEditorPage(page);
  await editor.open(board.id);
  await page.getByLabel('Name', { exact: true }).fill('Estimate');
  await editor.addField('Points', 'Points', 'integer', '1e-400');
  await editor.addField('Effort', 'Effort', 'integer', 'abc');
  const responsePromise = page.waitForResponse(response => response.request().method() === 'POST'
    && response.url().endsWith(`/api/boards/${board.id}/card-types`));
  await editor.submit(true);
  const response = await responsePromise;
  expect(response.status()).toBe(400);
  const payload = await response.json();
  expect(payload.validationErrors['fields[0].defaultValue']).toEqual(['Whole number required']);
  const points = page.getByRole('region', { name: 'Points Number · Optional', exact: true });
  await expect(points.getByLabel('Default value', { exact: true })).toHaveAccessibleDescription('Whole number required');
  await expect(points.getByLabel('Default value', { exact: true })).toHaveValue('1e-400');
  await expect(page.getByText('Validation failed.', { exact: true })).toHaveCount(0);
  await page.getByLabel('Name', { exact: true }).fill('Estimates');
  await expect(points.getByLabel('Default value', { exact: true })).toHaveAccessibleDescription('Whole number required');
  await points.getByLabel('Default value', { exact: true }).fill('0');
  await expect(points.getByLabel('Default value', { exact: true })).not.toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('button', { name: 'Effort Number · Optional Validation errors', exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Move Effort up', exact: true }).click();
  await editor.expandField('Effort');
  const effort = page.getByRole('region', { name: 'Effort Number · Optional', exact: true });
  await expect(effort.getByLabel('Default value', { exact: true })).toHaveAccessibleDescription('Whole number required');
  await effort.getByLabel('Default value', { exact: true }).fill('0');
  await editor.save(true);
});
