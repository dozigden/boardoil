import { expect, test } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';

test('Escape closes a card while the rich-text description has focus', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card editor rich-text Escape');
  const card = await api.createCard(board, 'Todo', 'Rich-text Escape card', 'Description');
  const cardEditor = new CardEditorPage(page);

  await page.goto(`/boards/${board.id}/card/${card.id}`);
  await cardEditor.descriptionEditor().focus();
  await cardEditor.descriptionEditor().press('Escape');

  await expect(page.getByRole('dialog')).toBeHidden();
  await expect(page).toHaveURL(`/boards/${board.id}`);
});

test('Escape closes a card while the plain-text description has focus', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card editor plain-text Escape');
  const card = await api.createCard(board, 'Todo', 'Plain-text Escape card', 'Description');

  await page.goto(`/boards/${board.id}/card/${card.id}`);
  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Switch to markdown text editor' }).click();
  const editor = dialog.getByRole('textbox', { name: 'Card description markdown' });
  await editor.focus();
  await editor.press('Escape');

  await expect(dialog).toBeHidden();
  await expect(page).toHaveURL(`/boards/${board.id}`);
});

test('Escape keeps a dirty card open when discarding changes is cancelled', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Card editor guarded Escape');
  const card = await api.createCard(board, 'Todo', 'Guarded Escape card', 'Original description');
  const cardEditor = new CardEditorPage(page);
  const unsavedDescription = 'Unsaved description';

  await page.goto(`/boards/${board.id}/card/${card.id}`);
  await cardEditor.descriptionEditor().fill(unsavedDescription);
  await cardEditor.descriptionEditor().press('Escape');

  const confirmDialog = page.getByRole('dialog').filter({
    has: page.getByRole('heading', { name: 'Discard unsaved changes' })
  });
  await expect(confirmDialog).toBeVisible();
  await confirmDialog.getByRole('button', { name: 'Cancel', exact: true }).click();

  await expect(confirmDialog).toBeHidden();
  await expect(page).toHaveURL(`/boards/${board.id}/card/${card.id}`);
  await expect(cardEditor.descriptionEditor()).toHaveText(unsavedDescription);
});
