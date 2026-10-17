import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { controlLayoutAnimations, seekLayoutAnimations } from '../support/layoutAnimations';
import { holdCardMoveResponse } from '../support/cardMovementResponse';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { BoardPage } from '../ui/BoardPage';

for (const renderer of ['full', 'lite']) {
  test(`realtime movement reorders cards and ${renderer} goo together`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Reorder ${renderer}`);
    const slick = await api.createSlick(board, 'Moving slick');
    const moving = await api.createCard(board, 'Todo', 'Moving card', '', [], slick.name);
    await api.createCard(board, 'Todo', 'Stationary neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    const card = bp.card('Todo', 'Moving card');
    const neighbour = bp.card('Todo', 'Stationary neighbour');
    const before = await box(card);
    const neighbourBefore = await box(neighbour);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
    await api.moveCard(board.id, moving.id, moving.boardColumnId);
    await expect.poll(() => seekLayoutAnimations(page, 0)).toBe(2);
    expect((await box(card)).y).toBeCloseTo(before.y, 0);
    expect((await box(neighbour)).y).toBeCloseTo(neighbourBefore.y, 0);
    await seekLayoutAnimations(page, 0.5);
    const halfway = await box(card);
    expect(halfway.y).toBeLessThan(before.y);
    expect((await box(neighbour)).y).toBeGreaterThan(neighbourBefore.y);
    await aligned(card, page.locator(`[data-goo-blob-id="card-${moving.id}"]`));
    await seekLayoutAnimations(page, 1);
    await expect.poll(async () => (await box(card)).y).toBeLessThan(halfway.y);
    await card.click();
    await expect(page.getByRole('dialog')).toBeVisible();
  });

  test(`realtime movement crosses columns without clipping and restores ${renderer} goo on landing`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Cross-column ${renderer}`);
    const slick = await api.createSlick(board, 'Travelling slick');
    await api.createCard(board, 'Todo', 'Source neighbour', '', [], slick.name);
    const moving = await api.createCard(board, 'Todo', 'Travelling card', '', [], slick.name);
    await api.createCard(board, 'In Progress', 'Destination neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    const before = await box(bp.card('Todo', 'Travelling card'));
    const sourceBefore = await box(bp.card('Todo', 'Source neighbour'));
    const destinationBefore = await box(bp.card('In Progress', 'Destination neighbour'));
    const targetColumnId = board.columns.find(column => column.title === 'In Progress')!.id;
    await api.moveCard(board.id, moving.id, targetColumnId);
    const floating = page.locator(`[data-moving-card-id="${moving.id}"]`);
    await expect(floating).toHaveCount(1);
    // Goo resolves the registered presentation, not a duplicate card identity.
    await expect(floating).not.toHaveAttribute('data-card-id');
    expect(await seekLayoutAnimations(page, 0)).toBeGreaterThanOrEqual(3);
    expect((await box(floating)).x).toBeCloseTo(before.x, 0);
    expect((await box(floating)).y).toBeCloseTo(before.y, 0);
    expect((await box(bp.card('Todo', 'Source neighbour'))).y).toBeCloseTo(sourceBefore.y, 0);
    expect((await box(bp.card('In Progress', 'Destination neighbour'))).y).toBeCloseTo(destinationBefore.y, 0);
    await seekLayoutAnimations(page, 0.5);
    const halfway = await box(floating);
    expect(halfway.x).toBeGreaterThan(before.x);
    expect(halfway.x).toBeLessThan(destinationBefore.x);
    const sourceNeighbour = bp.card('Todo', 'Source neighbour');
    expect(await sourceNeighbour.evaluate(element => {
      const card = element.getBoundingClientRect();
      const clip = element.closest('.column-content')!.getBoundingClientRect();
      return card.bottom - clip.bottom;
    })).toBeLessThanOrEqual(1);
    const movingBlob = page.locator(`[data-goo-blob-id="card-${moving.id}"]`);
    if (renderer === 'full') {
      await aligned(floating, movingBlob);
      // The travelling card stays in the same filtered group as its slick.
      expect(await movingBlob.evaluate(element => element.parentElement!.querySelectorAll('[data-goo-blob-id^="card-"]').length)).toBe(3);
    } else {
      await expect(movingBlob).toHaveCount(0);
      await expect(page.locator('.board > svg.goo-layer--lite')).toHaveCount(0);
    }
    // The floating presentation must really paint across the column boundary.
    expect(await floating.evaluate(element => getComputedStyle(element).visibility)).toBe('visible');
    await seekLayoutAnimations(page, 1);
    await expect(floating).toHaveCount(0);
    const saved = bp.card('In Progress', 'Travelling card');
    await expect(saved).toBeVisible();
    await aligned(saved, movingBlob);
    await saved.click();
    await expect(page.getByRole('dialog')).toBeVisible();
  });

  for (const delivery of ['realtime-first', 'http-only']) {
    test(`local drop animates once for ${delivery} delivery with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
      const board = await api.createBoard(`Drop ${renderer} ${delivery}`);
      const moving = await api.createCard(board, 'Todo', 'Dropped card');
      await api.createCard(board, 'In Progress', 'Drop target');
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await controlLayoutAnimations(page);
      const connection = await interceptBoardConnection(page);
      await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
      const bp = new BoardPage(page);
      await expect(bp.card('Todo', 'Dropped card')).toBeVisible();
      if (delivery === 'http-only') {
        connection.pauseEvents();
      }
      const response = await holdCardMoveResponse(page, board.id, moving.id);
      try {
        await bp.card('Todo', 'Dropped card').dragTo(bp.card('In Progress', 'Drop target'), { targetPosition: { x: 20, y: 5 } });
        await response.ready;
        const floating = page.locator(`[data-moving-card-id="${moving.id}"]`);
        if (delivery === 'realtime-first') {
          await expect(floating).toHaveCount(1);
          await seekLayoutAnimations(page, 1);
          await expect(floating).toHaveCount(0);
        }
        const httpResponse = page.waitForResponse(result => result.url().endsWith(`/cards/${moving.id}/move`));
        response.release();
        await (await httpResponse).finished();
        if (delivery === 'http-only') {
          await expect(floating).toHaveCount(1);
          await seekLayoutAnimations(page, 1);
        }
        await expect(bp.card('In Progress', 'Dropped card')).toBeVisible();
        expect(await seekLayoutAnimations(page, 0)).toBe(0);
      } finally {
        response.release();
      }
    });
  }

  test(`selection goo restores on landing with ${renderer} rendering`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Selected movement ${renderer}`);
    const moving = await api.createCard(board, 'Todo', 'Selected traveller');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await bp.enterCardSelectionMode();
    await bp.selectCard('Todo', 'Selected traveller');
    await api.moveCard(board.id, moving.id, board.columns.find(column => column.title === 'In Progress')!.id);
    const floating = page.locator(`[data-moving-card-id="${moving.id}"]`);
    await expect(floating).toHaveCount(1);
    await seekLayoutAnimations(page, 0.5);
    const selectionBlob = page.locator(`[data-goo-blob-id="selection-card-${moving.id}"]`);
    if (renderer === 'full') {
      await aligned(floating, selectionBlob);
    } else {
      await expect(selectionBlob).toHaveCount(0);
    }
    expect(await floating.evaluate(element => element.closest('.column-content') === null)).toBe(true);
    await seekLayoutAnimations(page, 1);
    await expect(floating).toHaveCount(0);
    await expect(bp.selectionModeCard('In Progress', 'Selected traveller')).toHaveAttribute('aria-checked', 'true');
    await aligned(bp.selectionModeCard('In Progress', 'Selected traveller'), selectionBlob);
  });
}

test('another arrival retargets a card already travelling into that column without clipping it', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Overlapping movement');
  const slick = await api.createSlick(board, 'Retargeted slick');
  const first = await api.createCard(board, 'Todo', 'First traveller', '', [], slick.name);
  const second = await api.createCard(board, 'Todo', 'Second traveller', '', [], slick.name);
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await expect(bp.card('Todo', 'First traveller')).toBeVisible();
  const destinationId = board.columns.find(column => column.title === 'In Progress')!.id;
  await api.moveCard(board.id, first.id, destinationId);
  const firstCopy = page.locator(`[data-moving-card-id="${first.id}"]`);
  await expect(firstCopy).toHaveCount(1);
  await seekLayoutAnimations(page, 0.3);
  const firstBlob = page.locator(`[data-goo-blob-id="card-${first.id}"]`);
  await aligned(firstCopy, firstBlob);
  const before = await box(firstCopy);
  await api.moveCard(board.id, second.id, destinationId);
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(2);
  await seekLayoutAnimations(page, 0);
  expect((await box(firstCopy)).x).toBeCloseTo(before.x, 0);
  expect((await box(firstCopy)).y).toBeCloseTo(before.y, 0);
  await aligned(firstCopy, firstBlob);
  await seekLayoutAnimations(page, 0.5);
  await aligned(firstCopy, firstBlob);
  await seekLayoutAnimations(page, 1);
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
  await expect(bp.card('In Progress', 'First traveller')).toBeVisible();
  await expect(bp.card('In Progress', 'Second traveller')).toBeVisible();
  await aligned(bp.card('In Progress', 'First traveller'), firstBlob);
});

test('a bulk drop moves all selected cards and leaves no floating copies', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Bulk movement');
  const first = await api.createCard(board, 'Todo', 'First selected card');
  const second = await api.createCard(board, 'Todo', 'Second selected card');
  await api.createCard(board, 'In Progress', 'Bulk drop target');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'First selected card');
  await bp.selectCard('Todo', 'Second selected card');
  await bp.selectionModeCard('Todo', 'First selected card').dragTo(bp.selectionModeCard('In Progress', 'Bulk drop target'));
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(2);
  await seekLayoutAnimations(page, 0.5);
  await expect(page.locator(`[data-moving-card-id="${first.id}"]`)).toBeVisible();
  await expect(page.locator(`[data-moving-card-id="${second.id}"]`)).toBeVisible();
  await seekLayoutAnimations(page, 1);
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
  await expect(bp.card('In Progress', 'First selected card')).toBeVisible();
  await expect(bp.card('In Progress', 'Second selected card')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
});

test('scrolling settles movement and an offscreen source does not float across headers', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Movement scrolling');
  const moving = await api.createCard(board, 'Todo', 'Scrolling traveller');
  for (let index = 0; index < 15; index++) {
    await api.createCard(board, 'In Progress', `Destination card ${index}`);
  }
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await expect(bp.card('Todo', 'Scrolling traveller')).toBeVisible();
  const inProgress = board.columns.find(column => column.title === 'In Progress')!.id;
  await api.moveCard(board.id, moving.id, inProgress);
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(1);
  await page.locator(`[data-column-id="${inProgress}"]`).evaluate(element => { element.scrollTop = 200; });
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  await api.moveCard(board.id, moving.id, moving.boardColumnId);
  await expect(bp.card('Todo', 'Scrolling traveller')).toBeVisible();
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
  await seekLayoutAnimations(page, 1);
});

test('an interrupted cross-column move continues from its visible position and reduced motion settles it', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Interrupted movement');
  const moving = await api.createCard(board, 'Todo', 'Interrupted card');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await expect(bp.card('Todo', 'Interrupted card')).toBeVisible();
  const inProgress = board.columns.find(column => column.title === 'In Progress')!.id;
  await api.moveCard(board.id, moving.id, inProgress);
  const floating = page.locator(`[data-moving-card-id="${moving.id}"]`);
  await expect(floating).toHaveCount(1);
  await seekLayoutAnimations(page, 0.3);
  const interrupted = await box(floating);
  await api.moveCard(board.id, moving.id, moving.boardColumnId);
  await expect(page.locator(`[data-column-id="${moving.boardColumnId}"] > [data-card-id="${moving.id}"]`)).toHaveCount(1);
  await seekLayoutAnimations(page, 0);
  expect((await box(floating)).x).toBeCloseTo(interrupted.x, 0);
  expect((await box(floating)).y).toBeCloseTo(interrupted.y, 0);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(floating).toHaveCount(0);
  await expect(bp.card('Todo', 'Interrupted card')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  await api.moveCard(board.id, moving.id, inProgress);
  await expect(bp.card('In Progress', 'Interrupted card')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
});

test('filtering out a travelling card removes its presentation and unmount cleans up movement', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Movement cleanup');
  const moving = await api.createCard(board, 'Todo', 'Travelling card');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await expect(bp.card('Todo', 'Travelling card')).toBeVisible();
  const inProgress = board.columns.find(column => column.title === 'In Progress')!.id;
  await api.moveCard(board.id, moving.id, inProgress);
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(1);
  await page.getByPlaceholder('Search', { exact: true }).fill('No match');
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
  await bp.clearCardFilters();
  await expect(bp.card('In Progress', 'Travelling card')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  await api.moveCard(board.id, moving.id, moving.boardColumnId);
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(1);
  await bp.openArchivedCards();
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
});

async function box(locator: Locator) {
  const value = await locator.boundingBox();
  expect(value).not.toBeNull();
  return value!;
}

async function aligned(card: Locator, blob: Locator) {
  await expect(blob).toHaveCount(1);
  await expect.poll(async () => {
    const a = await box(card);
    const b = await box(blob);
    return Math.abs(a.x + a.width / 2 - b.x - b.width / 2)
      + Math.abs(a.y + a.height / 2 - b.y - b.height / 2);
  }).toBeLessThanOrEqual(2);
}
