import { expect, test } from '../fixtures/boardOilTest';
import { CardTypeEditorPage } from '../ui/CardTypeEditorPage';

test('duplicate a default card type with all settings and backend name validation', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Duplicate type');
  const source = await api.configureDefaultFields(board.id, [
    { dataType: 'text', code: 'Ref', name: 'Reference', emoji: '📎', nullable: true,
      showOnCard: true, defaultValue: 'Ready', minLength: 2, maxLength: 80 },
    { dataType: 'integer', code: 'Points', name: 'Estimate', emoji: null, nullable: false,
      showOnCard: false, defaultValue: 0, min: 0, max: 20 },
    { dataType: 'date', code: 'Due', name: 'Due date', emoji: '📅', nullable: true,
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
    stylePropertiesJson: source.stylePropertiesJson, customFields: source.customFields });
  expect(types.find(type => type.id === source.id)).toEqual(source);
  expect((await api.getCard(board.id, card.id)).cardTypeId).toBe(source.id);
});

test('duplicate type shares fields and retains the card preview', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Shared duplicate');
  const source = await api.createCardType(board.id, 'Original', [
    { dataType: 'text', code: 'Ref', name: 'Reference', emoji: null, nullable: true,
      showOnCard: true, defaultValue: 'Ready', minLength: null, maxLength: 80 }
  ]);
  const editor = new CardTypeEditorPage(page);
  await editor.duplicate(board.id, source.name);
  await expect(page.getByRole('article', { name: 'Card preview' })).toContainText('Ready');
  await expect(page.getByRole('link', { name: 'Manage custom fields' })).toBeVisible();
  await expect(page.getByLabel('Field code', { exact: true })).toHaveCount(0);
  await page.getByLabel('Name', { exact: true }).fill('Copy');
  await editor.save(true);
  const copy = (await api.getCardTypes(board.id)).find(type => type.name === 'Copy')!;
  expect(copy.customFields).toEqual(source.customFields);
  const catalogue = await api.getCustomFields(board.id);
  expect(catalogue).toHaveLength(1);
  expect(catalogue[0].appliesTo).toEqual({ mode: 'selected', cardTypeIds: [source.id, copy.id] });
});
