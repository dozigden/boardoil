import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { controlLayoutAnimations, seekLayoutAnimations } from '../support/layoutAnimations';
import { holdCardCreationResponse } from '../support/cardCreationResponse';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { BoardPage } from '../ui/BoardPage';

for (const renderer of ['full', 'lite']) {
  for (const membership of ['same', 'different', 'none']) {
    test(`realtime removal collapses ${membership} slick spacing with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
      const board = await api.createBoard(`Removal ${renderer} ${membership}`);
      const slick = await api.createSlick(board, 'Neighbour slick');
      const neighbour = await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
      let departingSlick: string | null = slick.name;
      if (membership === 'different') { departingSlick = (await api.createSlick(board, 'Departing slick')).name; }
      if (membership === 'none') { departingSlick = null; }
      const removed = await api.createCard(board, 'Todo', 'Departing', '', [], departingSlick);
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await controlLayoutAnimations(page);
      await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
      const bp = new BoardPage(page);
      const card = page.locator(`[data-card-id="${removed.id}"]`);
      const survivor = bp.card('Todo', 'Neighbour');
      await expect(survivor).toBeVisible();
      const before = { card: await box(card), neighbour: await box(survivor), column: await box(bp.column('Todo')) };
      await api.deleteCards(board.id, [removed.id]);
      await expect(card).toHaveAttribute('data-removing-card', 'true');
      await expect(card).toHaveAttribute('inert', '');
      await expect(card).toHaveAttribute('draggable', 'false');
      await seekLayoutAnimations(page, 0);
      expect((await box(card)).height).toBeCloseTo(before.card.height, 0);
      expect((await box(bp.column('Todo'))).height).toBeCloseTo(before.column.height, 0);
      expect((await box(survivor)).y).toBeCloseTo(before.neighbour.y, 0);
      await seekLayoutAnimations(page, 0.5);
      const middle = await box(card);
      expect(middle.height).toBeGreaterThan(0);
      expect(middle.height).toBeLessThan(before.card.height);
      expect((await box(survivor)).y).toBeLessThan(before.neighbour.y);
      await aligned(survivor, page.locator(`[data-goo-blob-id="card-${neighbour.id}"]`));
      if (departingSlick) { await aligned(card, page.locator(`[data-goo-blob-id="card-${removed.id}"]`)); }
      await seekLayoutAnimations(page, 0.9999);
      const landing = await box(survivor);
      await seekLayoutAnimations(page, 1);
      await expect(card).toHaveCount(0);
      expect((await box(survivor)).y).toBeCloseTo(landing.y, 0);
      await expect(page.locator(`[data-goo-blob-id="card-${removed.id}"]`)).toHaveCount(0);
      expect(await seekLayoutAnimations(page, 0)).toBe(0);
    });
  }

  test(`bulk archive collapses adjacent cards and empties a column with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const connection = await interceptBoardConnection(page);
    const board = await api.createBoard(`Bulk removal ${renderer}`);
    const slick = await api.createSlick(board, 'Bulk slick');
    const a = await api.createCard(board, 'Todo', 'A', '', [], slick.name);
    const b = await api.createCard(board, 'Todo', 'B', '', [], slick.name);
    const c = await api.createCard(board, 'In Progress', 'C', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    await expect(page.locator(`[data-card-id="${a.id}"]`)).toBeVisible();
    connection.bufferEvents();
    await api.archiveCards(board.id, [a.id, b.id, c.id]);
    await expect.poll(() => connection.bufferedEventCount).toBe(3);
    connection.releaseEvents();
    await expect(page.locator('[data-removing-card]')).toHaveCount(3);
    await seekLayoutAnimations(page, 0.9999);
    const columns = page.locator('.column');
    const heights = await columns.evaluateAll(elements => elements.map(element => element.getBoundingClientRect().height));
    await seekLayoutAnimations(page, 1);
    await expect(page.locator('[data-card-id]')).toHaveCount(0);
    const finalHeights = await columns.evaluateAll(elements => elements.map(element => element.getBoundingClientRect().height));
    heights.forEach((height, index) => expect(finalHeights[index]).toBeCloseTo(height, 0));
    await expect(page.locator('[data-goo-blob-id]')).toHaveCount(0);
  });

  test(`deletion during cross-column travel cleans up continuously with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Travel removal ${renderer}`);
    const slick = await api.createSlick(board, 'Travel slick');
    const moving = await api.createCard(board, 'Todo', 'Traveller', '', [], slick.name);
    const neighbour = await api.createCard(board, 'In Progress', 'Neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await expect(bp.card('Todo', 'Traveller')).toBeVisible();
    await api.moveCard(board.id, moving.id, neighbour.boardColumnId);
    const copy = page.locator(`[data-moving-card-id="${moving.id}"]`);
    await expect(copy).toHaveCount(1);
    await seekLayoutAnimations(page, 0.35);
    const before = await box(copy);
    const neighbourBefore = await box(bp.card('In Progress', 'Neighbour'));
    await api.deleteCards(board.id, [moving.id]);
    await expect(page.locator(`[data-card-id="${moving.id}"]`)).toHaveAttribute('data-removing-card', 'true');
    await seekLayoutAnimations(page, 0);
    expect((await box(copy)).x).toBeCloseTo(before.x, 0);
    expect((await box(copy)).y).toBeCloseTo(before.y, 0);
    expect((await box(bp.card('In Progress', 'Neighbour'))).y).toBeCloseTo(neighbourBefore.y, 0);
    await seekLayoutAnimations(page, 0.5);
    expect((await box(copy)).height).toBeLessThan(before.height / 2);
    if (renderer === 'full') { await aligned(copy, page.locator(`[data-goo-blob-id="card-${moving.id}"]`)); }
    await seekLayoutAnimations(page, 0.9999);
    expect((await box(copy)).height).toBeLessThan(0.5);
    await seekLayoutAnimations(page, 1);
    await expect(copy).toHaveCount(0);
    await expect(page.locator(`[data-card-id="${moving.id}"]`)).toHaveCount(0);
    await aligned(bp.card('In Progress', 'Neighbour'), page.locator(`[data-goo-blob-id="card-${neighbour.id}"]`));
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`scrolled bulk deletion preserves its starting viewport with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const connection = await interceptBoardConnection(page);
    const board = await api.createBoard(`Scrolled bulk removal ${renderer}`);
    const slick = await api.createSlick(board, 'Scrolled slick');
    const cards = [];
    for (let index = 0; index < 18; index++) {
      cards.push(await api.createCard(board, 'Todo', `Card ${index}`, '', [], slick.name));
    }
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const column = new BoardPage(page).column('Todo');
    const scroller = column.locator('.column-content');
    const tail = page.locator(`[data-card-id="${cards[0]!.id}"]`);
    await expect(tail).toHaveCount(1);
    await scroller.evaluate(element => { element.scrollTop = element.scrollHeight; });
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    const scrollBefore = await scroller.evaluate(element => element.scrollTop);
    expect(scrollBefore).toBeGreaterThan(100);
    const tailBefore = await box(tail);

    connection.bufferEvents();
    await api.deleteCards(board.id, cards.slice(0, 12).map(card => card.id));
    await expect.poll(() => connection.bufferedEventCount).toBe(12);
    connection.releaseEvents();
    await expect(page.locator('[data-removing-card]')).toHaveCount(12);
    await seekLayoutAnimations(page, 0);
    // Allow the browser to deliver scroll events queued by endpoint measurement.
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    expect(await scroller.evaluate(element => element.scrollTop)).toBeCloseTo(scrollBefore, 0);
    expect((await box(tail)).y).toBeCloseTo(tailBefore.y, 0);
    expect(await column.evaluate(element => element.getAnimations().some(animation =>
      animation.effect instanceof KeyframeEffect && animation.effect.getKeyframes().some(frame => frame.height !== undefined)
    ))).toBe(true);
    await seekLayoutAnimations(page, 0.5);
    expect((await box(tail)).height).toBeLessThan(tailBefore.height);
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(column);
    await seekLayoutAnimations(page, 1);
    await expect(page.locator('[data-removing-card]')).toHaveCount(0);
    expect((await box(column)).height).toBeCloseTo(landing.height, 0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`deletion during creation starts at the displayed height with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Growing removal ${renderer}`);
    await api.createCard(board, 'Todo', 'Neighbour');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await expect(bp.card('Todo', 'Neighbour')).toBeVisible();
    const arriving = await api.createCard(board, 'Todo', 'Arrival');
    const card = page.locator(`[data-card-id="${arriving.id}"]`);
    await expect(card).toHaveCount(1);
    await seekLayoutAnimations(page, 0.2);
    const before = await box(card);
    const neighbourBefore = await box(bp.card('Todo', 'Neighbour'));
    await api.deleteCards(board.id, [arriving.id]);
    await expect(card).toHaveAttribute('data-removing-card', 'true');
    await seekLayoutAnimations(page, 0);
    expect((await box(card)).height).toBeCloseTo(before.height, 0);
    expect((await box(bp.card('Todo', 'Neighbour'))).y).toBeCloseTo(neighbourBefore.y, 0);
    await seekLayoutAnimations(page, 0.5);
    expect((await box(card)).height).toBeLessThan(before.height);
    await seekLayoutAnimations(page, 1);
    await expect(card).toHaveCount(0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`successive removals retarget an unfinished collapse with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Successive removal ${renderer}`);
    const second = await api.createCard(board, 'Todo', 'Second');
    const first = await api.createCard(board, 'Todo', 'First');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const a = page.locator(`[data-card-id="${first.id}"]`);
    const b = page.locator(`[data-card-id="${second.id}"]`);
    await expect(a).toBeVisible();
    await api.deleteCards(board.id, [first.id]);
    await expect(a).toHaveAttribute('data-removing-card', 'true');
    await seekLayoutAnimations(page, 0.35);
    const before = { a: await box(a), b: await box(b) };
    await api.deleteCards(board.id, [second.id]);
    await expect(b).toHaveAttribute('data-removing-card', 'true');
    await seekLayoutAnimations(page, 0);
    expect((await box(a)).height).toBeCloseTo(before.a.height, 0);
    expect((await box(a)).y).toBeCloseTo(before.a.y, 0);
    expect((await box(b)).y).toBeCloseTo(before.b.y, 0);
    await seekLayoutAnimations(page, 0.9999);
    const column = new BoardPage(page).column('Todo');
    const landing = await box(column);
    await seekLayoutAnimations(page, 1);
    await expect(page.locator('[data-card-id]')).toHaveCount(0);
    expect((await box(column)).height).toBeCloseTo(landing.height, 0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`creation and removal in one render share layout with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const connection = await interceptBoardConnection(page);
    const board = await api.createBoard(`Combined create/remove ${renderer}`);
    const removed = await api.createCard(board, 'Todo', 'Departing');
    const neighbour = await api.createCard(board, 'Todo', 'Neighbour');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const survivor = page.locator(`[data-card-id="${neighbour.id}"]`);
    await expect(survivor).toBeVisible();
    const before = await box(survivor);
    connection.bufferEvents();
    const arrival = await api.createCard(board, 'Todo', 'Arrival');
    await api.deleteCards(board.id, [removed.id]);
    await expect.poll(() => connection.bufferedEventCount).toBe(2);
    connection.releaseEvents();
    await expect(page.locator('[data-removing-card]')).toHaveCount(1);
    await seekLayoutAnimations(page, 0);
    expect((await box(survivor)).y).toBeCloseTo(before.y, 0);
    const newCard = page.locator(`[data-card-id="${arrival.id}"]`);
    expect((await box(newCard)).height).toBe(0);
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(survivor);
    await seekLayoutAnimations(page, 1);
    await expect(page.locator('[data-removing-card]')).toHaveCount(0);
    expect((await box(survivor)).y).toBeCloseTo(landing.y, 0);
    await expect(newCard).toBeVisible();
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`removing a tail card preserves column clipping with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Tail removal ${renderer}`);
    const removed = await api.createCard(board, 'Todo', 'Tail');
    const slick = await api.createSlick(board, 'Remaining slick');
    await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    const column = bp.column('Todo');
    const neighbour = bp.card('Todo', 'Neighbour');
    await expect(neighbour).toBeVisible();
    const before = { column: await box(column), neighbour: await box(neighbour) };
    await api.deleteCards(board.id, [removed.id]);
    await expect(page.locator('[data-removing-card]')).toHaveCount(1);
    await seekLayoutAnimations(page, 0);
    expect((await box(column)).height).toBeCloseTo(before.column.height, 0);
    expect((await box(neighbour)).y).toBeCloseTo(before.neighbour.y, 0);
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(column);
    await seekLayoutAnimations(page, 1);
    await expect(page.locator('[data-removing-card]')).toHaveCount(0);
    expect((await box(column)).height).toBeCloseTo(landing.height, 0);
  });

  test(`removing the last filter match lands on the empty message with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Filtered removal ${renderer}`);
    const removed = await api.createCard(board, 'Todo', 'Only match');
    await api.createCard(board, 'Todo', 'Hidden card');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const bp = new BoardPage(page);
    await expect(bp.card('Todo', 'Only match')).toBeVisible();
    await page.getByPlaceholder('Search', { exact: true }).fill('Only match');
    await expect(bp.card('Todo', 'Hidden card')).toHaveCount(0);
    const column = bp.column('Todo');
    const before = await box(column);
    await api.deleteCards(board.id, [removed.id]);
    await expect(page.locator('[data-removing-card]')).toHaveCount(1);
    await seekLayoutAnimations(page, 0);
    expect((await box(column)).height).toBeCloseTo(before.height, 0);
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(column);
    await seekLayoutAnimations(page, 1);
    await expect(page.locator('[data-removing-card]')).toHaveCount(0);
    await expect(column.getByText('No matching cards.', { exact: true })).toBeVisible();
    expect((await box(column)).height).toBeCloseTo(landing.height, 0);
  });
}

test('local deletion animates once when realtime is delivered before its HTTP response', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Local removal delivery');
  const card = await api.createCard(board, 'Todo', 'Delete locally');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.openCard('Todo', 'Delete locally');
  let release!: () => void;
  const held = new Promise<void>(resolve => { release = resolve; });
  await page.route(`**/api/boards/${board.id}/cards/delete`, async route => {
    const response = await route.fetch();
    await held;
    await route.fulfill({ response });
  });
  await bp.deleteOpenCard();
  const element = page.locator(`[data-card-id="${card.id}"]`);
  await expect(element).toHaveAttribute('data-removing-card', 'true');
  await seekLayoutAnimations(page, 1);
  await expect(element).toHaveCount(0);
  release();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
});

test('filters, reduced motion and teardown clean up departing cards', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Removal cleanup');
  const first = await api.createCard(board, 'Todo', 'Remove first');
  await api.createCard(board, 'Todo', 'Survivor');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await api.deleteCards(board.id, [first.id]);
  await expect(page.locator('[data-removing-card]')).toHaveCount(1);
  await page.getByPlaceholder('Search', { exact: true }).fill('Survivor');
  await expect(page.locator('[data-removing-card]')).toHaveCount(0);
  await bp.clearCardFilters();
  await expect(bp.card('Todo', 'Survivor')).toBeVisible();
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  const second = await api.createCard(board, 'Todo', 'Remove second');
  await expect(bp.card('Todo', 'Remove second')).toHaveCount(1);
  await seekLayoutAnimations(page, 1);
  await api.deleteCards(board.id, [second.id]);
  await expect(page.locator('[data-removing-card]')).toHaveCount(1);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await expect(page.locator('[data-removing-card]')).toHaveCount(0);
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  const third = await api.createCard(board, 'Todo', 'Remove third');
  await expect(bp.card('Todo', 'Remove third')).toBeVisible();
  await api.deleteCards(board.id, [third.id]);
  await expect(page.locator(`[data-card-id="${third.id}"]`)).toHaveCount(0);
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  const last = await api.createCard(board, 'Todo', 'Remove on navigation');
  await expect(bp.card('Todo', 'Remove on navigation')).toHaveCount(1);
  await seekLayoutAnimations(page, 1);
  await api.deleteCards(board.id, [last.id]);
  await expect(page.locator('[data-removing-card]')).toHaveCount(1);
  await page.getByRole('button', { name: 'View archived cards' }).click();
  await expect(page).toHaveURL(/\/archived$/);
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
});

test('a departing image card retains its displayed thumbnail while collapsing', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Image removal');
  const card = await api.createCard(board, 'Todo', 'Image card');
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
  await api.uploadAttachment(board.id, card.id, 'cover.png', 'image/png', png);
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  const element = page.locator(`[data-card-id="${card.id}"]`);
  const image = element.getByTestId('card-attachment-thumbnail');
  await expect(image).toBeVisible();
  await expect.poll(() => image.evaluate(element => (element as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);
  const imageUrl = await image.getAttribute('src');
  const before = await box(element);
  await api.deleteCards(board.id, [card.id]);
  await expect(element).toHaveAttribute('data-removing-card', 'true');
  await seekLayoutAnimations(page, 0);
  expect((await box(element)).height).toBeCloseTo(before.height, 0);
  await seekLayoutAnimations(page, 0.5);
  await expect(image).toBeVisible();
  await expect(image).toHaveAttribute('src', imageUrl!);
  await seekLayoutAnimations(page, 1);
  await expect(element).toHaveCount(0);
});

test('a failed delete leaves the card intact and HTTP-only success animates once', async ({ api, authenticatedPage: page }) => {
  const connection = await interceptBoardConnection(page);
  const board = await api.createBoard('HTTP-only deletion');
  const card = await api.createCard(board, 'Todo', 'Delete locally');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  connection.bufferEvents();
  await bp.openCard('Todo', 'Delete locally');
  const path = `**/api/boards/${board.id}/cards/delete`;
  await page.route(path, route => route.fulfill({
    status: 500, contentType: 'application/json', body: JSON.stringify({ success: false, message: 'Delete rejected.' })
  }));
  await bp.deleteOpenCard();
  await expect(page.getByText('Delete rejected.', { exact: true })).toBeVisible();
  const element = page.locator(`[data-card-id="${card.id}"]`);
  await expect(element).not.toHaveAttribute('data-removing-card');
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
  await page.unroute(path);
  await bp.deleteOpenCard();
  await expect(element).toHaveAttribute('data-removing-card', 'true');
  await seekLayoutAnimations(page, 1);
  await expect(element).toHaveCount(0);
  await expect.poll(() => connection.bufferedEventCount).toBe(1);
  connection.releaseEvents();
  // A subsequent marker proves the duplicate removal has been consumed.
  await api.createCard(board, 'In Progress', 'Delivery marker');
  await expect(bp.card('In Progress', 'Delivery marker')).toHaveCount(1);
  await expect(page.locator('[data-removing-card]')).toHaveCount(0);
  expect(await seekLayoutAnimations(page, 0)).toBe(1);
  await seekLayoutAnimations(page, 1);
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
});

test('removing a realtime arrival held behind a pending editor never reveals it', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Held arrival removal');
  await api.createCard(board, 'Todo', 'Neighbour');
  await page.emulateMedia({ reducedMotion: 'no-preference' });
  await controlLayoutAnimations(page);
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await bp.column('Todo').getByRole('button', { name: 'Add default card' }).click();
  await seekLayoutAnimations(page, 1);
  const response = await holdCardCreationResponse(page, board.id);
  const title = bp.column('Todo').getByPlaceholder('Title');
  await title.fill('Local saved card');
  await title.press('Enter');
  await response.ready;
  const arrival = await api.createCard(board, 'Todo', 'Held arrival');
  const id = arrival.id;
  await api.deleteCards(board.id, [id]);
  await api.createCard(board, 'In Progress', 'Delivery marker');
  await expect(bp.card('In Progress', 'Delivery marker')).toHaveCount(1);
  await expect(page.locator(`[data-card-id="${id}"]`)).toHaveCount(0);
  await expect(page.locator('[data-removing-card]')).toHaveCount(0);
  response.release();
  await expect(title).toHaveCount(0);
  await seekLayoutAnimations(page, 1);
  await expect(page.locator(`[data-card-id="${id}"]`)).toHaveCount(0);
  expect(await seekLayoutAnimations(page, 0)).toBe(0);
});

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
