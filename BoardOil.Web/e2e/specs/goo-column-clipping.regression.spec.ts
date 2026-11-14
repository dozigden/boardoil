import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('short columns open below the cards and regain scrolling at full height in every goo renderer', async ({ api, authenticatedPage: page }) => {
  test.setTimeout(60_000);
  const errors: string[] = [];
  page.on('pageerror', error => errors.push(error.message));
  const board = await api.createBoard('Goo column clipping');
  await api.createSlick(board, 'One slick');
  for (let i = 0; i < 16; i++) await api.createCard(board, 'Todo', `Tall column ${i}`, '', [], 'One slick');
  for (let i = 0; i < 4; i++) await api.createCard(board, 'In Progress', `Short column ${i}`, '', [], 'One slick');
  const boardPage = new BoardPage(page);
  await page.goto('/user-admin/profile');
  const optIn = page.getByRole('checkbox', { name: 'Enable experimental features', exact: true });
  const originallyEnabled = await optIn.isChecked();
  async function setExperimental(enabled: boolean) {
    await page.goto('/user-admin/profile');
    if (await optIn.isChecked() === enabled) return;
    await optIn.setChecked(enabled);
    await page.getByRole('button', { name: 'Save profile', exact: true }).click();
    await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeDisabled();
  }
  try {
    for (const experimental of [false, true]) {
      await setExperimental(experimental);
      const modes = experimental ? ['smooth', 'liquid'] : ['full', 'lite'];
      for (const mode of modes) await test.step(mode, async () => {
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.goto(`/boards/${board.id}?gooRenderer=${mode === 'lite' ? 'lite' : 'full'}`);
        if (experimental) await page.getByRole('combobox', { name: 'Goo renderer', exact: true }).selectOption(mode);
        const column = boardPage.column('In Progress');
        const scroller = column.locator('.column-content');
        await expect(column).toHaveClass(/column--short/);
        await expect(boardPage.column('Todo')).not.toHaveClass(/column--short/);
        expect(await column.evaluate(el => getComputedStyle(el, '::after').content)).toBe('none');
        const card = boardPage.card('In Progress', 'Short column 0');
        const before = (await card.boundingBox())!;
        await page.setViewportSize({ width: 1440, height: 350 });
        await expect(column).not.toHaveClass(/column--short/);
        const full = (await card.boundingBox())!;
        expect(full.x).toBe(before.x); expect(full.width).toBe(before.width);
        expect(await scroller.evaluate(el => { el.scrollTop = 80; return el.scrollTop; })).toBeGreaterThan(0);
        await page.setViewportSize({ width: 1440, height: 1000 });
        await expect(column).toHaveClass(/column--short/);
        await expect.poll(() => scroller.evaluate(el => el.scrollTop)).toBe(0);
        const after = (await card.boundingBox())!;
        expect(after.x).toBe(before.x); expect(after.width).toBe(before.width);
        await page.getByPlaceholder('Search', { exact: true }).fill('Short column');
        await expect(boardPage.column('Todo')).toHaveClass(/column--short/);
        await expect.poll(() => page.locator('section.board').evaluate(el => el.style.getPropertyValue('--board-goo-clip-bottom'))).toBe('0px');
        await page.getByPlaceholder('Search', { exact: true }).fill('');
        await expect(boardPage.column('Todo')).not.toHaveClass(/column--short/);
      });
    }
  } finally {
    await setExperimental(originallyEnabled);
  }
  expect(errors).toEqual([]);
});
