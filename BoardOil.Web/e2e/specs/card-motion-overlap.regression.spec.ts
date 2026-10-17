import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { controlLayoutAnimations, seekLayoutAnimations } from '../support/layoutAnimations';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { holdCardCreationResponse } from '../support/cardCreationResponse';
import { BoardPage } from '../ui/BoardPage';

for (const renderer of ['full', 'lite']) {
  test(`a creation and reorder in one render preserve positions with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const connection = await interceptBoardConnection(page);
    const board = await api.createBoard(`Combined create/reorder ${renderer}`);
    const slick = await api.createSlick(board, 'Moving slick');
    const moving = await api.createCard(board, 'Todo', 'Moving card', '', [], slick.name);
    const neighbour = await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    const movedCard = bp.card('Todo', 'Moving card');
    const stationary = bp.card('Todo', 'Neighbour');
    const before = { moving: await box(movedCard), neighbour: await box(stationary) };

    connection.bufferEvents();
    await api.createCard(board, 'Todo', 'New arrival');
    await api.moveCard(board.id, moving.id, moving.boardColumnId);
    await expect.poll(() => connection.bufferedEventCount).toBe(2);
    connection.releaseEvents();
    const arrival = bp.card('Todo', 'New arrival');
    await expect(arrival).toHaveCount(1);
    await seekLayoutAnimations(page, 0);
    expect((await box(movedCard)).y).toBeCloseTo(before.moving.y, 0);
    expect((await box(stationary)).y).toBeCloseTo(before.neighbour.y, 0);
    expect((await box(arrival)).height).toBe(0);
    await seekLayoutAnimations(page, 0.5);
    const middle = { moving: await box(movedCard), neighbour: await box(stationary), arrival: await box(arrival) };
    await aligned(stationary, page.locator(`[data-goo-blob-id="card-${neighbour.id}"]`));
    await seekLayoutAnimations(page, 1);
    await expect.poll(async () => (await box(movedCard)).y).toBeLessThan(middle.moving.y);
    expect(middle.moving.y).toBeLessThan(before.moving.y);
    expect((await box(stationary)).y).toBeGreaterThan(middle.neighbour.y);
    expect(middle.neighbour.y).toBeGreaterThan(before.neighbour.y);
    expect((await box(arrival)).height).toBeGreaterThan(middle.arrival.height);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`a creation retargets an incoming card continuously with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Create during travel ${renderer}`);
    const slick = await api.createSlick(board, 'Travelling slick');
    const moving = await api.createCard(board, 'Todo', 'Travelling card', '', [], slick.name);
    await api.createCard(board, 'In Progress', 'Destination neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await expect(bp.card('Todo', 'Travelling card')).toBeVisible();
    await api.moveCard(board.id, moving.id, board.columns.find(column => column.title === 'In Progress')!.id);
    const copy = page.locator(`[data-moving-card-id="${moving.id}"]`);
    await expect(copy).toHaveCount(1);
    await seekLayoutAnimations(page, 0.35);
    const before = await box(copy);
    await api.createCard(board, 'In Progress', 'New arrival');
    await expect(bp.card('In Progress', 'New arrival')).toHaveCount(1);
    await seekLayoutAnimations(page, 0);
    expect((await box(copy)).x).toBeCloseTo(before.x, 0);
    expect((await box(copy)).y).toBeCloseTo(before.y, 0);
    await seekLayoutAnimations(page, 0.5);
    const blob = page.locator(`[data-goo-blob-id="card-${moving.id}"]`);
    if (renderer === 'full') {
      await aligned(copy, blob);
    } else {
      await expect(blob).toHaveCount(0);
    }
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(copy);
    await seekLayoutAnimations(page, 1);
    await expect(copy).toHaveCount(0);
    const saved = bp.card('In Progress', 'Travelling card');
    expect((await box(saved)).x).toBeCloseTo(landing.x, 0);
    expect((await box(saved)).y).toBeCloseTo(landing.y, 0);
    await aligned(saved, blob);
  });

  test(`a move preserves an unfinished creation with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Move during creation ${renderer}`);
    const slick = await api.createSlick(board, 'Growing slick');
    const moving = await api.createCard(board, 'Todo', 'Moving card', '', [], slick.name);
    await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await expect(bp.card('Todo', 'Moving card')).toBeVisible();
    await api.createCard(board, 'Todo', 'Growing arrival');
    const arrival = bp.card('Todo', 'Growing arrival');
    await expect(arrival).toHaveCount(1);
    await seekLayoutAnimations(page, 0.35);
    const before = { arrival: await box(arrival), moving: await box(bp.card('Todo', 'Moving card')), neighbour: await box(bp.card('Todo', 'Neighbour')) };
    await api.moveCard(board.id, moving.id, moving.boardColumnId);
    await seekLayoutAnimations(page, 0);
    expect((await box(arrival)).height).toBeCloseTo(before.arrival.height, 0);
    expect((await box(arrival)).y).toBeCloseTo(before.arrival.y, 0);
    expect((await box(bp.card('Todo', 'Moving card'))).y).toBeCloseTo(before.moving.y, 0);
    expect((await box(bp.card('Todo', 'Neighbour'))).y).toBeCloseTo(before.neighbour.y, 0);
    await seekLayoutAnimations(page, 0.5);
    await aligned(bp.card('Todo', 'Moving card'), page.locator(`[data-goo-blob-id="card-${moving.id}"]`));
    await seekLayoutAnimations(page, 1);
    await expect(arrival).toBeVisible();
    expect((await box(arrival)).height).toBeGreaterThan(before.arrival.height);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`a partially created card can travel between columns with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Growing traveller ${renderer}`);
    const slick = await api.createSlick(board, 'Growing traveller slick');
    await api.createCard(board, 'Todo', 'Source neighbour', '', [], slick.name);
    await api.createCard(board, 'In Progress', 'Destination neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await expect(bp.card('Todo', 'Source neighbour')).toBeVisible();
    const arriving = await api.createCard(board, 'Todo', 'Growing traveller', '', [], slick.name);
    const arrival = bp.card('Todo', 'Growing traveller');
    await expect(arrival).toHaveCount(1);
    await seekLayoutAnimations(page, 0.35);
    const before = await box(arrival);
    await api.moveCard(board.id, arriving.id, board.columns.find(column => column.title === 'In Progress')!.id);
    const copy = page.locator(`[data-moving-card-id="${arriving.id}"]`);
    await expect(copy).toHaveCount(1);
    await seekLayoutAnimations(page, 0);
    const start = await box(copy);
    expect(start.x).toBeCloseTo(before.x, 0);
    expect(start.y).toBeCloseTo(before.y, 0);
    expect(start.height).toBeCloseTo(before.height, 0);
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(copy);
    await seekLayoutAnimations(page, 1);
    await expect(copy).toHaveCount(0);
    const saved = bp.card('In Progress', 'Growing traveller');
    const final = await box(saved);
    expect(final.y).toBeCloseTo(landing.y, 0);
    expect(final.height).toBeCloseTo(landing.height, 0);
    await aligned(saved, page.locator(`[data-goo-blob-id="card-${arriving.id}"]`));
  });

  test(`opening and cancelling an editor retargets movement with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Draft during movement ${renderer}`);
    const slick = await api.createSlick(board, 'Draft neighbour slick');
    const moving = await api.createCard(board, 'Todo', 'Moving card', '', [], slick.name);
    const neighbour = await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await expect(bp.card('Todo', 'Moving card')).toBeVisible();
    await api.moveCard(board.id, moving.id, moving.boardColumnId);
    await expect.poll(() => seekLayoutAnimations(page, 0.35)).toBeGreaterThan(0);
    const beforeOpen = await box(bp.card('Todo', 'Moving card'));
    await bp.column('Todo').getByRole('button', { name: 'Add default card' }).click();
    const title = bp.column('Todo').getByPlaceholder('Title');
    await expect(title).toBeFocused();
    await seekLayoutAnimations(page, 0);
    expect((await box(bp.card('Todo', 'Moving card'))).y).toBeCloseTo(beforeOpen.y, 0);
    await seekLayoutAnimations(page, 0.35);
    const beforeClose = await box(bp.card('Todo', 'Moving card'));
    await title.press('Escape');
    await seekLayoutAnimations(page, 0);
    expect((await box(bp.card('Todo', 'Moving card'))).y).toBeCloseTo(beforeClose.y, 0);
    await seekLayoutAnimations(page, 0.5);
    await aligned(bp.card('Todo', 'Neighbour'), page.locator(`[data-goo-blob-id="card-${neighbour.id}"]`));
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(bp.card('Todo', 'Moving card'));
    await seekLayoutAnimations(page, 1);
    await expect(title).toHaveCount(0);
    expect((await box(bp.card('Todo', 'Moving card'))).y).toBeCloseTo(landing.y, 0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`an editor handoff retargets an unfinished move with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Handoff during movement ${renderer}`);
    const slick = await api.createSlick(board, 'Handoff slick');
    const moving = await api.createCard(board, 'Todo', 'Moving card', '', [], slick.name);
    const neighbour = await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    const column = bp.column('Todo');
    await column.getByRole('button', { name: 'Add default card' }).click();
    await seekLayoutAnimations(page, 1);
    const response = await holdCardCreationResponse(page, board.id);
    const title = column.getByPlaceholder('Title');
    await title.fill('Saved card');
    await title.press('Enter');
    await response.ready;
    await api.moveCard(board.id, moving.id, moving.boardColumnId);
    await expect.poll(() => seekLayoutAnimations(page, 0.35)).toBeGreaterThan(0);
    const before = { moving: await box(bp.card('Todo', 'Moving card')), neighbour: await box(bp.card('Todo', 'Neighbour')) };
    response.release();
    const saved = bp.card('Todo', 'Saved card');
    await expect(saved).toHaveCount(1);
    await expect(title).toHaveCount(0);
    await seekLayoutAnimations(page, 0);
    expect((await box(bp.card('Todo', 'Moving card'))).y).toBeCloseTo(before.moving.y, 0);
    expect((await box(bp.card('Todo', 'Neighbour'))).y).toBeCloseTo(before.neighbour.y, 0);
    await seekLayoutAnimations(page, 0.5);
    await aligned(bp.card('Todo', 'Neighbour'), page.locator(`[data-goo-blob-id="card-${neighbour.id}"]`));
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(bp.card('Todo', 'Neighbour'));
    await seekLayoutAnimations(page, 1);
    expect((await box(bp.card('Todo', 'Neighbour'))).y).toBeCloseTo(landing.y, 0);
    await saved.click();
    await expect(page.getByRole('dialog')).toBeVisible();
  });
}

async function box(locator: Locator) {
  const result = await locator.boundingBox();
  expect(result).not.toBeNull();
  return result!;
}

async function aligned(card: Locator, blob: Locator) {
  await expect.poll(async () => {
    const a = await box(card);
    const b = await box(blob);
    return Math.max(Math.abs(a.x + a.width / 2 - b.x - b.width / 2), Math.abs(a.y + a.height / 2 - b.y - b.height / 2));
  }).toBeLessThanOrEqual(1);
}
