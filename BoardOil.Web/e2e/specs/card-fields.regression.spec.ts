import { authenticatePage, expect, getBaseUrl, test } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';
import { ArchivedCardsPage } from '../ui/ArchivedCardsPage';
import { BoardPage } from '../ui/BoardPage';
import type { CustomFieldDefinition } from '../../src/shared/types/customFieldTypes';

const definitions: CustomFieldDefinition[] = [
  { dataType: 'text', code: 'Reference', name: 'Reference', emoji: null, nullable: true, showOnCard: false, defaultValue: 'Ready', minLength: null, maxLength: 250 },
  { dataType: 'integer', code: 'Points', name: 'Estimate', emoji: '🎯', nullable: true, showOnCard: true, defaultValue: 0, min: 0, max: 10 },
  { dataType: 'date', code: 'Due', name: 'Due date', emoji: null, nullable: true, showOnCard: true, defaultValue: '2026-10-04' }
];

test('card fields save, reload and render on board cards, with inline backend errors', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Card values');
  await api.configureDefaultFields(board.id, definitions);
  const card = await api.createCard(board, 'Todo', 'Field values');
  const editor = new CardEditorPage(page);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await expect(boardPage.card('Todo', card.title).getByText('0', { exact: true })).toBeVisible();
  await expect(boardPage.card('Todo', card.title).getByText('Ready', { exact: true })).toHaveCount(0);
  await editor.open(board.id, card.id);
  await expect(editor.field('Estimate')).toHaveValue('0');
  await expect(editor.field('Due date')).toHaveValue('2026-10-04');
  await editor.field('Reference').fill('Draft reference');
  await editor.field('Estimate').fill('1e-400');
  await editor.submit();
  await expect(editor.field('Estimate')).toHaveAccessibleDescription('Whole number required');
  await expect(editor.field('Estimate')).toHaveValue('1e-400');
  await expect(editor.field('Reference')).toHaveValue('Draft reference');
  await page.screenshot({ path: testInfo.outputPath('card-fields-validation.png') });
  await editor.field('Estimate').fill('7');
  await editor.field('Due date').fill('2026-12-25');
  await editor.saveCard();
  expect((await api.getCard(board.id, card.id)).customFieldValues).toEqual({ Reference: 'Draft reference', Points: 7, Due: '2026-12-25' });
  await expect(boardPage.card('Todo', card.title).getByText('7', { exact: true })).toBeVisible();
  await editor.open(board.id, card.id);
  await expect(editor.field('Reference')).toHaveValue('Draft reference');
  await expect(editor.field('Due date')).toHaveValue('2026-12-25');
  await page.setViewportSize({ width: 390, height: 844 });
  await editor.field('Due date').scrollIntoViewIfNeeded();
  await page.screenshot({ path: testInfo.outputPath('card-fields-mobile.png') });
  await editor.field('Reference').fill('');
  await editor.field('Estimate').fill('');
  await editor.field('Due date').fill('');
  await editor.saveCard();
  expect((await api.getCard(board.id, card.id)).customFieldValues).toEqual({ Reference: null, Points: null, Due: null });
});

test('type changes preview edited values, cancel intact and convert on confirmation', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Convert edited fields');
  const source = await api.configureDefaultFields(board.id, definitions);
  const target = await api.createCardType(board.id, 'Delivery', [
    { dataType: 'text', code: 'Effort', name: 'Estimate', emoji: null, nullable: true, showOnCard: true, defaultValue: null, minLength: null, maxLength: 250 }
  ]);
  const card = await api.createCard(board, 'Todo', 'Convert this card');
  const editor = new CardEditorPage(page);
  await editor.open(board.id, card.id);
  await editor.field('Reference').fill('Edited loss');
  await editor.field('Estimate').fill('invalid');
  await editor.selectType(target.name);
  await editor.submit();
  await expect(editor.field('Estimate')).toHaveAccessibleDescription('Whole number required');
  await expect(editor.typeChangePreview()).toBeHidden();
  await editor.field('Estimate').fill('7');
  await expect(editor.field('Reference')).toHaveValue('Edited loss');
  await editor.selectType(source.name);
  await editor.selectType(target.name);
  await expect(editor.field('Estimate')).toHaveValue('7');
  await editor.field('Estimate').press('Control+s');
  await expect(editor.typeChangePreview()).toBeVisible();
  await expect(editor.typeChangeRow('Estimate').getByRole('cell')).toHaveText(['🎯 EstimateNumber7', ' EstimateText7']);
  await expect(editor.typeChangeRow('Reference')).toContainText('Edited loss');
  await expect(editor.typeChangeRow('Reference')).toContainText('Removed · saved to comments');
  expect((await api.getCard(board.id, card.id)).cardTypeId).toBe(source.id);
  await page.screenshot({ path: testInfo.outputPath('type-change-preview.png') });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: testInfo.outputPath('type-change-preview-mobile.png') });
  await editor.cancelTypeChange();
  await expect(editor.field('Reference')).toHaveValue('Edited loss');
  await expect(editor.field('Estimate')).toHaveValue('7');
  await page.setViewportSize({ width: 1280, height: 900 });
  let releaseSave!: () => void;
  const saveGate = new Promise<void>(resolve => { releaseSave = resolve; });
  await page.route(`**/api/boards/${board.id}/cards/${card.id}`, async route => {
    await saveGate;
    await route.continue();
  }, { times: 1 });
  try {
    await editor.field('Estimate').press('Control+s');
    await editor.confirmTypeChange();
    await expect(page.getByRole('status').filter({ hasText: 'Saving…' })).toBeVisible();
    await expect(page.getByRole('dialog').locator('form')).toHaveAttribute('inert', '');
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeVisible();
    await page.keyboard.type('Must not change the draft');
  } finally {
    releaseSave();
  }
  await expect(page.getByRole('dialog')).toHaveAttribute('aria-busy', 'false');
  await expect(editor.field('Reference')).toHaveCount(0);
  await expect(editor.field('Estimate')).toHaveValue('7');
  const saved = await api.getCard(board.id, card.id);
  expect(saved.cardTypeId).toBe(target.id);
  expect(saved.customFieldValues).toEqual({ Effort: '7' });
  expect(saved.description).toBe(card.description);
  await expect(editor.commentContent()).toContainText('Reference: Edited loss');
  await expect(page.getByLabel('System comment', { exact: true })).toBeVisible();
  await editor.field('Estimate').fill('After conversion');
  await editor.saveCard();
  expect((await api.getCard(board.id, card.id)).customFieldValues).toEqual({ Effort: 'After conversion' });
  expect((await api.getCard(board.id, card.id)).description).toBe(saved.description);
});

test('discarding changes through browser history closes the previous card type preview', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Discard type preview');
  await api.configureDefaultFields(board.id, definitions);
  const target = await api.createCardType(board.id, 'No fields', []);
  const first = await api.createCard(board, 'Todo', 'First card');
  const second = await api.createCard(board, 'Todo', 'Second card');
  const editor = new CardEditorPage(page);
  await editor.open(board.id, first.id);
  await editor.cancelEditing();
  await new BoardPage(page).openCard('Todo', second.title);
  await expect(page).toHaveURL(new RegExp(`/card/${second.id}$`));
  await editor.field('Reference').fill('Discard this value');
  await editor.selectType(target.name);
  await editor.submit();
  await expect(editor.typeChangePreview()).toBeVisible();

  // Select the first card from browser history, skipping the intervening board entry.
  await page.evaluate(() => history.go(-2));
  await editor.discardUnsavedChanges();

  await expect(page).toHaveURL(new RegExp(`/card/${first.id}$`));
  await expect(editor.typeChangePreview()).toBeHidden();
  await expect(editor.field('Reference')).toHaveValue('Ready');
  const unchanged = await api.getCard(board.id, second.id);
  expect(unchanged.cardTypeId).toBe(second.cardTypeId);
  expect(unchanged.customFieldValues).toEqual(second.customFieldValues);
});

test('keyboard save rejects an incomplete date without clearing the saved value', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Incomplete date');
  await api.configureDefaultFields(board.id, definitions);
  const card = await api.createCard(board, 'Todo', 'Keep the date');
  const editor = new CardEditorPage(page);
  await editor.open(board.id, card.id);
  const date = editor.field('Due date');
  await date.focus();
  await date.press('ArrowRight');
  await date.press('Backspace');
  expect(await date.evaluate((input: HTMLInputElement) => input.validity.badInput)).toBe(true);
  await date.evaluate(input => input.addEventListener('invalid', () => input.setAttribute('data-invalid-reported', 'true')));
  await date.press('Control+s');
  await expect(date).toHaveAttribute('data-invalid-reported', 'true');
  expect((await api.getCard(board.id, card.id)).customFieldValues.Due).toBe('2026-10-04');
  await expect(date).toBeFocused();
  await date.fill('2026-12-25');
  await date.press('Control+s');
  await expect(page.getByRole('status').filter({ hasText: 'Saved successfully.' })).toBeVisible();
  expect((await api.getCard(board.id, card.id)).customFieldValues.Due).toBe('2026-12-25');
});

test('realtime updates preserve the open field draft and stale definitions fail on save', async ({ api, browser, authenticatedPage: page }) => {
  const board = await api.createBoard('Realtime field drafts');
  await api.configureDefaultFields(board.id, definitions);
  const card = await api.createCard(board, 'Todo', 'Shared fields');
  const context = await browser.newContext({ baseURL: getBaseUrl() });
  try {
    const remote = await context.newPage();
    await authenticatePage(remote);
    const observer = new BoardPage(page);
    await observer.open(board.id);
    const remoteEditor = new CardEditorPage(remote);
    await remoteEditor.open(board.id, card.id);
    await remoteEditor.field('Estimate').fill('5');
    await remoteEditor.saveCard();
    await expect(observer.card('Todo', card.title).getByText('5', { exact: true })).toBeVisible();
    const editor = new CardEditorPage(page);
    await editor.open(board.id, card.id);
    await editor.field('Reference').fill('Local draft');
    await api.configureDefaultFields(board.id, [...definitions,
      { dataType: 'text', code: 'Added', name: 'Added later', emoji: null, nullable: true, showOnCard: true, defaultValue: null, minLength: null, maxLength: 250 }
    ]);
    await editor.submit();
    await expect(page.getByRole('alert')).toContainText('Field must be supplied');
    await expect(editor.field('Reference')).toHaveValue('Local draft');
    await expect(editor.field('Estimate')).toHaveValue('5');
    expect((await api.getCard(board.id, card.id)).customFieldValues.Reference).toBe('Ready');
  } finally {
    await context.close();
  }
});

for (const changeType of [false, true]) {
  test(`duplicate drafts preserve fields and source values${changeType ? ' through type conversion' : ''}`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard('Duplicate custom fields');
    await api.configureDefaultFields(board.id, definitions);
    const target = await api.createCardType(board.id, 'Delivery', [
      { dataType: 'text', code: 'Effort', name: 'Estimate', emoji: null, nullable: true, showOnCard: true, defaultValue: null, minLength: null, maxLength: 250 }
    ]);
    const source = await api.createCard(board, 'Todo', 'Source fields');
    const editor = new CardEditorPage(page);
    await editor.open(board.id, source.id);
    await page.getByRole('button', { name: 'Card actions' }).click();
    await page.getByRole('menu', { name: 'Card actions' }).getByRole('button', { name: 'Duplicate', exact: true }).click();
    await expect(editor.field('Reference')).toHaveValue('Ready');
    await expect(editor.field('Estimate')).toHaveValue('0');
    await expect(editor.field('Due date')).toHaveValue('2026-10-04');
    await editor.field('Reference').fill('Edited duplicate');
    await editor.field('Estimate').fill('invalid');
    const create = page.getByRole('button', { name: 'Create duplicate card', exact: true });
    await create.click();
    await expect(editor.field('Estimate')).toHaveAccessibleDescription('Whole number required');
    await expect(editor.field('Reference')).toHaveValue('Edited duplicate');
    await editor.field('Estimate').fill('7');
    await editor.field('Due date').fill('');
    if (changeType) await editor.selectType(target.name);
    const response = page.waitForResponse(res => res.url().endsWith(`/cards/${source.id}/duplicate`) && res.request().method() === 'POST');
    await create.click();
    if (changeType) await editor.confirmTypeChange();
    const duplicate = (await (await response).json()).data;
    await expect(page.getByRole('dialog')).toBeHidden();
    expect((await api.getCard(board.id, source.id)).customFieldValues).toEqual({ Reference: 'Ready', Points: 0, Due: '2026-10-04' });
    const saved = await api.getCard(board.id, duplicate.id);
    if (changeType) {
      expect(saved.customFieldValues).toEqual({ Effort: '7' });
      expect(saved.description).toBe(source.description);
    } else {
      expect(saved.customFieldValues).toEqual({ Reference: 'Edited duplicate', Points: 7, Due: null });
    }
    await editor.open(board.id, duplicate.id);
    if (changeType) await expect(editor.commentContent()).toContainText('Reference: Edited duplicate');
    await expect(editor.field('Estimate')).toHaveValue('7');
  });
}


test('archived fields retain their labels and convert on restore', async ({ api, authenticatedPage: page }, testInfo) => {
  const board = await api.createBoard('Archive custom fields');
  await api.configureDefaultFields(board.id, definitions);
  const card = await api.createCard(board, 'Todo', 'Captured fields');
  await api.archiveCard(board.id, card.id);
  await api.configureDefaultFields(board.id, [
    { ...definitions[1]!, name: 'Renamed estimate' }
  ]);
  const boardPage = new BoardPage(page);
  const archive = new ArchivedCardsPage(page);
  await boardPage.open(board.id);
  await boardPage.openArchivedCards();
  await archive.openCard(card.title);
  const fields = page.getByRole('region', { name: 'Custom Fields', exact: true });
  await expect(fields).toContainText('Reference');
  await expect(fields).toContainText('Ready');
  await expect(fields).toContainText('Estimate');
  await expect(fields.getByText('0', { exact: true })).toBeVisible();
  await expect(fields).toContainText('2026-10-04');
  await expect(fields).not.toContainText('Renamed estimate');
  await page.screenshot({ path: testInfo.outputPath('archived-custom-fields.png') });
  await archive.unarchiveOpenCard(card.title);
  expect((await api.getCard(board.id, card.id)).customFieldValues).toEqual({ Points: 0 });
  expect((await api.getCard(board.id, card.id)).description).toBe(card.description);
  await archive.goBackToBoard();
  const editor = new CardEditorPage(page);
  await editor.open(board.id, card.id);
  await expect(editor.field('Renamed estimate')).toHaveValue('0');
  await expect(editor.commentContent()).toContainText('Reference: Ready');
});

test('equivalent fields save directly when only codes and presentation differ', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Equivalent fields');
  await api.configureDefaultFields(board.id, definitions);
  const target = await api.createCardType(board.id, 'Equivalent', definitions.map(field => ({
    ...field, code: `New${field.code}`, emoji: null, showOnCard: !field.showOnCard
  })).reverse());
  const card = await api.createCard(board, 'Todo', 'Skip confirmation');
  const editor = new CardEditorPage(page);
  await editor.open(board.id, card.id);
  await editor.field('Estimate').fill('5');
  await editor.selectType(target.name);
  await editor.saveCard();
  await expect(editor.typeChangePreview()).toBeHidden();
  expect((await api.getCard(board.id, card.id)).customFieldValues).toEqual({ NewReference: 'Ready', NewPoints: 5, NewDue: '2026-10-04' });
});
