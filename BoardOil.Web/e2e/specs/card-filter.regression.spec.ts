import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('custom card filter includes, excludes, and clears tagged cards', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression card filters');
  const tagName = 'Filter focus';
  const taggedCardTitle = 'Focus match card';
  const untaggedCardTitle = 'Plain card';
  await api.createCard(board, 'Todo', taggedCardTitle, '', [tagName]);
  await api.createCard(board, 'Todo', untaggedCardTitle);

  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);

  await test.step('include a tag using the keyboard', async () => {
    await boardPage.openCardFilters();
    await boardPage.setTagFilterWithKeyboard(tagName, 'include');
    await expect(boardPage.card('Todo', taggedCardTitle)).toBeVisible();
    await expect(boardPage.card('Todo', untaggedCardTitle)).toHaveCount(0);
  });

  await test.step('clear the active filter', async () => {
    await boardPage.clearCardFilters();
    await expect(boardPage.card('Todo', taggedCardTitle)).toBeVisible();
    await expect(boardPage.card('Todo', untaggedCardTitle)).toBeVisible();
  });

  await test.step('exclude the same tag using the keyboard', async () => {
    await boardPage.openCardFilters();
    await boardPage.setTagFilterWithKeyboard(tagName, 'exclude');
    await expect(boardPage.card('Todo', taggedCardTitle)).toHaveCount(0);
    await expect(boardPage.card('Todo', untaggedCardTitle)).toBeVisible();
  });
});
