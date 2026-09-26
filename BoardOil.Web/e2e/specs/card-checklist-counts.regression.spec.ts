import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';

test('stored checklist counts refresh after saving, realtime updates and reload', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Checklist counts');
  const title = 'Checklist card';
  await api.createCard(board, 'Todo', title, '- [x] Done\n- [ ] Open');
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const card = boardPage.card('Todo', title);
  await expect(card.getByText('1/2', { exact: true })).toBeVisible();

  const observer = await page.context().newPage();
  try {
    const observerBoard = new BoardPage(observer);
    await observerBoard.open(board.id);
    const observedCard = observerBoard.card('Todo', title);
    await expect(observedCard.getByText('1/2', { exact: true })).toBeVisible();

    await boardPage.openCard('Todo', title);
    const editor = new CardEditorPage(page);
    await editor.setDescriptionMarkdown('- [x] Done\n- [x] Finished');
    await editor.saveCard();
    await expect(card.getByText('2/2', { exact: true })).toBeVisible();
    await expect(observedCard.getByText('2/2', { exact: true })).toBeVisible();

    await page.reload();
    await expect(card.getByText('2/2', { exact: true })).toBeVisible();
    await boardPage.openCard('Todo', title);
    await editor.setDescriptionMarkdown('No tasks remain');
    await editor.saveCard();
    await expect(card.getByLabel(/checklist items complete$/)).toHaveCount(0);
    await expect(observedCard.getByLabel(/checklist items complete$/)).toHaveCount(0);
    await expect(card.getByText('0/0', { exact: true })).toHaveCount(0);
  } finally {
    await observer.close();
  }
});
