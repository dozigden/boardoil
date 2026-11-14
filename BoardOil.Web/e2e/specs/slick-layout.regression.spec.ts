import { expectSlickOutlineAligned as aligned } from '../support/slickEffects';
import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('slick goo realigns when an unslicked card above it grows', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression slick layout');
  const upperCardTitle = 'Short unslicked card';
  const expandedUpperCardTitle = 'A much longer unslicked card title that wraps across several lines and makes the card noticeably taller';
  const slickCardTitle = 'Slicked card below';
  const slick = await api.createSlick(board, 'Layout regression slick');
  await api.createCard(board, 'Todo', slickCardTitle, '', [], slick.name);
  await api.createCard(board, 'Todo', upperCardTitle);
  const boardPage = new BoardPage(page);

  await boardPage.open(board.id);

  const upperCard = boardPage.card('Todo', upperCardTitle);
  const targetCard = boardPage.card('Todo', slickCardTitle);

  const initialUpperCardBox = await requireBoundingBox(upperCard);
  const initialTargetCardBox = await requireBoundingBox(targetCard);

  await aligned(targetCard);

  await boardPage.openCard('Todo', upperCardTitle);
  await boardPage.renameOpenCard(expandedUpperCardTitle);
  await boardPage.saveOpenCard();

  const expandedUpperCard = boardPage.card('Todo', expandedUpperCardTitle);
  await expect.poll(async () => (await requireBoundingBox(expandedUpperCard)).height)
    .toBeGreaterThan(initialUpperCardBox.height);
  await expect.poll(async () => (await requireBoundingBox(targetCard)).y)
    .toBeGreaterThan(initialTargetCardBox.y);
  await aligned(targetCard);
});

async function requireBoundingBox(locator: Locator) {
  const boundingBox = await locator.boundingBox();
  expect(boundingBox).not.toBeNull();
  return boundingBox!;
}
