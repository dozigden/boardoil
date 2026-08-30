import { expect, test } from '../fixtures/boardOilTest';

test('plain-text editing preserves the caret and synchronises back to rich mode', async ({ api, authenticatedPage: page }) => {
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

  const synchronisedMarkdown = '**Synchronised draft** retained from plain text';
  await editor.fill(synchronisedMarkdown);
  await dialog.getByRole('button', { name: 'Switch to rich editor' }).click();
  const richEditor = dialog.getByLabel('Card description', { exact: true });
  await expect(richEditor.locator('strong')).toHaveText('Synchronised draft');
  await dialog.getByRole('button', { name: 'Save card' }).click();

  await page.goto(`/boards/${board.id}/card/${card.id}`);
  await expect(dialog.getByLabel('Card description', { exact: true }).locator('strong'))
    .toHaveText('Synchronised draft');
});
