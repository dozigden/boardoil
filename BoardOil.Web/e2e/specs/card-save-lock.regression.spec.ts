import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';
import { holdCardSaveResponse } from '../support/cardSaveResponse';

for (const operation of ['create', 'edit', 'duplicate'] as const) {
  test(`${operation} locks the dialog while saving without losing keyboard focus`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Card ${operation} lock`);
    const card = operation === 'create' ? null : await api.createCard(board, 'Todo', 'Original card');
    const boardPage = new BoardPage(page);
    const editor = new CardEditorPage(page);
    await boardPage.open(board.id);
    const dialog = page.getByRole('dialog');
    if (card) {
      await boardPage.openCard('Todo', 'Original card');
      if (operation === 'duplicate') {
        await dialog.getByRole('button', { name: 'Card actions', exact: true }).click();
        await dialog.getByRole('button', { name: 'Duplicate', exact: true }).click();
        await expect(editor.descriptionEditor()).toHaveCount(1);
      }
    } else {
      await boardPage.openCardDetails('Todo');
      await dialog.getByRole('textbox', { name: 'Card title', exact: true }).fill('Created card');
    }
    await editor.descriptionEditor().fill('Submitted description');
    const hold = await holdCardSaveResponse(page, board.id, card?.id ?? null, { duplicate: operation === 'duplicate' });
    try {
      await editor.descriptionEditor().press('ControlOrMeta+s');
      await hold.ready;
      await expect(dialog).toHaveAttribute('aria-busy', 'true');
      await page.keyboard.type('Accidental extra typing');
      await page.keyboard.press('Escape');
      await expect(dialog).toBeVisible();
      await expect(editor.descriptionEditor()).toHaveText('Submitted description');
      // Even an attempted focus on another field cannot enable editing while saving.
      const tags = dialog.getByRole('textbox', { name: 'Add tags', exact: true, includeHidden: true });
      await tags.focus();
      await expect(tags).not.toBeFocused();
      await page.keyboard.type('Accidental tag');
      await expect(tags).toHaveValue('');
      hold.release();
      if (operation === 'edit') {
        await expect(dialog).not.toHaveAttribute('aria-busy', 'true');
        await expect(editor.descriptionEditor()).toBeFocused();
        await page.keyboard.type(' Editable again');
        await expect(editor.descriptionEditor()).toContainText('Editable again');
        expect((await api.getCard(board.id, card!.id)).description).toBe('Submitted description');
      } else {
        await expect(dialog).toBeHidden();
        const expectedTitle = operation === 'create' ? 'Created card' : 'Original card';
        await expect(boardPage.card('Todo', expectedTitle)).toHaveCount(operation === 'create' ? 1 : 2);
      }
    } finally { hold.release(); }
  });
}

test('a rejected create unlocks the retained draft for correction', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Rejected create unlock');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await boardPage.openCardDetails('Todo');
  const dialog = page.getByRole('dialog');
  const title = dialog.getByRole('textbox', { name: 'Card title', exact: true });
  await title.fill('Rejected title');
  const hold = await holdCardSaveResponse(page, board.id, null, { reject: true });
  try {
    await dialog.getByRole('button', { name: 'Create card', exact: true }).click();
    await hold.ready;
    await expect(dialog).toHaveAttribute('aria-busy', 'true');
    await page.keyboard.press('Escape');
    await expect(dialog).toBeVisible();
    hold.release();
    await expect(dialog.getByRole('alert')).toContainText('Please choose a different title.');
    await expect(title).toHaveValue('Rejected title');
    await title.fill('Corrected title');
    await dialog.getByRole('button', { name: 'Create card', exact: true }).click();
    await expect(dialog).toBeHidden();
    await expect(boardPage.card('Todo', 'Corrected title')).toBeVisible();
  } finally { hold.release(); }
});

for (const shortcut of ['Control+s', 'Meta+s']) {
  test(`${shortcut} and nested Save do not create the card from the link editor`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard('Nested link save');
    const boardPage = new BoardPage(page);
    const editor = new CardEditorPage(page);
    await boardPage.open(board.id);
    await boardPage.openCardDetails('Todo');
    const cardDialog = page.getByRole('dialog').filter({ has: page.getByLabel('Card title', { exact: true }) });
    await cardDialog.getByRole('textbox', { name: 'Card title', exact: true }).fill('Card with link');
    await editor.descriptionEditor().fill('Original description');
    await cardDialog.getByRole('toolbar', { name: 'Markdown formatting' })
      .getByRole('button', { name: 'Set link', exact: true }).click();
    const linkDialog = page.getByRole('dialog', { name: 'Edit Link', exact: true });
    await linkDialog.getByRole('textbox', { name: 'Text', exact: true }).fill('Before shortcut');
    await linkDialog.getByRole('textbox', { name: 'Text', exact: true }).press(shortcut);
    await linkDialog.getByRole('textbox', { name: 'Text', exact: true }).fill('Retained link');
    await linkDialog.getByRole('textbox', { name: 'URL', exact: true }).fill('https://example.com/retained');
    await linkDialog.getByRole('button', { name: 'Save', exact: true }).click();
    await expect(linkDialog).toBeHidden();
    await expect(cardDialog).toBeVisible();
    await expect(editor.descriptionEditor()).toContainText('Retained link');
    await expect(boardPage.card('Todo', 'Card with link')).toHaveCount(0);

    await cardDialog.getByRole('button', { name: 'Create card', exact: true }).click();
    await expect(cardDialog).toBeHidden();
    await page.reload();
    await boardPage.openCard('Todo', 'Card with link');
    await expect(editor.descriptionEditor().getByRole('link', { name: 'Retained link', exact: true }))
      .toHaveAttribute('href', 'https://example.com/retained');
  });
}
