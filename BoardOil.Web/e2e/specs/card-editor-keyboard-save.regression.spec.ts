import { expect, test } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';

for (const shortcut of ['Control+s', 'Meta+s']) {
  test(`${shortcut} saves a card in place while the comment editor has focus`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Card editor ${shortcut} save`);
    const card = await api.createCard(board, 'Todo', `${shortcut} save card`, 'Original description');
    const cardEditor = new CardEditorPage(page);
    const updatedDescription = `Saved with ${shortcut}`;
    const commentDraft = `Unposted comment with ${shortcut}`;

    await page.goto(`/boards/${board.id}/card/${card.id}`);
    await cardEditor.descriptionEditor().fill(updatedDescription);
    await cardEditor.commentEditor().fill(commentDraft);
    await cardEditor.commentEditor().press(shortcut);

    await expect(page.getByRole('dialog')).toBeVisible();
    await expect(page).toHaveURL(`/boards/${board.id}/card/${card.id}`);
    const savedToast = page.getByRole('status').filter({ hasText: 'Saved successfully.' });
    await expect(savedToast).toContainText('Saved successfully.');
    await expect(savedToast).toBeVisible();
    expect(await savedToast.evaluate((toast) => toast.matches(':popover-open'))).toBe(true);
    await expect(cardEditor.descriptionEditor()).toHaveText(updatedDescription);
    await expect(cardEditor.commentEditor()).toHaveText(commentDraft);

    await page.reload();
    await expect(cardEditor.descriptionEditor()).toHaveText(updatedDescription);
  });
}
