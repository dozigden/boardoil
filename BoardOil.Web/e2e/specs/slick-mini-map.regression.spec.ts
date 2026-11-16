import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';
import { holdCardSaveResponse } from '../support/cardSaveResponse';

test('slick mini map shows occupied columns, previews cards and guards navigation', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Slick mini map navigation');
  const slick = await api.createSlick(board, 'Shared work');
  const current = await api.createCard(board, 'Todo', 'Current card', '', [], slick.name);
  const next = await api.createCard(board, 'Todo', 'Next card', '', ['Preview tag'], slick.name);
  const finished = await api.createCard(board, 'Done', 'Finished card', '', [], slick.name);
  await api.createCard(board, 'In Progress', 'Unrelated card');
  await api.moveCard(board.id, next.id, current.boardColumnId, current.id);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await page.getByPlaceholder('Search', { exact: true }).fill(current.title);
  await boardPage.openCard('Todo', current.title);

  const map = page.getByRole('region', { name: 'Slick mini map', exact: true });
  const preview = page.getByRole('tooltip');
  const nextDot = map.getByRole('button', { name: `#${next.id}: ${next.title}`, exact: true });
  await expect(map.getByRole('group')).toHaveCount(2);
  await expect(map.getByRole('group').nth(0)).toHaveAccessibleName('Todo');
  await expect(map.getByRole('group').nth(1)).toHaveAccessibleName('Done');
  await expect(map.getByRole('button')).toHaveCount(3);
  await expect(map.getByRole('group', { name: 'Todo', exact: true }).getByRole('button').nth(0)).toHaveAccessibleName(`#${current.id}: ${current.title}`);
  await expect(map.getByRole('button', { name: `#${current.id}: ${current.title}`, exact: true })).toHaveAttribute('aria-current', 'true');

  await nextDot.hover();
  await expect(preview).toContainText(next.title);
  await expect(preview).toContainText('Preview tag');
  await page.screenshot({ path: test.info().outputPath('slick-mini-map-desktop.png') });
  await nextDot.focus();
  await nextDot.press('Escape');
  await expect(preview).toBeHidden();
  await expect(map).toBeVisible();

  await boardPage.renameOpenCard('Unsaved title');
  await nextDot.click();
  const confirmation = page.getByRole('dialog', { name: 'Unsaved changes', exact: true });
  await confirmation.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(page).toHaveURL(`/boards/${board.id}/card/${current.id}`);
  await expect(page.getByRole('button', { name: `#${current.id} Unsaved title`, exact: true })).toBeVisible();
  const currentDot = map.getByRole('button', { name: `#${current.id}: Unsaved title`, exact: true });
  await currentDot.focus();
  await currentDot.press('Tab');
  await expect(nextDot).toBeFocused();
  await expect(preview).toContainText(next.title);
  await nextDot.press('Enter');
  await confirmation.getByRole('button', { name: 'Discard', exact: true }).click();
  await expect(page).toHaveURL(`/boards/${board.id}/card/${next.id}`);
  await expect(nextDot).toHaveAttribute('aria-current', 'true');

  const inProgress = board.columns.find(column => column.title === 'In Progress')!;
  await api.moveCard(board.id, finished.id, inProgress.id);
  await expect(map.getByRole('group', { name: 'Done', exact: true })).toHaveCount(0);
  await expect(map.getByRole('group', { name: 'In Progress', exact: true })).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  const finishedDot = map.getByRole('button', { name: `#${finished.id}: ${finished.title}`, exact: true });
  await nextDot.focus();
  await nextDot.press('Tab');
  await expect(finishedDot).toBeFocused();
  await expect(preview).toContainText(finished.title);
  await expect(preview).toBeInViewport({ ratio: 1 });
  await page.screenshot({ path: test.info().outputPath('slick-mini-map-mobile.png') });
  await finishedDot.press('Escape');
  await expect(preview).toBeHidden();
  await finishedDot.press('Escape');
  await expect(page.getByRole('dialog')).toBeHidden();
});

test('slick mini map keyboard focus replaces stale hover and pointer previews close on leaving a dot', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Slick mini map keyboard and pointer');
  const slick = await api.createSlick(board, 'Shared work');
  const current = await api.createCard(board, 'Todo', 'Keyboard destination', '', [], slick.name);
  await api.createCard(board, 'Todo', 'Pointer destination', '', [], slick.name);
  await page.goto(`/boards/${board.id}/card/${current.id}`);
  const map = page.getByRole('region', { name: 'Slick mini map', exact: true });
  const first = map.getByRole('button').first();
  const second = map.getByRole('button').nth(1);
  const preview = page.getByRole('tooltip');
  await first.hover();
  await expect(preview).toContainText('Pointer destination');
  await first.focus();
  await first.press('Tab');
  await expect(second).toBeFocused();
  await expect(preview).toContainText('Keyboard destination');
  await second.hover();
  await first.hover();
  await expect(preview).toContainText('Pointer destination');
  await page.getByTitle('Select slick', { exact: true }).hover();
  await expect(preview).toBeHidden();
});

test('slick mini map follows the draft slick and handles empty or cleared selections', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Slick mini map selection');
  const first = await api.createSlick(board, 'First slick');
  const second = await api.createSlick(board, 'Second slick');
  const empty = await api.createSlick(board, 'Empty slick');
  const current = await api.createCard(board, 'Todo', 'Current card', '', [], first.name);
  const other = await api.createCard(board, 'Done', 'Other slick card', '', [], second.name);
  await page.goto(`/boards/${board.id}/card/${current.id}`);
  const editor = new CardEditorPage(page);
  const map = page.getByRole('region', { name: 'Slick mini map', exact: true });
  await editor.openSlickPicker();
  await editor.slickOption(second.name).click();
  const currentDot = map.getByRole('button', { name: `#${current.id}: ${current.title}`, exact: true });
  await expect(map.getByRole('button')).toHaveCount(2);
  await expect(map.getByRole('button', { name: `#${other.id}: ${other.title}`, exact: true })).toBeVisible();
  await expect(currentDot).toHaveAttribute('aria-description', 'Unsaved changes');
  await editor.openSlickPicker();
  await editor.slickOption(empty.name).click();
  await expect(map.getByRole('button')).toHaveCount(1);
  await expect(currentDot).toBeVisible();
  await editor.createSlick('New unsaved slick');
  await expect(map.getByRole('button')).toHaveCount(1);
  await expect(currentDot).toBeVisible();
  await editor.openSlickPicker();
  await editor.slickOption('No slick').click();
  await expect(map.getByRole('button')).toHaveCount(1);
  await expect(currentDot).toHaveAttribute('aria-description', 'Unsaved changes');
  await editor.openSlickPicker();
  await editor.slickOption(first.name).click();
  await expect(currentDot).not.toHaveAttribute('aria-description');
});

test('slick mini map previews draft column and type changes without moving the saved card', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Slick mini map draft destination');
  const slick = await api.createSlick(board, 'Draft preview');
  const current = await api.createCard(board, 'Todo', 'Current card', '', [], slick.name);
  await api.createCard(board, 'Done', 'Destination neighbour', '', [], slick.name);
  const typeName = 'Draft type';
  await api.createCardType(board.id, typeName);
  await page.goto(`/boards/${board.id}/card/${current.id}`);
  const map = page.getByRole('region', { name: 'Slick mini map', exact: true });
  const currentDot = map.getByRole('button', { name: `#${current.id}: ${current.title}`, exact: true });
  const boardPage = new BoardPage(page);
  await page.getByTitle('Select card type', { exact: true }).click();
  await page.getByRole('menu', { name: 'Select card type', exact: true }).getByRole('menuitemradio', { name: typeName, exact: true }).click();
  await expect(currentDot).toHaveAttribute('aria-description', 'Unsaved changes');
  await expect(map.getByRole('group', { name: 'Todo', exact: true }).getByRole('button')).toHaveCount(1);
  await boardPage.moveOpenCardTo('Done');
  await expect(map.getByRole('group', { name: 'Todo', exact: true })).toHaveCount(0);
  await expect(map.getByRole('group', { name: 'Done', exact: true }).getByRole('button').first()).toHaveAccessibleName(`#${current.id}: ${current.title}`);
  await currentDot.hover();
  await expect(page.getByRole('tooltip')).toContainText('Done');
  await expect(page.getByRole('tooltip')).toContainText('Unsaved changes');
  expect((await api.getCard(board.id, current.id)).boardColumnId).toBe(current.boardColumnId);
  await boardPage.saveOpenCard();
  await boardPage.openCard('Done', current.title);
  await expect(currentDot).not.toHaveAttribute('aria-description');
});

test('slick mini map navigation saves a slick change before opening the destination and retains rejected drafts', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Slick mini map save and open');
  const first = await api.createSlick(board, 'Original slick');
  const second = await api.createSlick(board, 'Destination slick');
  const current = await api.createCard(board, 'Todo', 'Card to save', '', [], first.name);
  const other = await api.createCard(board, 'Done', 'Card to open', '', [], second.name);
  await page.goto(`/boards/${board.id}/card/${current.id}`);
  const editor = new CardEditorPage(page);
  await editor.openSlickPicker();
  await editor.slickOption(second.name).click();
  const map = page.getByRole('region', { name: 'Slick mini map', exact: true });
  const destination = map.getByRole('button', { name: `#${other.id}: ${other.title}`, exact: true });
  const confirmation = page.getByRole('dialog', { name: 'Unsaved changes', exact: true });
  await destination.click();
  await expect(confirmation).toContainText(`You have unsaved changes, navigate to ${other.title}?`);

  const failedSave = await holdCardSaveResponse(page, board.id, current.id, { reject: true });
  try {
    await confirmation.getByRole('button', { name: 'Save', exact: true }).click();
    await failedSave.ready;
    await expect(confirmation).toHaveAttribute('aria-busy', 'true');
    await expect(page).toHaveURL(`/boards/${board.id}/card/${current.id}`);
    failedSave.release();
    await expect(confirmation).toBeHidden();
    await expect(page).toHaveURL(`/boards/${board.id}/card/${current.id}`);
    await expect(page.getByTitle('Select slick', { exact: true })).toHaveText(second.name);
  } finally {
    failedSave.release();
  }

  await destination.click();
  await confirmation.getByRole('button', { name: 'Save', exact: true }).click();
  await expect(page).toHaveURL(`/boards/${board.id}/card/${other.id}`);
  expect((await api.getCard(board.id, current.id)).slickName).toBe(second.name);
});
