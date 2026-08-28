import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('move a card to another board with matching content', async ({ api, authenticatedPage: page }) => {
  const sourceBoard = await api.createBoard('Regression transfer source');
  const destinationBoard = await api.createBoard('Regression transfer destination');
  const cardTitle = 'Card moving between boards';
  const tagName = 'Transfer tag';
  const sourceCard = await api.createCard(sourceBoard, 'Todo', cardTitle, '', [tagName]);
  const boardPage = new BoardPage(page);
  await boardPage.open(sourceBoard.id);

  await test.step('choose the destination and transfer policy', async () => {
    await boardPage.openCard('Todo', cardTitle);
    await page.getByRole('button', { name: 'Card actions' }).click();
    await page.getByRole('menu', { name: 'Card actions' })
      .getByRole('button', { name: 'Move to another board' })
      .click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Move card to another board' })).toBeVisible();
    const cardPreview = dialog.getByLabel('Card being moved');
    await expect(cardPreview).toContainText(`#${sourceCard.id}`);
    await expect(cardPreview).toContainText(cardTitle);
    await expect(cardPreview.getByLabel(tagName)).toBeVisible();
    await expect(cardPreview.getByRole('button')).toHaveCount(0);
    await dialog.getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(page).toHaveURL(new RegExp(`/boards/${sourceBoard.id}/card/\\d+$`));

    await page.getByRole('button', { name: 'Card actions' }).click();
    await page.getByRole('menu', { name: 'Card actions' })
      .getByRole('button', { name: 'Move to another board' })
      .click();
    const reopenedDialog = page.getByRole('dialog');
    await reopenedDialog.getByLabel('Destination board', { exact: true }).selectOption(String(destinationBoard.id));
    const destinationColumn = reopenedDialog.getByLabel('Destination column', { exact: true });
    await expect(destinationColumn).toHaveValue(/\d+/);
    await expect(destinationColumn.getByRole('option', { name: 'Select column' })).toHaveCount(0);
    await destinationColumn.selectOption({ label: 'In Progress' });
    await expect(reopenedDialog.getByRole('radio', { name: /Keep matching only/ })).toBeChecked();
    await expect(reopenedDialog.getByText(
      "Use the destination card type, tags, and slick where they match, otherwise they're cleared."
    )).toBeVisible();
    await expect(reopenedDialog.getByText(
      'Any card type, tags, or slick that are missing will be created on the destination board.'
    )).toBeVisible();
    await reopenedDialog.getByRole('button', { name: 'Move card' }).click();
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
