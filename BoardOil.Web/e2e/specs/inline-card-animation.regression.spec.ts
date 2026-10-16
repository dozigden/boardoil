import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { controlLayoutAnimations, seekLayoutAnimations } from '../support/layoutAnimations';
import { holdCardCreationResponse } from '../support/cardCreationResponse';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { BoardPage } from '../ui/BoardPage';

for (const renderer of ['full', 'lite']) {
  test(`inline creation moves cards and ${renderer} goo together`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Inline animation ${renderer}`);
    const slick = await api.createSlick(board, 'Animated slick');
    const existing = await api.createCard(board, 'Todo', 'Existing slick card', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    // Isolate the in-place height handoff; early realtime delivery is covered below.
    const connection = await interceptBoardConnection(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const boardPage = new BoardPage(page);
    const column = boardPage.column('Todo');
    const card = boardPage.card('Todo', 'Existing slick card');
    const blob = page.locator(`[data-goo-blob-id="card-${existing.id}"]`);
    await expect(card).toBeVisible();
    connection.pauseEvents();
    const initialY = (await box(card)).y;

    await test.step('opening makes space with immediate focus', async () => {
      await column.getByRole('button', { name: 'Add default card' }).click();
      await expect(column.getByPlaceholder('Title')).toBeFocused();
      expect(await seekLayoutAnimations(page, 0.5)).toBe(1);
      const halfwayY = (await box(card)).y;
      expect(halfwayY).toBeGreaterThan(initialY);
      await expectGooAligned(card, blob);
      await seekLayoutAnimations(page, 1);
      await expect.poll(async () => (await box(card)).y).toBeGreaterThan(halfwayY);
      await expectGooAligned(card, blob);
    });

    await test.step('cancelling closes the space', async () => {
      const openY = (await box(card)).y;
      await column.getByPlaceholder('Title').press('Escape');
      expect(await seekLayoutAnimations(page, 0.5)).toBe(1);
      const halfwayY = (await box(card)).y;
      expect(halfwayY).toBeGreaterThan(initialY);
      expect(halfwayY).toBeLessThan(openY);
      await expectGooAligned(card, blob);
      await seekLayoutAnimations(page, 1);
      await expect(column.getByPlaceholder('Title')).toHaveCount(0);
      await expect.poll(async () => (await box(card)).y).toBeCloseTo(initialY, 0);
    });

    await test.step('saving replaces the editor without collapsing its space first', async () => {
      await column.getByRole('button', { name: 'Add default card' }).click();
      await seekLayoutAnimations(page, 1);
      const title = column.getByPlaceholder('Title');
      await title.fill('New animated card');
      const beforeSaveY = (await box(card)).y;
      await title.press('Enter');
      const savedCard = boardPage.card('Todo', 'New animated card');
      await expect(savedCard).toBeVisible();
      await expect(title).toHaveCount(0);
      expect(await seekLayoutAnimations(page, 0)).toBe(1);
      expect(Math.abs((await box(card)).y - beforeSaveY)).toBeLessThanOrEqual(1);
      const startHeight = (await box(savedCard)).height;
      await seekLayoutAnimations(page, 0.5);
      const middleHeight = (await box(savedCard)).height;
      await expectGooAligned(card, blob);
      await seekLayoutAnimations(page, 1);
      await expect.poll(async () => (await box(savedCard)).height).toBeLessThan(middleHeight);
      expect(middleHeight).toBeLessThan(startHeight);
      await expectGooAligned(card, blob);
      await savedCard.click();
      await expect(page.getByRole('dialog')).toBeVisible();
    });
  });

  test(`saving a filtered-out card closes the editor space with ${renderer} goo aligned`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Filtered inline creation ${renderer}`);
    const slick = await api.createSlick(board, 'Visible slick');
    const existing = await api.createCard(board, 'Todo', 'Visible existing card', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const boardPage = new BoardPage(page);
    const column = boardPage.column('Todo');
    const card = boardPage.card('Todo', 'Visible existing card');
    const blob = page.locator(`[data-goo-blob-id="card-${existing.id}"]`);
    await expect(card).toBeVisible();
    await page.getByPlaceholder('Search', { exact: true }).fill('Visible');
    const initialY = (await box(card)).y;
    await column.getByRole('button', { name: 'Add default card' }).click();
    await seekLayoutAnimations(page, 1);
    const title = column.getByPlaceholder('Title');
    await title.fill('Hidden new card');
    const openY = (await box(card)).y;
    await title.press('Enter');
    await expect.poll(() => seekLayoutAnimations(page, 0)).toBe(1);
    expect((await box(card)).y).toBeCloseTo(openY, 0);
    await seekLayoutAnimations(page, 0.5);
    const halfwayY = (await box(card)).y;
    expect(halfwayY).toBeGreaterThan(initialY);
    expect(halfwayY).toBeLessThan(openY);
    await expectGooAligned(card, blob);
    await seekLayoutAnimations(page, 1);
    await expect(title).toHaveCount(0);
    await expect.poll(async () => (await box(card)).y).toBeCloseTo(initialY, 0);
    await expectGooAligned(card, blob);
    await expect(boardPage.card('Todo', 'Hidden new card')).toHaveCount(0);
    await boardPage.clearCardFilters();
    await expect(boardPage.card('Todo', 'Hidden new card')).toBeVisible();
  });

  test(`an early realtime card replaces the editor in place with ${renderer} goo aligned`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Early inline realtime ${renderer}`);
    const slick = await api.createSlick(board, 'Existing slick');
    const existing = await api.createCard(board, 'Todo', 'Existing slick card', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const boardPage = new BoardPage(page);
    const column = boardPage.column('Todo');
    const card = boardPage.card('Todo', 'Existing slick card');
    const blob = page.locator(`[data-goo-blob-id="card-${existing.id}"]`);
    await expect(card).toBeVisible();
    await column.getByRole('button', { name: 'Add default card' }).click();
    await seekLayoutAnimations(page, 1);
    const title = column.getByPlaceholder('Title');
    await title.fill('Early realtime card');
    const editorY = (await box(column.getByRole('article'))).y;
    const existingY = (await box(card)).y;
    const response = await holdCardCreationResponse(page, board.id);
    try {
      await title.press('Enter');
      const savedCard = boardPage.card('Todo', 'Early realtime card');
      await response.ready;
      // This later event on the same connection proves realtime has delivered
      // the local create. Other columns must keep updating during the save.
      await api.createCard(board, 'In Progress', 'Realtime delivery confirmed');
      await expect(boardPage.card('In Progress', 'Realtime delivery confirmed')).toHaveCount(1);
      await seekLayoutAnimations(page, 1);
      await expect(boardPage.card('In Progress', 'Realtime delivery confirmed')).toBeVisible();
      await expect(savedCard).toHaveCount(0);
      await expect(title).toHaveValue('Early realtime card');
      expect((await box(card)).y).toBeCloseTo(existingY, 0);
      response.release();
      await expect(title).toHaveCount(0);
      await expect(savedCard).toBeVisible();
      expect(await seekLayoutAnimations(page, 0)).toBe(1);
      expect((await box(savedCard)).y).toBeCloseTo(editorY, 0);
      expect((await box(card)).y).toBeCloseTo(existingY, 0);
      const startHeight = (await box(savedCard)).height;
      await seekLayoutAnimations(page, 0.5);
      const middleHeight = (await box(savedCard)).height;
      expect(middleHeight).toBeLessThan(startHeight);
      expect((await box(savedCard)).y).toBeCloseTo(editorY, 0);
      await expectGooAligned(card, blob);
      await seekLayoutAnimations(page, 1);
      await expect.poll(async () => (await box(savedCard)).height).toBeLessThan(middleHeight);
      expect((await box(savedCard)).y).toBeCloseTo(editorY, 0);
      await expectGooAligned(card, blob);
      await savedCard.click();
      await expect(page.getByRole('dialog')).toBeVisible();
    } finally {
      response.release();
    }
  });
}

test('clearing filters during a pending save reveals existing cards immediately', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Inline save filter changes');
  await api.createCard(board, 'Todo', 'Visible existing card');
  await api.createCard(board, 'Todo', 'Previously hidden card');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const hiddenCard = boardPage.card('Todo', 'Previously hidden card');
  await expect(hiddenCard).toBeVisible();
  await page.getByPlaceholder('Search', { exact: true }).fill('Visible');
  await expect(hiddenCard).toHaveCount(0);

  const column = boardPage.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  const title = column.getByPlaceholder('Title');
  await title.fill('Visible new card');
  const response = await holdCardCreationResponse(page, board.id);
  try {
    await title.press('Enter');
    await response.ready;
    await boardPage.clearCardFilters();
    await expect(hiddenCard).toBeVisible();
    await expect(title).toHaveValue('Visible new card');
    await expect(boardPage.card('Todo', 'Visible new card')).toHaveCount(0);
    response.release();
    await expect(title).toHaveCount(0);
    await expect(boardPage.card('Todo', 'Visible new card')).toBeVisible();
    await expect(hiddenCard).toBeVisible();
  } finally {
    response.release();
  }
});

test('a pending save does not close a newly opened draft after cancellation', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Inline save cancellation');
  await controlLayoutAnimations(page);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const column = boardPage.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  await seekLayoutAnimations(page, 1);
  const title = column.getByPlaceholder('Title');
  await title.fill('Already submitted card');
  const response = await holdCardCreationResponse(page, board.id);
  try {
    await title.press('Enter');
    await response.ready;
    await title.press('Escape');
    await seekLayoutAnimations(page, 1);
    await expect(title).toHaveCount(0);
    await column.getByRole('button', { name: 'Add default card' }).click();
    await seekLayoutAnimations(page, 1);
    await title.fill('Next draft');
    response.release();
    await expect(boardPage.card('Todo', 'Already submitted card')).toBeVisible();
    await expect(title).toHaveValue('Next draft');
    await expect(title).toBeFocused();
    await page.unroute(`**/api/boards/${board.id}/cards`);
    await title.press('Enter');
    await expect(title).toHaveCount(0);
    await expect(boardPage.card('Todo', 'Next draft')).toBeVisible();
    await seekLayoutAnimations(page, 1);
  } finally {
    response.release();
  }
});

test('inline creation honours reduced motion and retains invalid drafts', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Inline animation reduced motion');
  await api.createCard(board, 'Todo', 'Existing card');
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await controlLayoutAnimations(page);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const column = boardPage.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  const title = column.getByPlaceholder('Title');
  await expect(title).toBeFocused();
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
  await title.fill('Keep this draft');
  await page.route(`**/api/boards/${board.id}/cards`, route => route.fulfill({
    status: 400,
    json: { success: false, message: 'Invalid card.', validationErrors: { title: ['Try another title.'] } }
  }));
  await title.press('Enter');
  await expect(column.getByRole('alert')).toHaveText('Try another title.');
  await expect(title).toHaveValue('Keep this draft');
  await expect(title).toBeFocused();
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
  await title.press('Escape');
  await expect(title).toHaveCount(0);
  await column.getByRole('button', { name: 'Add default card' }).click();
  await title.fill('Saved without motion');
  await page.unroute(`**/api/boards/${board.id}/cards`);
  await title.press('Enter');
  await expect(boardPage.card('Todo', 'Saved without motion')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
});

test('an opening draft can be cancelled, reopened and unmounted mid-animation', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Inline animation interruptions');
  await api.createCard(board, 'Todo', 'Existing card');
  await controlLayoutAnimations(page);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const column = boardPage.column('Todo');
  const card = boardPage.card('Todo', 'Existing card');
  const initialY = (await box(card)).y;
  await column.getByRole('button', { name: 'Add default card' }).click();
  await seekLayoutAnimations(page, 0.25);
  const interruptedY = (await box(card)).y;
  await column.getByPlaceholder('Title').press('Escape');
  await seekLayoutAnimations(page, 0);
  expect((await box(card)).y).toBeCloseTo(interruptedY, 0);
  await column.getByRole('button', { name: 'Add default card' }).click();
  await expect(column.getByPlaceholder('Title')).toBeFocused();
  await seekLayoutAnimations(page, 1);
  await column.getByPlaceholder('Title').press('Escape');
  await seekLayoutAnimations(page, 1);
  await expect.poll(async () => (await box(card)).y).toBeCloseTo(initialY, 0);
  await column.getByRole('button', { name: 'Add default card' }).click();
  await page.getByRole('button', { name: 'View archived cards' }).click();
  await expect(page).toHaveURL(/\/archived$/);
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
});

test('typing resizes immediately and switching to reduced motion completes a closing animation', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Inline animation immediate typing');
  await api.createCard(board, 'Todo', 'Existing card');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const boardPage = new BoardPage(page);
  await boardPage.open(board.id);
  const column = boardPage.column('Todo');
  await column.getByRole('button', { name: 'Add default card' }).click();
  await seekLayoutAnimations(page, 0.25);
  const title = column.getByPlaceholder('Title');
  const shortHeight = (await box(title)).height;
  await title.fill('A long title that wraps over several lines and must remain fully visible even when typing starts during the opening animation');
  await expect.poll(async () => (await box(title)).height).toBeGreaterThan(shortHeight);
  await expect(title).toBeFocused();
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
  await title.press('Escape');
  expect(await seekLayoutAnimations(page, 0.5)).toBe(1);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(title).toHaveCount(0);
  expect(await seekLayoutAnimations(page, 0.5)).toBe(0);
});

async function box(locator: Locator) {
  const result = await locator.boundingBox();
  expect(result).not.toBeNull();
  return result!;
}

async function expectGooAligned(card: Locator, blob: Locator) {
  await expect.poll(async () => {
    const cardBox = await box(card);
    const blobBox = await box(blob);
    return Math.abs(cardBox.y + cardBox.height / 2 - blobBox.y - blobBox.height / 2);
  }).toBeLessThanOrEqual(1);
}
