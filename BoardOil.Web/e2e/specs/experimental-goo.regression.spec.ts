import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('experimental Smooth and Liquid follow the live board and stop when idle', async ({ api, authenticatedPage: page }) => {
  test.setTimeout(60_000);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  const board = await api.createBoard('Experimental goo');
  await api.createSlick(board, 'Green pool');
  const travelling = await api.createCard(board, 'Todo', 'Travelling card', '', [], 'Green pool');
  for (let i = 0; i < 12; i++) await api.createCard(board, 'Todo', `Stack card ${i}`, '', [], 'Green pool');
  await api.createCard(board, 'In Progress', 'Across the gap', '', [], 'Green pool');
  const boardPage = new BoardPage(page);
  const picker = page.getByRole('combobox', { name: 'Goo renderer', exact: true });
  const canvas = page.locator('canvas.experimental-goo-layer');

  await test.step('opt in through the profile and get Smooth by default', async () => {
    await page.goto('/user-admin/profile');
    const optIn = page.getByRole('checkbox', { name: 'Experimental features', exact: true });
    await expect(optIn).toBeVisible();
    if (await optIn.isChecked()) {
      await optIn.uncheck();
      await page.getByRole('button', { name: 'Save profile', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeDisabled();
    }
    await boardPage.open(board.id);
    await expect(picker).toHaveCount(0);
    await page.goto('/user-admin/profile');
    await page.getByRole('checkbox', { name: 'Experimental features', exact: true }).check();
    await page.getByRole('button', { name: 'Save profile', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeDisabled();
    await boardPage.open(board.id);
    await expect(picker).toHaveValue('smooth');
    await expect(canvas).toHaveAttribute('data-renderer', 'smooth');
    await expect.poll(() => canvas.evaluate((el: HTMLCanvasElement) => {
      const pixels = el.getContext('2d')!.getImageData(0, 0, el.width, el.height).data;
      return pixels.some((value, index) => index % 4 === 3 && value > 0);
    })).toBe(true);
    await page.screenshot({ path: test.info().outputPath('board-smooth.png') });
  });
  await test.step('scroll Smooth outlines with their cards without rebuilding them', async () => {
    const scroller = boardPage.column('Todo').locator('.column-content');
    const outline = scroller.locator('.smooth-column-outline path').first();
    await expect(outline).toHaveAttribute('d', /^M/);
    const d = await outline.getAttribute('d');
    const builds = await canvas.getAttribute('data-outline-builds');
    const renders = await canvas.getAttribute('data-render-count');
    const movement = await scroller.evaluate(el => {
      const path = el.querySelector('.smooth-column-outline path')!;
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
  await test.step('sleep Liquid and preserve its frozen image through renderer changes', async () => {
    await picker.selectOption('liquid');
    await expect(page.locator('.smooth-column-outline')).toHaveCount(0);
    await expect(canvas).toHaveAttribute('data-awake', '0', { timeout: 15_000 });
    const image = await canvas.evaluate((el: HTMLCanvasElement) => el.toDataURL());
    const renders = await canvas.getAttribute('data-render-count');
    await page.evaluate(() => new Promise<void>(resolve => {
      let remaining = 12;
      function frame() { if (--remaining) requestAnimationFrame(frame); else resolve(); }
      requestAnimationFrame(frame);
    }));
    await expect(canvas).toHaveAttribute('data-render-count', renders!);
    await picker.selectOption('smooth');
    await expect(canvas).toHaveAttribute('data-renderer', 'smooth');
    await picker.selectOption('liquid');
    await expect(canvas).toHaveAttribute('data-renderer', 'liquid');
    await expect.poll(() => canvas.evaluate((el: HTMLCanvasElement) => el.toDataURL())).toBe(image);
    await page.screenshot({ path: test.info().outputPath('board-liquid.png') });
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
    await expect.poll(() => canvas.evaluate(el => el.style.transform)).toContain(`${scrollLeft}px`);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await picker.selectOption('smooth');
    await picker.selectOption('liquid');
    await expect(canvas).toHaveAttribute('data-renderer', 'liquid');
    await expect(page.locator('.smooth-column-outline').first()).toBeAttached();
    await page.screenshot({ path: test.info().outputPath('board-reduced-motion.png') });
  });
  await test.step('opt out and restore the existing renderer', async () => {
    await page.goto('/user-admin/profile');
    await page.getByRole('checkbox', { name: 'Experimental features', exact: true }).uncheck();
    await page.getByRole('button', { name: 'Save profile', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeDisabled();
    await boardPage.open(board.id);
    await expect(picker).toHaveCount(0);
    await expect(canvas).toHaveCount(0);
  });
  expect(errors).toEqual([]);
});
