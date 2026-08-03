import { expect, test } from '../fixtures/boardOilTest';
import { ArchivedCardsPage } from '../ui/ArchivedCardsPage';
import { BoardPage } from '../ui/BoardPage';

test('archive and restore a card to its original column', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression card archive');
  const cardTitle = 'Card to archive and restore';
  await api.createCard(board, 'In Progress', cardTitle);

  const boardPage = new BoardPage(page);
  const archivedCardsPage = new ArchivedCardsPage(page);
  await boardPage.open(board.id);

  await test.step('archive the card through the board', async () => {
    await boardPage.enterCardSelectionMode();
    await boardPage.selectCard('In Progress', cardTitle);
    await boardPage.archiveSelectedCards(1);
    await expect(boardPage.card('In Progress', cardTitle)).toHaveCount(0);
  });

  await test.step('restore the card through the archive', async () => {
    await boardPage.openArchivedCards();
    await expect(archivedCardsPage.row(cardTitle)).toBeVisible();
    await archivedCardsPage.openCard(cardTitle);
    await archivedCardsPage.unarchiveOpenCard(cardTitle);
    await expect(archivedCardsPage.row(cardTitle)).toHaveCount(0);
  });

  await test.step('reload the restored card in its original column', async () => {
    await archivedCardsPage.goBackToBoard();
    await expect(boardPage.card('In Progress', cardTitle)).toBeVisible();
    await page.reload();
    await expect(boardPage.card('In Progress', cardTitle)).toBeVisible();
  });
});
