import { expect, test } from '../fixtures/boardOilTest';
import { delayColumns, failNextLookup, rejectNextTransfer, showContributorDestination } from '../support/cardTransferResponses';
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

test('a deleted destination column is reported beside the field and can be corrected without losing the draft', async ({ api, authenticatedPage: page }) => {
  const source = await api.createBoard('Transfer field error source');
  const destination = await api.createBoard('Transfer field error destination');
  const card = await api.createCard(source, 'Todo', 'Retained transfer draft');
  await page.goto(`/boards/${source.id}/card/${card.id}/transfer`);
  const dialog = page.getByRole('dialog', { name: 'Move card to another board' });
  await dialog.getByLabel('Destination board', { exact: true }).selectOption(String(destination.id));
  const column = dialog.getByLabel('Destination column', { exact: true });
  await column.selectOption({ label: 'In Progress' });
  await dialog.getByRole('radio', { name: /^Use destination defaults/ }).check();
  await api.deleteColumn(destination.id, Number(await column.inputValue()));

  await dialog.getByRole('button', { name: 'Move card', exact: true }).click();
  await expect(column).toBeFocused();
  await expect(column).toHaveAttribute('aria-invalid', 'true');
  await expect(dialog.getByRole('alert')).toHaveText('Column does not exist on the destination board.');
  await expect(page.getByText('Column does not exist on the destination board.', { exact: true })).toHaveCount(1);
  await expect(dialog.getByRole('radio', { name: /^Use destination defaults/ })).toBeChecked();
  await expect(dialog.getByLabel('Destination board', { exact: true })).toHaveValue(String(destination.id));
  await page.screenshot({ path: '/tmp/forms-engine-transfer-field-error.png' });

  await column.selectOption({ label: 'Todo' });
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Move card', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/boards/${destination.id}/card/\\d+$`));
});

test('reassessment locks the form while loading columns and adjusts the policy for the new board', async ({ api, authenticatedPage: page }) => {
  const source = await api.createBoard('Transfer pending source');
  const ownerBoard = await api.createBoard('Transfer pending owner');
  const contributorBoard = await api.createBoard('Transfer pending contributor');
  const card = await api.createCard(source, 'Todo', 'Changing destinations');
  await showContributorDestination(page, contributorBoard.id);
  await page.goto(`/boards/${source.id}/card/${card.id}/transfer`);
  const dialog = page.getByRole('dialog', { name: 'Move card to another board' });
  const board = dialog.getByLabel('Destination board', { exact: true });
  const column = dialog.getByLabel('Destination column', { exact: true });
  const pending = await delayColumns(page, ownerBoard.id);
  await board.selectOption(String(ownerBoard.id));
  await pending.requested;
  await expect(column).toBeDisabled();
  await expect(board).toBeDisabled();
  await expect(dialog.getByRole('status')).toHaveText('Loading choices...');
  await expect(dialog.getByRole('radio', { name: /^Keep matching only/ })).toBeDisabled();
  await expect(dialog.getByRole('button', { name: 'Move card', exact: true })).toBeDisabled();
  pending.release();
  await pending.finished;
  await expect(column).toHaveValue(String(ownerBoard.columns[0]!.id));
  await dialog.getByRole('radio', { name: /^Copy missing/ }).check();
  await board.selectOption(String(contributorBoard.id));
  await expect(column).toHaveValue(String(contributorBoard.columns[0]!.id));
  await expect(dialog.getByRole('radio', { name: /^Copy missing/ })).toBeDisabled();
  await expect(dialog.getByRole('radio', { name: /^Keep matching only/ })).toBeChecked();

  await board.selectOption('');
  await expect(column).toBeDisabled();
  await expect(column).toHaveValue('');
  await expect(column).toHaveText('Select a board first');
  await expect(dialog.getByRole('button', { name: 'Move card', exact: true })).toBeDisabled();
  await board.selectOption(String(contributorBoard.id));
  await expect(column).toBeEnabled();
  await dialog.getByRole('button', { name: 'Move card', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/boards/${contributorBoard.id}/card/\\d+$`));
});

test('board and column lookup failures can be retried in the dialog', async ({ api, authenticatedPage: page }) => {
  const source = await api.createBoard('Transfer lookup source');
  const destination = await api.createBoard('Transfer lookup destination');
  const card = await api.createCard(source, 'Todo', 'Lookup retries');
  // The board layout also loads the catalogue, so fail the dialog's refresh after entering the board.
  await page.goto(`/boards/${source.id}/card/${card.id}`);
  await expect(page.getByRole('button', { name: 'Card actions' })).toBeVisible();
  await failNextLookup(page, '/api/boards', 'Boards unavailable.');
  await page.getByRole('button', { name: 'Card actions' }).click();
  await page.getByRole('menu', { name: 'Card actions' }).getByRole('button', { name: 'Move to another board' }).click();
  const dialog = page.getByRole('dialog', { name: 'Move card to another board' });
  await expect(dialog.getByRole('alert')).toContainText('Could not load destination boards.');
  await expect(page.getByText('Boards unavailable.', { exact: true })).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Retry' }).click();
  await expect(dialog.getByRole('alert')).toHaveCount(0);

  await failNextLookup(page, `/api/boards/${destination.id}/columns`, 'Columns unavailable.');
  await dialog.getByLabel('Destination board', { exact: true }).selectOption(String(destination.id));
  await expect(dialog.getByRole('alert')).toContainText('Columns unavailable.');
  await expect(dialog.getByRole('button', { name: 'Move card', exact: true })).toBeDisabled();
  await dialog.getByRole('button', { name: 'Retry' }).click();
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await expect(dialog.getByLabel('Destination column', { exact: true })).toHaveValue(String(destination.columns[0]!.id));
  await dialog.getByRole('button', { name: 'Move card', exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/boards/${destination.id}/card/\\d+$`));
});

test('transfer failures retain choices, block duplicate submission and cancel while busy, and retry successfully', async ({ api, authenticatedPage: page }) => {
  const source = await api.createBoard('Transfer conflict source');
  const destination = await api.createBoard('Transfer conflict destination');
  const card = await api.createCard(source, 'Todo', 'Retry the transfer');
  await page.goto(`/boards/${source.id}/card/${card.id}/transfer`);
  const dialog = page.getByRole('dialog', { name: 'Move card to another board' });
  await dialog.getByLabel('Destination board', { exact: true }).selectOption(String(destination.id));
  await dialog.getByLabel('Destination column', { exact: true }).selectOption({ label: 'In Progress' });
  const release = await rejectNextTransfer(page, source.id, card.id);
  const move = dialog.getByRole('button', { name: 'Move card', exact: true });
  await move.click();
  await expect(move).toBeDisabled();
  await expect(dialog.getByRole('button', { name: 'Cancel', exact: true })).toBeDisabled();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  release();
  await expect(dialog.getByRole('alert')).toHaveText('Destination changed. Please try again.');
  await expect(dialog.getByRole('alert')).toBeFocused();
  await expect(page.getByText('Destination changed. Please try again.', { exact: true })).toHaveCount(1);
  await expect(dialog.getByLabel('Destination column', { exact: true })).toHaveValue(String(destination.columns.find(c => c.title === 'In Progress')!.id));
  await move.click();
  await expect(page).toHaveURL(new RegExp(`/boards/${destination.id}/card/\\d+$`));
});
