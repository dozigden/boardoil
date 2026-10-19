import { authenticatePage, expect, getBaseUrl, test } from '../fixtures/boardOilTest';
import { ColumnsManagerPage } from '../ui/ColumnsManagerPage';

test('column drag-and-drop persists the complete order and updates another connected client', async ({ api, browser, authenticatedPage: page }) => {
  const board = await api.createBoard('Complete column order');
  const titles = board.columns.map(column => column.title);
  expect(titles).toHaveLength(3);
  const first = titles[0]!;
  const middle = titles[1]!;
  const last = titles[2]!;
  const secondContext = await browser.newContext({ baseURL: getBaseUrl() });

  try {
    const secondPage = await secondContext.newPage();
    await authenticatePage(secondPage);
    const local = new ColumnsManagerPage(page);
    const remote = new ColumnsManagerPage(secondPage);
    await local.open(board.id);
    await remote.open(board.id);
    await local.expectOrder(titles);
    await remote.expectOrder(titles);

    await test.step('move the first column to the end', async () => {
      await local.dragColumnOnto(first, last);
      await local.expectOrder([middle, last, first]);
      await remote.expectOrder([middle, last, first]);
    });

    await test.step('move it back to the beginning and reload', async () => {
      await local.dragColumnOnto(first, middle);
      await local.expectOrder(titles);
      await remote.expectOrder(titles);
      await page.reload();
      await local.expectOrder(titles);
    });
  } finally {
    await secondContext.close();
  }
});
