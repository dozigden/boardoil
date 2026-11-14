import { setSlickEffect } from '../support/slickEffects';
import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { reportHorizontalOverscroll } from '../support/experimentalGooScrolling';

test('None, Bridges and Liquid follow the live board and stop when idle', async ({ api, authenticatedPage: page }) => {
  test.setTimeout(60_000);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  const board = await api.createBoard('Experimental goo');
  await api.createSlick(board, 'Green pool');
  for (let i = 0; i < 12; i++) await api.createCard(board, 'Todo', `Stack card ${i}`, '', [], 'Green pool');
  const travelling = await api.createCard(board, 'Todo', 'Travelling card', '', [], 'Green pool');
  await api.createCard(board, 'In Progress', 'Across the gap', '', [], 'Green pool');
  const boardPage = new BoardPage(page);
  const canvas = page.locator('canvas.slick-goo-layer');
  const header = page.locator('header').filter({ has: page.getByRole('button', { name: 'User menu', exact: true }) });

  await test.step('use Bridges by default without an experimental header picker', async () => {
    await boardPage.open(board.id);
    await expect(header.getByRole('combobox')).toHaveCount(0);
    await expect(header.getByRole('img', { name: 'Experimental feature', exact: true })).toHaveCount(0);
    await expect(canvas).toHaveAttribute('data-renderer', 'bridges');
    await expect.poll(() => canvas.evaluate((el: HTMLCanvasElement) => {
      const pixels = el.getContext('2d')!.getImageData(0, 0, el.width, el.height).data;
      return pixels.some((value, index) => index % 4 === 3 && value > 0);
    })).toBe(true);
    await page.screenshot({ path: test.info().outputPath('board-bridges.png') });
  });
  await test.step('scroll Bridges outlines with their cards without rebuilding them', async () => {
    const scroller = boardPage.column('Todo').locator('.column-content');
    const outline = scroller.locator('.bridge-column-outline path').first();
    await expect(outline).toHaveAttribute('d', /^M/);
    const d = await outline.getAttribute('d');
    const builds = await canvas.getAttribute('data-outline-builds');
    const renders = await canvas.getAttribute('data-render-count');
    const movement = await scroller.evaluate(el => {
      const path = el.querySelector('.bridge-column-outline path')!;
      const card = el.querySelector('.card[data-card-id]')!;
      const pathTop = path.getBoundingClientRect().top, cardTop = card.getBoundingClientRect().top;
      el.scrollTop = 120;
      // Read before a scroll event or animation frame can redraw anything.
      return { outline: pathTop - path.getBoundingClientRect().top, card: cardTop - card.getBoundingClientRect().top };
    });
    expect(movement).toEqual({ outline: 120, card: 120 });
    await expect.poll(() => canvas.getAttribute('data-render-count')).not.toBe(renders);
    await expect(outline).toHaveAttribute('d', d!);
    await expect(canvas).toHaveAttribute('data-outline-builds', builds!);
    await scroller.evaluate(el => { el.scrollTop = 0; });
  });
  await test.step('keep merged outlines without cross-column bridges or Liquid work in None', async () => {
    const outlines = page.locator('.bridge-column-outline path');
    const shapes = await outlines.evaluateAll(paths => paths.map(path => path.getAttribute('d')));
    await setSlickEffect(page, 'none');
    await expect(canvas).toHaveAttribute('data-renderer', 'none');
    await expect.poll(() => outlines.evaluateAll(paths => paths.map(path => path.getAttribute('d')))).toEqual(shapes);
    await expect.poll(() => canvas.evaluate((el: HTMLCanvasElement) => {
      return el.getContext('2d')!.getImageData(0, 0, el.width, el.height).data.some((value, index) => index % 4 === 3 && value > 0);
    })).toBe(false);
    expect(page.workers()).toHaveLength(0);
    const diagnostics = JSON.parse((await canvas.getAttribute('data-slicks'))!) as { steps: number }[];
    expect(diagnostics.every(slick => slick.steps === 0)).toBe(true);
    await page.screenshot({ path: test.info().outputPath('board-none.png') });
    await page.emulateMedia({ colorScheme: 'dark' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await page.screenshot({ path: test.info().outputPath('board-none-dark.png') });
    await page.emulateMedia({ colorScheme: 'light' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');

    const target = board.columns.find(column => column.title === 'In Progress')!;
    await Promise.all([
      page.waitForFunction(() => {
        if (!document.querySelector('[data-moving-card-id]')) return false;
        const layer = document.querySelector<HTMLCanvasElement>('canvas.slick-goo-layer')!;
        return layer.getContext('2d')!.getImageData(0, 0, layer.width, layer.height).data
          .some((value, index) => index % 4 === 3 && value > 0);
      }),
      api.moveCard(board.id, travelling.id, target.id)
    ]);
    await expect(boardPage.card('In Progress', 'Travelling card')).toBeVisible();
    await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
    await expect.poll(() => canvas.evaluate((el: HTMLCanvasElement) => {
      return el.getContext('2d')!.getImageData(0, 0, el.width, el.height).data.some((value, index) => index % 4 === 3 && value > 0);
    })).toBe(false);
    await api.moveCard(board.id, travelling.id, board.columns.find(column => column.title === 'Todo')!.id);
    await expect(boardPage.card('Todo', 'Travelling card')).toBeVisible();
    await expect(page.locator('[data-moving-card-id]')).toHaveCount(0);
    expect(page.workers()).toHaveLength(0);
  });
  await test.step('sleep Liquid and preserve its frozen image through renderer changes', async () => {
    await setSlickEffect(page, 'liquid');
    await expect(canvas).toHaveAttribute('data-simulation', 'worker');
    await expect(page.locator('.bridge-column-outline')).toHaveCount(0);
    await expect(canvas).toHaveAttribute('data-awake', '0', { timeout: 15_000 });
    await expect(canvas).toHaveAttribute('data-simulation', 'worker');
    const image = await canvas.evaluate((el: HTMLCanvasElement) => el.toDataURL());
    const renders = await canvas.getAttribute('data-render-count');
    await page.evaluate(() => new Promise<void>(resolve => {
      let remaining = 12;
      function frame() { if (--remaining) requestAnimationFrame(frame); else resolve(); }
      requestAnimationFrame(frame);
    }));
    await expect(canvas).toHaveAttribute('data-render-count', renders!);
    await setSlickEffect(page, 'bridges');
    await expect(canvas).toHaveAttribute('data-renderer', 'bridges');
    await setSlickEffect(page, 'liquid');
    await expect(canvas).toHaveAttribute('data-renderer', 'liquid');
    await expect.poll(() => canvas.evaluate((el: HTMLCanvasElement) => el.toDataURL())).toBe(image);
    await page.screenshot({ path: test.info().outputPath('board-liquid.png') });
    const steps = JSON.parse((await canvas.getAttribute('data-slicks'))!) as { steps: number }[];
    await setSlickEffect(page, 'none');
    await expect(canvas).toHaveAttribute('data-renderer', 'none');
    await expect(page.locator('.bridge-column-outline').first()).toBeAttached();
    await page.evaluate(() => new Promise<void>(resolve => {
      let remaining = 12;
      function frame() { if (--remaining) requestAnimationFrame(frame); else resolve(); }
      requestAnimationFrame(frame);
    }));
    expect((JSON.parse((await canvas.getAttribute('data-slicks'))!) as { steps: number }[]).map(slick => slick.steps))
      .toEqual(steps.map(slick => slick.steps));
    await setSlickEffect(page, 'liquid');
    await expect.poll(() => canvas.evaluate((el: HTMLCanvasElement) => el.toDataURL())).toBe(image);
  });
  await test.step('scroll, move and filter cards while retaining the running solver', async () => {
    const before = JSON.parse((await canvas.getAttribute('data-slicks'))!) as { particles: number; steps: number }[];
    await boardPage.column('Todo').locator('.column-content').evaluate(el => { el.scrollTop = 120; });
    await expect.poll(async () => JSON.parse((await canvas.getAttribute('data-slicks'))!)[0].steps).toBeGreaterThan(before[0].steps);
    expect(JSON.parse((await canvas.getAttribute('data-slicks'))!)[0].particles).toBeGreaterThan(0);
    const target = board.columns.find(c => c.title === 'In Progress')!;
    await api.moveCard(board.id, travelling.id, target.id);
    await expect(boardPage.card('In Progress', 'Travelling card')).toBeVisible();
    await expect.poll(async () => JSON.parse((await canvas.getAttribute('data-slicks'))!)[0].steps).toBeGreaterThan(before[0].steps);
    const scroller = boardPage.column('Todo').locator('.column-content');
    for (const position of [10000, 0]) {
      const renders = await canvas.getAttribute('data-render-count');
      await scroller.evaluate((el, top) => { el.scrollTop = top; }, position);
      await expect.poll(() => canvas.getAttribute('data-render-count')).not.toBe(renders);
      expect(JSON.parse((await canvas.getAttribute('data-slicks'))!)[0].particles).toBeGreaterThan(0);
    }
    await page.getByPlaceholder('Search', { exact: true }).fill('No matching goo cards');
    await expect.poll(async () => JSON.parse((await canvas.getAttribute('data-slicks'))!).length).toBe(0);
    await page.getByPlaceholder('Search', { exact: true }).fill('');
    await expect.poll(async () => JSON.parse((await canvas.getAttribute('data-slicks'))!).length).toBe(1);
  });
  await test.step('follow horizontal scrolling and respect reduced motion', async () => {
    await page.setViewportSize({ width: 700, height: 900 });
    const scrollLeft = await page.locator('section.board').evaluate(el => { el.scrollLeft = 180; return el.scrollLeft; });
    expect(scrollLeft).toBeGreaterThan(0);
    await expect.poll(async () => {
      const canvasBounds = (await canvas.boundingBox())!;
      const boardBounds = (await page.locator('section.board').boundingBox())!;
      return { x: canvasBounds.x - boardBounds.x, y: canvasBounds.y - boardBounds.y };
    }).toEqual({ x: 0, y: 0 });
    for (const mode of ['none', 'bridges', 'liquid']) {
      await setSlickEffect(page, mode);
      await expect(canvas).toHaveAttribute('data-renderer', mode);
      for (const direction of ['left', 'right'] as const) {
        const range = await reportHorizontalOverscroll(page.locator('section.board'), direction);
        expect(range.after, `${mode} ${direction} overscroll must not extend the board`).toBe(range.before);
      }
    }
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await setSlickEffect(page, 'bridges');
    await setSlickEffect(page, 'liquid');
    await expect(canvas).toHaveAttribute('data-renderer', 'liquid');
    await expect(page.locator('.bridge-column-outline').first()).toBeAttached();
    await page.screenshot({ path: test.info().outputPath('board-reduced-motion.png') });
    await page.setViewportSize({ width: 375, height: 800 });
    await expect(header.getByRole('button', { name: 'User menu', exact: true })).toBeVisible();
  });
  await test.step('dispose the renderer and its worker when leaving the board', async () => {
    await page.goto('/user-admin/profile');
    await expect(canvas).toHaveCount(0);
    await expect.poll(() => page.workers().filter(worker => worker.url().includes('liquid.worker')).length).toBe(0);
  });
  expect(errors).toEqual([]);
});
