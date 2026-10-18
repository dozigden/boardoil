import type { Locator } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { controlLayoutAnimations, seekLayoutAnimations } from '../support/layoutAnimations';
import { interceptBoardConnection } from '../support/boardRealtimeConnection';
import { holdCardUpdateResponse } from '../support/cardUpdateResponse';
import { BoardPage } from '../ui/BoardPage';

const longTitle = 'A longer saved card title that wraps onto several lines and makes this card noticeably taller than before';

for (const renderer of ['full', 'lite']) {
  for (const position of ['only card', 'tail card']) {
    test(`resizing a slicked ${position} preserves bottom spacing with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
      const board = await api.createBoard(`Tail resize ${renderer} ${position}`);
      const slick = await api.createSlick(board, 'Tail slick');
      let saved = await api.createCard(board, 'Todo', 'Short title', '', [], slick.name);
      if (position === 'tail card') {
        await api.createCard(board, 'Todo', 'Card above', '', [], slick.name);
      }
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await controlLayoutAnimations(page);
      await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
      const card = page.locator(`[data-card-id="${saved.id}"]`);
      const column = new BoardPage(page).column('Todo');
      const blob = page.locator(`[data-goo-blob-id="card-${saved.id}"]`);
      await expect(card).toBeVisible();
      const margin = await card.evaluate(element => getComputedStyle(element).marginBottom);
      expect(Number.parseFloat(margin)).toBeGreaterThan(0);

      for (const title of [longTitle, 'Short title']) {
        const before = { card: await box(card), column: await box(column) };
        saved = await api.updateCard(board.id, saved, { title });
        await expect(card).toContainText(title);
        expect(await seekLayoutAnimations(page, 0)).toBeGreaterThan(0);
        expect((await box(card)).height).toBeCloseTo(before.card.height, 0);
        expect((await box(column)).height).toBeCloseTo(before.column.height, 0);
        expect(await card.evaluate(element => getComputedStyle(element).marginBottom)).toBe(margin);
        await seekLayoutAnimations(page, 0.5);
        expect(await card.evaluate(element => getComputedStyle(element).marginBottom)).toBe(margin);
        await aligned(card, blob);
        await seekLayoutAnimations(page, 0.9999);
        const landing = { card: await box(card), column: await box(column) };
        await seekLayoutAnimations(page, 1);
        expect((await box(card)).height).toBeCloseTo(landing.card.height, 0);
        expect((await box(column)).height).toBeCloseTo(landing.column.height, 0);
        expect(await card.evaluate(element => getComputedStyle(element).marginBottom)).toBe(margin);
        expect(await seekLayoutAnimations(page, 0)).toBe(0);
      }
    });
  }

  for (const slicked of [true, false]) {
    test(`saved content grows and shrinks ${slicked ? 'inside' : 'outside'} slicks with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
      const board = await api.createBoard(`Resize ${renderer} ${slicked}`);
      const slick = await api.createSlick(board, 'Resizing slick');
      const neighbour = await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
      let saved = await api.createCard(board, 'Todo', 'Short title', '', [], slicked ? slick.name : null);
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await controlLayoutAnimations(page);
      await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
      const bp = new BoardPage(page);
      const card = page.locator(`[data-card-id="${saved.id}"]`);
      const below = page.locator(`[data-card-id="${neighbour.id}"]`);
      await expect(card).toBeVisible();
      expect(await seekLayoutAnimations(page, 0)).toBe(0);
      const before = { card: await box(card), below: await box(below), column: await box(bp.column('Todo')) };

      saved = await api.updateCard(board.id, saved, { title: longTitle });
      await expect(card).toContainText(longTitle);
      expect(await seekLayoutAnimations(page, 0)).toBeGreaterThan(0);
      expect((await box(card)).height).toBeCloseTo(before.card.height, 0);
      expect((await box(below)).y).toBeCloseTo(before.below.y, 0);
      expect((await box(bp.column('Todo'))).height).toBeCloseTo(before.column.height, 0);
      await seekLayoutAnimations(page, 0.5);
      const middle = { card: await box(card), below: await box(below) };
      expect(middle.card.height).toBeGreaterThan(before.card.height);
      expect(middle.below.y - before.below.y).toBeCloseTo(middle.card.height - before.card.height, 0);
      await aligned(below, page.locator(`[data-goo-blob-id="card-${neighbour.id}"]`));
      if (slicked) { await aligned(card, page.locator(`[data-goo-blob-id="card-${saved.id}"]`)); }
      await seekLayoutAnimations(page, 0.9999);
      const landing = await box(card);
      await seekLayoutAnimations(page, 1);
      expect((await box(card)).height).toBeCloseTo(landing.height, 0);
      expect(landing.height).toBeGreaterThan(middle.card.height);

      saved = await api.updateCard(board.id, saved, { title: 'Short title' });
      await expect(card).toContainText('Short title');
      await seekLayoutAnimations(page, 0);
      expect((await box(card)).height).toBeCloseTo(landing.height, 0);
      await seekLayoutAnimations(page, 0.5);
      expect((await box(card)).height).toBeLessThan(landing.height);
      expect((await box(card)).height).toBeGreaterThan(before.card.height);
      await aligned(below, page.locator(`[data-goo-blob-id="card-${neighbour.id}"]`));
      await seekLayoutAnimations(page, 1);
      expect((await box(card)).height).toBeCloseTo(before.card.height, 0);
      expect((await box(below)).y).toBeCloseTo(before.below.y, 0);
      expect(await seekLayoutAnimations(page, 0)).toBe(0);

      // New text with the same natural height and duplicate saved content stay idle.
      saved = await api.updateCard(board.id, saved, { title: 'Other title' });
      await expect(card).toContainText('Other title');
      expect(await seekLayoutAnimations(page, 0)).toBe(0);
      await api.updateCard(board.id, saved, {});
      expect(await seekLayoutAnimations(page, 0)).toBe(0);
    });
  }

  test(`another edit and deletion retarget an unfinished resize with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Retarget resize ${renderer}`);
    const slick = await api.createSlick(board, 'Resizing slick');
    const neighbour = await api.createCard(board, 'Todo', 'Neighbour', '', [], slick.name);
    let saved = await api.createCard(board, 'Todo', 'Short title', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const card = page.locator(`[data-card-id="${saved.id}"]`);
    const below = page.locator(`[data-card-id="${neighbour.id}"]`);
    await expect(card).toBeVisible();
    saved = await api.updateCard(board.id, saved, { title: longTitle });
    await expect(card).toContainText(longTitle);
    await seekLayoutAnimations(page, 0.3);
    const beforeEdit = { card: await box(card), below: await box(below) };
    saved = await api.updateCard(board.id, saved, { title: 'Short again' });
    await expect(card).toContainText('Short again');
    await seekLayoutAnimations(page, 0);
    expect((await box(card)).height).toBeCloseTo(beforeEdit.card.height, 0);
    expect((await box(below)).y).toBeCloseTo(beforeEdit.below.y, 0);
    await seekLayoutAnimations(page, 0.4);
    const beforeDelete = { card: await box(card), below: await box(below) };
    await api.deleteCards(board.id, [saved.id]);
    await expect(card).toHaveAttribute('data-removing-card', 'true');
    await seekLayoutAnimations(page, 0);
    expect((await box(card)).height).toBeCloseTo(beforeDelete.card.height, 0);
    expect((await box(below)).y).toBeCloseTo(beforeDelete.below.y, 0);
    await seekLayoutAnimations(page, 0.5);
    await aligned(card, page.locator(`[data-goo-blob-id="card-${saved.id}"]`));
    await seekLayoutAnimations(page, 1);
    await expect(card).toHaveCount(0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`resizing a travelling card preserves its displayed box with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Resize traveller ${renderer}`);
    const slick = await api.createSlick(board, 'Travel slick');
    let saved = await api.createCard(board, 'Todo', 'Traveller', '', [], slick.name);
    await api.createCard(board, 'In Progress', 'Neighbour', '', [], slick.name);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const card = page.locator(`[data-card-id="${saved.id}"]`);
    await expect(card).toBeVisible();
    saved = await api.moveCard(board.id, saved.id, board.columns.find(column => column.title === 'In Progress')!.id);
    const copy = page.locator(`[data-moving-card-id="${saved.id}"]`);
    await expect(copy).toHaveCount(1);
    await seekLayoutAnimations(page, 0.4);
    const before = await box(copy);
    saved = await api.updateCard(board.id, saved, { title: longTitle });
    await expect(copy).toContainText(longTitle);
    await seekLayoutAnimations(page, 0);
    const start = await box(copy);
    expect(start.x).toBeCloseTo(before.x, 0);
    expect(start.y).toBeCloseTo(before.y, 0);
    expect(start.height).toBeCloseTo(before.height, 0);
    await seekLayoutAnimations(page, 0.5);
    expect((await box(copy)).height).toBeGreaterThan(before.height);
    const blob = page.locator(`[data-goo-blob-id="card-${saved.id}"]`);
    if (renderer === 'full') { await aligned(copy, blob); }
    else { await expect(blob).toHaveCount(0); }
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(copy);
    await seekLayoutAnimations(page, 1);
    await expect(copy).toHaveCount(0);
    expect((await box(card)).height).toBeCloseTo(landing.height, 0);
    expect((await box(card)).y).toBeCloseTo(landing.y, 0);
    await aligned(card, blob);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);

    // The reverse interruption remounts an already resizing card in its new column.
    saved = await api.updateCard(board.id, saved, { title: 'Short traveller' });
    await expect(card).toContainText('Short traveller');
    await seekLayoutAnimations(page, 0.35);
    const beforeMove = await box(card);
    await api.moveCard(board.id, saved.id, board.columns.find(column => column.title === 'Todo')!.id);
    await expect(copy).toHaveCount(1);
    await seekLayoutAnimations(page, 0);
    expect((await box(copy)).height).toBeCloseTo(beforeMove.height, 0);
    expect((await box(copy)).y).toBeCloseTo(beforeMove.y, 0);
    await seekLayoutAnimations(page, 0.9999);
    const returnLanding = await box(copy);
    await seekLayoutAnimations(page, 1);
    await expect(copy).toHaveCount(0);
    expect((await box(card)).height).toBeCloseTo(returnLanding.height, 0);
    await aligned(card, blob);
  });

  test(`creation and resizing share one layout pass with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const connection = await interceptBoardConnection(page);
    const board = await api.createBoard(`Combined resize ${renderer}`);
    const neighbour = await api.createCard(board, 'Todo', 'Neighbour');
    const saved = await api.createCard(board, 'Todo', 'Short title');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const card = page.locator(`[data-card-id="${saved.id}"]`);
    const below = page.locator(`[data-card-id="${neighbour.id}"]`);
    await expect(card).toBeVisible();
    const before = { card: await box(card), below: await box(below) };
    connection.bufferEvents();
    await api.updateCard(board.id, saved, { title: longTitle });
    const created = await api.createCard(board, 'Todo', 'New arrival');
    await expect.poll(() => connection.bufferedEventCount).toBe(2);
    connection.releaseEvents();
    await expect(card).toContainText(longTitle);
    await seekLayoutAnimations(page, 0);
    expect((await box(card)).height).toBeCloseTo(before.card.height, 0);
    expect((await box(card)).y).toBeCloseTo(before.card.y, 0);
    expect((await box(below)).y).toBeCloseTo(before.below.y, 0);
    expect((await box(page.locator(`[data-card-id="${created.id}"]`))).height).toBe(0);
    await seekLayoutAnimations(page, 0.9999);
    const landing = await box(below);
    await seekLayoutAnimations(page, 1);
    expect((await box(below)).y).toBeCloseTo(landing.y, 0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  for (const delivery of ['realtime-first', 'http-only']) {
    test(`local resizing animates once with ${delivery} delivery and ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
      const connection = await interceptBoardConnection(page);
      const board = await api.createBoard(`Local resize ${renderer} ${delivery}`);
      const saved = await api.createCard(board, 'Todo', 'Short title');
      await page.emulateMedia({ reducedMotion: 'no-preference' });
      await controlLayoutAnimations(page);
      await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
      const bp = new BoardPage(page);
      const card = page.locator(`[data-card-id="${saved.id}"]`);
      await expect(card).toBeVisible();
      const before = await box(card);
      await bp.openCard('Todo', 'Short title');
      await bp.renameOpenCard(longTitle);
      const response = await holdCardUpdateResponse(page, board.id, saved.id);
      if (delivery === 'http-only') { connection.pauseEvents(); }
      await page.getByRole('button', { name: 'Save card', exact: true }).click();
      await response.ready;
      if (delivery === 'realtime-first') {
        await expect(card).toContainText(longTitle);
        await seekLayoutAnimations(page, 0.4);
        expect((await box(card)).height).toBeGreaterThan(before.height);
        await seekLayoutAnimations(page, 1);
      }
      response.release();
      await expect(page.getByRole('dialog')).toBeHidden();
      if (delivery === 'http-only') {
        expect(await seekLayoutAnimations(page, 0)).toBeGreaterThan(0);
        expect((await box(card)).height).toBeCloseTo(before.height, 0);
        await seekLayoutAnimations(page, 1);
      }
      expect(await seekLayoutAnimations(page, 0)).toBe(0);
    });
  }

  test(`reduced motion finishes and skips saved-card resizing with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Reduced resize ${renderer}`);
    const saved = await api.createCard(board, 'Todo', 'Short title');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const card = page.locator(`[data-card-id="${saved.id}"]`);
    await expect(card).toBeVisible();
    const before = await box(card);
    const updated = await api.updateCard(board.id, saved, { title: longTitle });
    await expect(card).toContainText(longTitle);
    await seekLayoutAnimations(page, 0.3);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await expect.poll(() => seekLayoutAnimations(page, 0)).toBe(0);
    expect((await box(card)).height).toBeGreaterThan(before.height);
    await api.updateCard(board.id, updated, { title: 'Short title' });
    await expect(card).toContainText('Short title');
    expect((await box(card)).height).toBeCloseTo(before.height, 0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`filtering settles a resize and reveals the saved height immediately with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Filtered resize ${renderer}`);
    const saved = await api.createCard(board, 'Todo', 'Short title');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const card = page.locator(`[data-card-id="${saved.id}"]`);
    await expect(card).toBeVisible();
    const before = await box(card);
    await api.updateCard(board.id, saved, { title: longTitle });
    await expect(card).toContainText(longTitle);
    await seekLayoutAnimations(page, 0.3);
    const search = page.getByPlaceholder('Search', { exact: true });
    await search.fill('No matching title');
    await expect(card).toHaveCount(0);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
    await search.fill('');
    await expect(card).toBeVisible();
    expect((await box(card)).height).toBeGreaterThan(before.height);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`a scrolled shrinking card retains its starting viewport with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Scrolled resize ${renderer}`);
    const saved = await api.createCard(board, 'Todo', `${longTitle} ${longTitle}`.slice(0, 200));
    for (let index = 0; index < 18; index++) {
      await api.createCard(board, 'Todo', `Neighbour ${index}`);
    }
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const card = page.locator(`[data-card-id="${saved.id}"]`);
    const column = new BoardPage(page).column('Todo');
    const scroller = column.locator('.column-content');
    await expect(card).toHaveCount(1);
    await scroller.evaluate(element => { element.scrollTop = element.scrollHeight; });
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    const before = { card: await box(card), scroll: await scroller.evaluate(element => element.scrollTop) };
    expect(before.scroll).toBeGreaterThan(100);
    await api.updateCard(board.id, saved, { title: 'Short title' });
    await expect(card).toContainText('Short title');
    await seekLayoutAnimations(page, 0);
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    expect((await box(card)).height).toBeCloseTo(before.card.height, 0);
    expect((await box(card)).y).toBeCloseTo(before.card.y, 0);
    expect(await scroller.evaluate(element => element.scrollTop)).toBeCloseTo(before.scroll, 0);
    await seekLayoutAnimations(page, 1);
    expect((await box(card)).height).toBeLessThan(before.card.height);
    expect(await seekLayoutAnimations(page, 0)).toBe(0);
  });

  test(`tag and checklist edits resize saved cards with ${renderer} goo`, async ({ api, authenticatedPage: page }) => {
    const board = await api.createBoard(`Resize metadata ${renderer}`);
    // Membership changes use an already loaded catalogue entry. Realtime
    // introduction of a brand-new tag has a separate catalogue bug (#945).
    await api.createCard(board, 'In Progress', 'Existing tagged card', '', ['Added tag']);
    let saved = await api.createCard(board, 'Todo', 'Short title');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await controlLayoutAnimations(page);
    await page.goto(`/boards/${board.id}?gooRenderer=${renderer}`);
    const card = page.locator(`[data-card-id="${saved.id}"]`);
    await expect(card).toBeVisible();
    const original = await box(card);
    saved = await api.updateCard(board.id, saved, { tagNames: ['Added tag'] });
    await expect(card.getByLabel('Added tag')).toBeVisible();
    expect(await seekLayoutAnimations(page, 0)).toBeGreaterThan(0);
    expect((await box(card)).height).toBeCloseTo(original.height, 0);
    await seekLayoutAnimations(page, 1);
    expect((await box(card)).height).toBeGreaterThan(original.height);
    saved = await api.updateCard(board.id, saved, { tagNames: [] });
    await expect(card.getByLabel('Added tag')).toHaveCount(0);
    await seekLayoutAnimations(page, 1);
    expect((await box(card)).height).toBeCloseTo(original.height, 0);
    await api.updateCard(board.id, saved, { description: '- [ ] A task' });
    await expect(card.getByLabel('0 of 1 checklist items complete')).toBeVisible();
    expect(await seekLayoutAnimations(page, 0)).toBeGreaterThan(0);
    expect((await box(card)).height).toBeCloseTo(original.height, 0);
    await seekLayoutAnimations(page, 1);
    expect((await box(card)).height).toBeGreaterThan(original.height);
  });
}

async function box(locator: Locator) {
  const result = await locator.boundingBox();
  expect(result).not.toBeNull();
  return result!;
}

async function aligned(card: Locator, blob: Locator) {
  await expect(blob).toBeVisible();
  await expect.poll(async () => {
    const cardBox = await box(card);
    const blobBox = await box(blob);
    return Math.abs(cardBox.y + cardBox.height / 2 - blobBox.y - blobBox.height / 2);
  }).toBeLessThanOrEqual(1);
}
