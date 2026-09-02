import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('slick goo realigns when an unslicked card above it grows', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Regression slick layout');
  const upperCardTitle = 'Short unslicked card';
  const expandedUpperCardTitle = 'A much longer unslicked card title that wraps across several lines and makes the card noticeably taller';
  const slickCardTitle = 'Slicked card below';
  const slick = await api.createSlick(board, 'Layout regression slick');
  const slickCard = await api.createCard(board, 'Todo', slickCardTitle, '', [], slick.name);
  await api.createCard(board, 'Todo', upperCardTitle);
  const boardPage = new BoardPage(page);

  await boardPage.open(board.id);

  const upperCard = boardPage.card('Todo', upperCardTitle);
  const targetCard = boardPage.card('Todo', slickCardTitle);
  const targetBlob = page.locator(`[data-goo-blob-id="card-${slickCard.id}"]`);
  const initialUpperCardBox = await requireBoundingBox(upperCard);
  const initialTargetCardBox = await requireBoundingBox(targetCard);

  await expect(targetBlob).toBeVisible();
  await expect.poll(() => resolveVerticalCentreDifference(targetCard, targetBlob)).toBeLessThanOrEqual(1);

  await boardPage.openCard('Todo', upperCardTitle);
  await boardPage.renameOpenCard(expandedUpperCardTitle);
  await boardPage.saveOpenCard();

  const expandedUpperCard = boardPage.card('Todo', expandedUpperCardTitle);
  await expect.poll(async () => (await requireBoundingBox(expandedUpperCard)).height)
    .toBeGreaterThan(initialUpperCardBox.height);
  await expect.poll(async () => (await requireBoundingBox(targetCard)).y)
    .toBeGreaterThan(initialTargetCardBox.y);
  await expect.poll(() => resolveVerticalCentreDifference(targetCard, targetBlob)).toBeLessThanOrEqual(1);
});

async function resolveVerticalCentreDifference(card: Locator, blob: Locator) {
  const cardBox = await requireBoundingBox(card);
  const blobBox = await requireBoundingBox(blob);
  const cardCentre = cardBox.y + (cardBox.height / 2);
  const blobCentre = blobBox.y + (blobBox.height / 2);
  return Math.abs(cardCentre - blobCentre);
}

async function requireBoundingBox(locator: Locator) {
  const boundingBox = await locator.boundingBox();
  expect(boundingBox).not.toBeNull();
  return boundingBox!;
}
