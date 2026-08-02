import { expect, test } from '../fixtures/boardOilTest';

test('plain-text Enter splits a numbered-list line and keeps the caret in place', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Smoke markdown caret');
  const description = '1. Alpha beta\n2. Second\n3. Third';
  const card = await api.createCard(board, 'Todo', 'Markdown caret card', description);

  await page.goto(`/boards/${board.id}/card/${card.id}`);

  const dialog = page.getByRole('dialog');
  await dialog.getByRole('button', { name: 'Switch to markdown text editor' }).click();
  const editor = dialog.getByRole('textbox', { name: 'Card description markdown' });
  const markdownBeforeEnter = await editor.inputValue();
  const insertionPoint = markdownBeforeEnter.indexOf(' beta');
  const expectedMarkdown = `${markdownBeforeEnter.slice(0, insertionPoint)}\n${markdownBeforeEnter.slice(insertionPoint)}`;

  await editor.evaluate((element, caretPosition) => {
    const textArea = element as HTMLTextAreaElement;
    textArea.focus();
    textArea.setSelectionRange(caretPosition, caretPosition);
  }, insertionPoint);
  await editor.press('Enter');

  await expect(editor).toHaveValue(expectedMarkdown);
  await expect(editor).toBeFocused();
  await expect.poll(async () => await editor.evaluate(element => (element as HTMLTextAreaElement).selectionStart)).toBe(insertionPoint + 1);
});
