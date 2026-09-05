import { authenticatePage, expect, getBaseUrl, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { CardEditorPage } from '../ui/CardEditorPage';

test('a slick created while editing one card is available on another without reloading', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression inline slick');
  await api.createCard(board, 'Todo', 'Card A');
  await api.createCard(board, 'Todo', 'Card B');
  const boardPage = new BoardPage(page);
  const editor = new CardEditorPage(page);
  const slickName = 'Shared inline slick';

  await boardPage.open(board.id);
  await boardPage.openCard('Todo', 'Card A');
  await editor.createSlick(slickName);
  await editor.saveCard();

  await boardPage.openCard('Todo', 'Card B');
  await editor.openSlickPicker();
  await expect(editor.slickOption(slickName)).toBeVisible();
  await editor.slickOption(slickName).click();
  await editor.saveCard();

  await boardPage.openCard('Todo', 'Card B');
  await expect(page.getByRole('dialog').getByTitle('Select slick', { exact: true })).toHaveText(slickName);
});

test('an inline slick appears in an already open picker in another browser context', async ({ api, browser, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression realtime slick');
  await api.createCard(board, 'Todo', 'Card A');
  await api.createCard(board, 'Todo', 'Card B');
  const secondContext = await browser.newContext({ baseURL: getBaseUrl() });

  try {
    const secondPage = await secondContext.newPage();
    await authenticatePage(secondPage);
    const firstBoard = new BoardPage(page);
    const secondBoard = new BoardPage(secondPage);
    const firstEditor = new CardEditorPage(page);
    const secondEditor = new CardEditorPage(secondPage);
    const slickName = 'Realtime inline slick';

    await firstBoard.open(board.id);
    await secondBoard.open(board.id);
    await secondBoard.openCard('Todo', 'Card B');
    await secondEditor.openSlickPicker();
    await expect(secondEditor.slickOption(slickName)).toHaveCount(0);

    await firstBoard.openCard('Todo', 'Card A');
    await firstEditor.createSlick(slickName);
    await firstEditor.saveCard();

    await expect(secondEditor.slickOption(slickName)).toBeVisible();
    await secondEditor.slickOption(slickName).click();
    await secondEditor.saveCard();
  } finally {
    await secondContext.close();
  }
});
