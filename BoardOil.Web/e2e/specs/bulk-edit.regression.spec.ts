import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { holdRejectedBulkEdit } from '../support/bulkEditSubmission';

test('bulk edits retain combined changes after a backend column rejection and apply them together on retry', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Bulk edit combined');
  const slick = await api.createSlick(board, 'Release');
  const first = await api.createCard(board, 'Todo', 'First', '', ['Remove me']);
  const second = await api.createCard(board, 'Todo', 'Second', '', ['Remove me']);
  const untouched = await api.createCard(board, 'Todo', 'Untouched', '', ['Add me']);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'First');
  await bp.selectCard('Todo', 'Second');
  const dialog = await bp.openBulkEdit();
  const apply = dialog.getByRole('button', { name: 'Apply edits', exact: true });
  await expect(apply).toBeDisabled();
  const column = dialog.getByRole('combobox', { name: 'Column', exact: true });
  const removedColumn = board.columns.find(item => item.title === 'In Progress')!;
  const destination = board.columns.find(item => item.title === 'Done')!;
  await column.selectOption({ label: removedColumn.title });
  await dialog.getByRole('combobox', { name: 'Slick', exact: true }).selectOption({ label: 'Set slick' });
  await dialog.getByRole('combobox', { name: 'Slick to set', exact: true }).selectOption({ label: slick.name });
  await dialog.getByRole('button', { name: 'Mark for add Add me', exact: true }).click();
  await dialog.getByRole('button', { name: 'Mark for remove Remove me', exact: true }).click();
  await api.deleteColumn(board.id, removedColumn.id);
  await apply.click();
  await expect(column).toHaveAccessibleDescription('Column does not exist in board.');
  await expect(column).toBeFocused();
  await expect(page.getByText('Column does not exist in board.', { exact: true })).toHaveCount(1);
  await expect(dialog.getByText('Edit 2 selected cards.', { exact: true })).toBeVisible();
  for (const card of [first, second]) {
    expect(await api.getCard(board.id, card.id)).toMatchObject({ boardColumnId: first.boardColumnId, tagNames: ['Remove me'], slickName: null });
  }
  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 900 });
    await page.screenshot({ path: test.info().outputPath(`bulk-edit-error-${width}.png`) });
  }
  await column.selectOption({ label: destination.title });
  await expect(column).not.toHaveAttribute('aria-invalid', 'true');
  await apply.click();
  await expect(dialog).toBeHidden();
  await expect(page.getByRole('checkbox', { name: 'Toggle card selection mode' })).not.toBeChecked();
  for (const card of [first, second]) {
    expect(await api.getCard(board.id, card.id)).toMatchObject({ boardColumnId: destination.id, tagNames: ['Add me'], slickName: slick.name });
  }
  expect(await api.getCard(board.id, untouched.id)).toMatchObject({ boardColumnId: untouched.boardColumnId, tagNames: ['Add me'], slickName: null });
});

test('bulk slick intent requires a choice and distinguishes clear from no change', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Bulk slick intent');
  const slick = await api.createSlick(board, 'Release');
  const card = await api.createCard(board, 'Todo', 'Selected', '', ['Keep me'], slick.name);
  await api.createTag(board.id, { name: 'Added' });
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'Selected');
  const dialog = await bp.openBulkEdit();
  const operation = dialog.getByRole('combobox', { name: 'Slick', exact: true });
  const apply = dialog.getByRole('button', { name: 'Apply edits', exact: true });
  await operation.selectOption({ label: 'Set slick' });
  await dialog.getByRole('button', { name: 'Mark for add Added', exact: true }).click();
  await apply.click();
  await expect(dialog.getByRole('combobox', { name: 'Slick to set', exact: true })).toHaveAccessibleDescription('Select a slick.');
  expect(await api.getCard(board.id, card.id)).toMatchObject({ tagNames: ['Keep me'], slickName: slick.name });
  await operation.selectOption({ label: 'No change' });
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await expect(dialog.getByRole('combobox', { name: 'Slick to set', exact: true })).toHaveCount(0);
  await apply.click();
  await expect(dialog).toBeHidden();
  expect(await api.getCard(board.id, card.id)).toMatchObject({ boardColumnId: card.boardColumnId, tagNames: ['Added', 'Keep me'], slickName: slick.name });

  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'Selected');
  await bp.openBulkEdit();
  await operation.selectOption({ label: 'No slick' });
  await apply.click();
  await expect(dialog).toBeHidden();
  expect(await api.getCard(board.id, card.id)).toMatchObject({ boardColumnId: card.boardColumnId, tagNames: ['Added', 'Keep me'], slickName: null });
});

test('bulk editing locks controls while saving and retains selection and draft after a general rejection', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Bulk retry');
  const card = await api.createCard(board, 'Todo', 'Selected');
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'Selected');
  const dialog = await bp.openBulkEdit();
  const column = dialog.getByRole('combobox', { name: 'Column', exact: true });
  const apply = dialog.getByRole('button', { name: 'Apply edits', exact: true });
  await column.selectOption({ label: 'In Progress' });
  const pending = await holdRejectedBulkEdit(page, board.id, 'Bulk editing is temporarily unavailable.');
  try {
    await apply.click();
    await pending.request;
    await expect(column).toBeDisabled();
    await expect(apply).toBeDisabled();
    await expect(dialog.getByRole('button', { name: 'Cancel', exact: true })).toBeDisabled();
    pending.release();
    await expect(dialog.getByRole('alert')).toHaveText('Bulk editing is temporarily unavailable.');
    await expect(page.getByText('Bulk editing is temporarily unavailable.', { exact: true })).toHaveCount(1);
    await expect(page.getByRole('checkbox', { name: 'Toggle card selection mode' })).toBeChecked();
    await expect(dialog.getByText('Edit 1 selected card.', { exact: true })).toBeVisible();
    await expect(column.getByRole('option', { selected: true })).toHaveText('In Progress');
    await apply.click();
    await expect(dialog).toBeHidden();
    expect(await api.getCard(board.id, card.id)).toMatchObject({ boardColumnId: board.columns.find(item => item.title === 'In Progress')!.id });
  } finally {
    pending.release();
  }
});

test('bulk editing follows live selection removal and cancellation discards the draft', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Bulk live selection');
  const first = await api.createCard(board, 'Todo', 'First');
  const second = await api.createCard(board, 'Todo', 'Second');
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'First');
  await bp.selectCard('Todo', 'Second');
  const dialog = await bp.openBulkEdit();
  await dialog.getByRole('combobox', { name: 'Column', exact: true }).selectOption({ label: 'In Progress' });
  await api.deleteCards(board.id, [first.id]);
  await expect(dialog.getByText('Edit 1 selected card.', { exact: true })).toBeVisible();
  await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(page.getByRole('checkbox', { name: 'Toggle card selection mode' })).toBeChecked();
  await bp.openBulkEdit();
  const apply = dialog.getByRole('button', { name: 'Apply edits', exact: true });
  await expect(apply).toBeDisabled();
  await dialog.getByRole('combobox', { name: 'Column', exact: true }).selectOption({ label: 'In Progress' });
  await api.deleteCards(board.id, [second.id]);
  await expect(dialog.getByText('Edit 0 selected cards.', { exact: true })).toBeVisible();
  await expect(apply).toBeDisabled();
  await expect(dialog.getByRole('button', { name: 'Cancel', exact: true })).toBeEnabled();
});

test('bulk tag choices follow tags created while an initially empty catalogue is open', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Bulk live tags');
  const card = await api.createCard(board, 'Todo', 'Selected');
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'Selected');
  const dialog = await bp.openBulkEdit();
  await expect(dialog.getByText('No tags available.', { exact: true })).toBeVisible();

  await api.createTag(board.id, { name: 'New tag' });
  await dialog.getByRole('button', { name: 'Mark for add New tag', exact: true }).click();
  await dialog.getByRole('button', { name: 'Apply edits', exact: true }).click();

  await expect(dialog).toBeHidden();
  expect(await api.getCard(board.id, card.id)).toMatchObject({ tagNames: ['New tag'] });
});
