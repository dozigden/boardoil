import { expect, test } from '../fixtures/boardOilTest';
import { CardEditorPage } from '../ui/CardEditorPage';

for (const viewport of [{ width: 1280, height: 720 }, { width: 600, height: 720 }]) {
  test(`upward cursor movement reveals the description below the toolbar at ${viewport.width}px`, async ({ api, authenticatedPage: page }) => {
    await page.setViewportSize(viewport);
    const board = await api.createBoard('Regression description keyboard scrolling');
    const description = Array.from({ length: 35 }, (_, index) => `Description line ${index + 1}`).join('\n\n');
    const card = await api.createCard(board, 'Todo', 'Long description card', description);
    const cardEditor = new CardEditorPage(page);
    await page.goto(`/boards/${board.id}/card/${card.id}`);
    const editor = cardEditor.descriptionEditor();
    await expect(editor).toHaveText(description.replace(/\n\n/g, ''));

    await cardEditor.moveDescriptionCursorToEnd();
    await expect(editor.locator('p').last()).toBeInViewport();
    for (let index = 0; index < 35; index++) {
      await editor.press('ArrowUp');
      await expect.poll(() => cardEditor.descriptionCursorClearance()).toBeGreaterThanOrEqual(0);
    }

    await expect.poll(() => cardEditor.descriptionTopClearance()).toBeGreaterThanOrEqual(0);
    await expect(editor).toBeFocused();
  });
}
