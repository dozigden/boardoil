import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';

test('Details creates a full card and shows backend validation on a single-type board', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Detailed creation');
  const boardPage = new BoardPage(page);
  const editor = new CardEditorPage(page);
  await boardPage.open(board.id);
  await boardPage.openCardDetails('Todo');
  const dialog = page.getByRole('dialog');
  const title = dialog.getByRole('textbox', { name: 'Card title', exact: true });
  await expect(title).toBeFocused();
  await expect(dialog.getByRole('region', { name: 'Card comments' })).toHaveCount(0);
  await expect(dialog.getByRole('button', { name: 'Card actions' })).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Create card', exact: true }).click();
  await expect(dialog.getByRole('alert')).toContainText('Card title is required.');

  await title.fill('Detailed card');
  await editor.setDescriptionMarkdown('A detailed description');
  await editor.createSlick('Creation slick');
  await dialog.getByRole('textbox', { name: 'Add tags', exact: true }).fill('Creation tag');
  await dialog.getByRole('button', { name: 'Add tags', exact: true }).click();
  await expect(dialog.getByLabel('Creation tag', { exact: true })).toBeVisible();
  await dialog.getByRole('group', { name: 'External link' }).getByRole('button', { name: 'Add', exact: true }).click();
  await dialog.getByRole('textbox', { name: 'External URL' }).fill('https://example.com/details');
  await dialog.getByRole('button', { name: 'Apply external URL' }).click();
  await boardPage.moveOpenCardTo('In Progress');
  await dialog.getByRole('button', { name: 'Create card', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(boardPage.card('In Progress', 'Detailed card')).toBeVisible();

  await page.reload();
  await boardPage.openCard('In Progress', 'Detailed card');
  await expect(editor.descriptionEditor()).toHaveText('A detailed description');
  await expect(dialog.getByTitle('Select slick', { exact: true })).toHaveText('Creation slick');
  await expect(dialog.getByLabel('Creation tag', { exact: true })).toBeVisible();
  await expect(dialog.getByRole('link', { name: 'https://example.com/details', exact: true })).toBeVisible();
});

test('Details expands quick-add with its title and type and keyboard creation saves once', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Expanded quick-add');
  await api.createCardType(board.id, 'Investigation');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.startQuickCard('Todo', 'Expanded draft', 'Investigation');
  await boardPage.openCardDetails('Todo');
  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('textbox', { name: 'Card title', exact: true })).toHaveValue('Expanded draft');
  await expect(dialog.getByTitle('Select card type', { exact: true })).toHaveText('Investigation');
  await expect(dialog.getByLabel('Card description', { exact: true })).toBeFocused();
  await expect(boardPage.column('Todo').getByPlaceholder('Title', { exact: true })).toHaveCount(0);
  await page.keyboard.press('ControlOrMeta+s');
  await expect(dialog).toBeHidden();
  await expect(boardPage.card('Todo', 'Expanded draft')).toHaveCount(1);
  await expect(boardPage.card('Todo', 'Expanded draft')).toBeFocused();
  await page.reload();
  await boardPage.openCard('Todo', 'Expanded draft');
  await expect(dialog.getByTitle('Select card type', { exact: true })).toHaveText('Investigation');
});

test('cancelling Details discards the draft without creating a card or its new tags', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Cancelled detailed card');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCardDetails('Todo');
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('textbox', { name: 'Card title', exact: true }).fill('Discarded card');
  await dialog.getByRole('textbox', { name: 'Add tags', exact: true }).fill('Discarded tag');
  await dialog.getByRole('button', { name: 'Add tags', exact: true }).click();
  await dialog.getByRole('button', { name: 'Cancel creation', exact: true }).first().click();
  const confirmation = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
  await confirmation.getByRole('button', { name: 'Discard', exact: true }).click();
  await expect(dialog).toBeHidden();
  await expect(boardPage.column('Todo').getByRole('button', { name: 'Choose card type', exact: true })).toBeFocused();
  await page.reload();
  await expect(boardPage.card('Todo', 'Discarded card')).toHaveCount(0);
  await boardPage.openCardDetails('Todo');
  await expect(dialog.getByRole('textbox', { name: 'Card title', exact: true })).toHaveValue('');
  await dialog.getByRole('textbox', { name: 'Add tags', exact: true }).fill('Discarded');
  await expect(dialog.getByRole('listbox', { name: 'Tag suggestions' })).toHaveCount(0);
});

for (const expandQuickAdd of [false, true]) {
  test(`Escape returns focus to the originating column menu after ${expandQuickAdd ? 'expanding quick-add' : 'opening Add...'}`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard('Add cancellation focus');
    const boardPage = new BoardPage(page);
    await boardPage.open(board.id);
    const column = boardPage.column('In Progress');
    if (expandQuickAdd) {
      await column.getByRole('button', { name: 'Add default card' }).click();
      await column.getByPlaceholder('Title', { exact: true }).fill('Cancelled quick draft');
    }
    await boardPage.openCardDetails('In Progress');
    await page.keyboard.press('Escape');
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(column.getByRole('button', { name: 'Choose card type', exact: true })).toBeFocused();
    await expect(boardPage.card('In Progress', 'Cancelled quick draft')).toHaveCount(0);
  });
}
