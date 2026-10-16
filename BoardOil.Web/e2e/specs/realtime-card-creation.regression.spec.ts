import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { controlLayoutAnimations, seekLayoutAnimations } from '../support/layoutAnimations';
import { BoardPage } from '../ui/BoardPage';

for (const renderer of ['full', 'lite']) {
  for (const membership of ['none', 'same', 'different']) {
    test(`realtime creation animates ${membership} slick membership with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
      const board = await api.createBoard(`Realtime creation ${renderer} ${membership}`);
      const slick = await api.createSlick(board, 'Existing slick');
      const existing = await api.createCard(board, 'Todo', 'Existing card', '', [], slick.name);
      let arrivingSlick: string | null = null;
      if (membership === 'same') {
        arrivingSlick = slick.name;
      } else if (membership === 'different') {
        arrivingSlick = (await api.createSlick(board, 'Arriving slick')).name;
      }
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await controlLayoutAnimations(page);
      await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
      const boardPage = new BoardPage(page);
      const card = boardPage.card('Todo', 'Existing card');
      const blob = page.locator(`[data-goo-blob-id="card-${existing.id}"]`);
      await expect(card).toBeVisible();
      expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
      const initialY = (await box(card)).y;

      await api.createCard(board, 'Todo', 'Realtime arrival', '', [], arrivingSlick);
      const arrival = boardPage.card('Todo', 'Realtime arrival');
      await expect(arrival).toHaveCount(1);
      expect(await seekLayoutAnimations(page, 0)).toBe(1);
      expect((await box(card)).y).toBeCloseTo(initialY, 0);
      expect((await box(arrival)).height).toBe(0);
      await seekLayoutAnimations(page, 0.5);
      const middleY = (await box(card)).y;
      const middleHeight = (await box(arrival)).height;
      expect(middleY).toBeGreaterThan(initialY);
      expect(middleHeight).toBeGreaterThan(0);
      await expect.poll(async () => {
        const cardBox = await box(card);
        const blobBox = await box(blob);
        return Math.abs(cardBox.y + cardBox.height / 2 - blobBox.y - blobBox.height / 2);
      }).toBeLessThanOrEqual(1);
      await seekLayoutAnimations(page, 1);
      await expect.poll(async () => (await box(card)).y).toBeGreaterThan(middleY);
      expect((await box(arrival)).height).toBeGreaterThan(middleHeight);
      await arrival.click();
      await expect(page.getByRole('dialog')).toBeVisible();
    });
  }
}

test('realtime creation respects reduced motion and filter reveals stay immediate', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Realtime creation filters');
  await api.createCard(board, 'Todo', 'Visible existing card');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await controlLayoutAnimations(page);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  await api.createCard(board, 'Todo', 'Visible realtime card');
  await expect(boardPage.card('Todo', 'Visible realtime card')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);

  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await page.getByPlaceholder('Search', { exact: true }).fill('Visible');
  await api.createCard(board, 'Todo', 'Hidden arrival');
  // A later event proves the filtered-out creation has reached the same client.
  await api.createCard(board, 'In Progress', 'Visible delivery marker');
  const marker = boardPage.card('In Progress', 'Visible delivery marker');
  await expect(marker).toHaveCount(1);
  await seekLayoutAnimations(page, 1);
  await expect(marker).toBeVisible();
  await expect(boardPage.card('Todo', 'Hidden arrival')).toHaveCount(0);
  await boardPage.clearCardFilters();
  await expect(boardPage.card('Todo', 'Hidden arrival')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
});

async function box(locator: Locator) {
  const result = await locator.boundingBox();
  expect(result).not.toBeNull();
  return result!;
}
