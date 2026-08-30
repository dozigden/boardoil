import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('move a card to another board with matching content', async ({ api, authenticatedPage: page }) => {
  const sourceBoard = await api.createBoard('Regression transfer source');
  const destinationBoard = await api.createBoard('Regression transfer destination');
  const cardTitle = 'Card moving between boards';
  await api.createCard(sourceBoard, 'Todo', cardTitle);
  const boardPage = new BoardPage(page);
  await boardPage.open(sourceBoard.id);

  await test.step('choose the destination and move the card', async () => {
    await boardPage.openCard('Todo', cardTitle);
    await page.getByRole('button', { name: 'Card actions' }).click();
    await page.getByRole('menu', { name: 'Card actions' })
      .getByRole('button', { name: 'Move to another board' })
      .click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Move card to another board' })).toBeVisible();
    await dialog.getByLabel('Destination board', { exact: true }).selectOption(String(destinationBoard.id));
    const destinationColumn = dialog.getByLabel('Destination column', { exact: true });
    await destinationColumn.selectOption({ label: 'In Progress' });
    await dialog.getByRole('button', { name: 'Move card' }).click();
  });

  await test.step('open the moved card on the destination board', async () => {
    await expect(page).toHaveURL(new RegExp(`/boards/${destinationBoard.id}/card/\\d+$`));
    await page.getByTitle('Cancel editing', { exact: true }).click();
    await expect(boardPage.card('In Progress', cardTitle)).toBeVisible();
  });

  await test.step('confirm the card no longer appears on the source board', async () => {
    await boardPage.open(sourceBoard.id);
    await expect(boardPage.card('Todo', cardTitle)).toHaveCount(0);
  });
});
