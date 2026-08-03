import { expect, test } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';

test('shared Markdown toolbar formats only the focused description or comment', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression shared Markdown toolbar');
  const cardTitle = 'Shared toolbar card';
  const descriptionDraft = 'Description draft';
  const commentDraft = 'Comment draft';
  const card = await api.createCard(board, 'Todo', cardTitle, descriptionDraft);

  const cardEditor = new CardEditorPage(page);
  await page.goto(`/boards/${board.id}/card/${card.id}`);

  const descriptionEditor = cardEditor.descriptionEditor();
  const commentEditor = cardEditor.commentEditor();
  await expect(descriptionEditor).toHaveText(descriptionDraft);
  await commentEditor.fill(commentDraft);

  await test.step('format only the focused description', async () => {
    await cardEditor.formatAllText(descriptionEditor, 'Bold');
    await expect(descriptionEditor.locator('strong')).toHaveText(descriptionDraft);
    await expect(descriptionEditor.locator('em')).toHaveCount(0);
    await expect(commentEditor).toHaveText(commentDraft);
    await expect(commentEditor.locator('strong')).toHaveCount(0);
  });

  await test.step('format only the focused comment', async () => {
    await cardEditor.formatAllText(commentEditor, 'Italic');
    await expect(commentEditor.locator('em')).toHaveText(commentDraft);
    await expect(commentEditor.locator('strong')).toHaveCount(0);
    await expect(descriptionEditor.locator('strong')).toHaveText(descriptionDraft);
    await expect(descriptionEditor.locator('em')).toHaveCount(0);
  });

  await test.step('persist both formatted values independently', async () => {
    await cardEditor.addComment();
    await expect(cardEditor.commentContent().locator('em')).toHaveText(commentDraft);
    await expect(descriptionEditor.locator('strong')).toHaveText(descriptionDraft);
    await cardEditor.saveCard();

    await page.goto(`/boards/${board.id}/card/${card.id}`);
    await expect(cardEditor.descriptionEditor().locator('strong')).toHaveText(descriptionDraft);
    await expect(cardEditor.commentContent().locator('em')).toHaveText(commentDraft);
  });
});
