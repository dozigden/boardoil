import { randomUUID } from 'node:crypto';
import { setSlickEffect } from '../support/slickEffects';
import { authenticatePage, expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { SelectionBridges } from '../ui/SelectionBridges';

test('selection clips outlines and bridge shoulders below each wrapped column header', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Wrapped selection headers');
  const title = 'In progress with a much longer heading that wraps onto several lines';
  for (let index = 0; index < 12; index++) {
    await api.createCard(board, 'Todo', `Other column ${index}`);
    await api.createCard(board, 'In Progress', `Below selection ${index}`);
  }
  await api.createCard(board, 'In Progress', 'Selected below wrapped header');
  await api.createCard(board, 'Done', 'Selected neighbour');
  await api.renameColumn(board.id, board.columns.find(column => column.title === 'In Progress')!.id, title);
  await page.setViewportSize({ width: 1152, height: 450 });
  const bp = new BoardPage(page);
  const selection = new SelectionBridges(page);
  await bp.open(board.id);
  await bp.enterCardSelectionMode();
  await bp.selectCard(title, 'Selected below wrapped header');
  await bp.selectCard('Done', 'Selected neighbour');
  const column = bp.column(title);
  const scroller = column.locator('.column-content');
  const card = bp.selectionModeCard(title, 'Selected below wrapped header');
  expect((await scroller.boundingBox())!.y).toBeGreaterThan((await bp.column('Done').locator('.column-content').boundingBox())!.y + 20);
  await scroller.evaluate(element => { element.scrollTop = 32; });
  await expect.poll(() => selection.paintsBelowHeader(column, card)).toBe(true);
  await expect.poll(() => selection.paintsInHeader(column)).toBe(false);
  await expect.poll(() => selection.paintsBetween(card, bp.selectionModeCard('Done', 'Selected neighbour'))).toBe(true);
  await page.screenshot({ path: test.info().outputPath('selection-wrapped-header.png') });
  await page.setViewportSize({ width: 700, height: 500 });
  await page.locator('section.board').evaluate(element => { element.scrollLeft = 180; });
  await expect.poll(() => selection.paintsInHeader(column)).toBe(false);
  await page.screenshot({ path: test.info().outputPath('selection-wrapped-header-scrolled.png') });
});

for (const experimental of [false, true]) {
  test(`selection Bridges remain independent of slick effects and follow visible selection (experimental opt-in: ${experimental})`, async ({ api, page }) => {
    const board = await api.createBoard('Selection Bridges');
    await api.createSlick(board, 'First slick');
    await api.createSlick(board, 'Second slick');
    for (let index = 0; index < 12; index++) await api.createCard(board, 'Todo', `Unselected ${index}`);
    const first = await api.createCard(board, 'Todo', 'Selected first', '', [], 'First slick');
    const second = await api.createCard(board, 'Todo', 'Selected second', '', [], 'Second slick');
    const unassigned = await api.createCard(board, 'In Progress', 'Selected unassigned');
    const bp = new BoardPage(page);
    const selection = new SelectionBridges(page);

    const userName = `selection-${randomUUID().slice(0, 8)}`;
    const password = 'SelectionPassword1234!';
    const user = await api.createUser(userName, password);
    await api.addBoardMember(board.id, user.id, 'Owner');
    await authenticatePage(page, userName, password);
    const optIn = page.getByRole('checkbox', { name: 'Enable experimental features', exact: true });
    await page.goto('/user-admin/profile');
    await expect(optIn).not.toBeChecked();
    if (experimental) {
      await optIn.check();
      await page.getByRole('button', { name: 'Save profile', exact: true }).click();
      await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeDisabled();
    }
    await bp.open(board.id);
    await bp.enterCardSelectionMode();
    await bp.selectCard('Todo', 'Selected first');
    await bp.selectCard('Todo', 'Selected second');
    await bp.selectCard('In Progress', 'Selected unassigned');
    for (const mode of ['none', 'bridges', 'liquid']) {
      await test.step(`selection Bridges with ${mode} slick effect`, async () => {
        await setSlickEffect(page, mode);
        await expect(selection.canvas).toHaveAttribute('data-renderer', 'bridges');
        await expect.poll(() => selection.selectedIds()).toEqual([second.id, first.id, unassigned.id]);
        await expect.poll(() => selection.paintsBetween(bp.selectionModeCard('Todo', 'Selected second'),
          bp.selectionModeCard('In Progress', 'Selected unassigned'))).toBe(true);
        await expect.poll(() => selection.paintsAround(bp.selectionModeCard('Todo', 'Selected first'))).toBe(true);
        expect(await selection.colourAround(bp.selectionModeCard('Todo', 'Selected first')))
          .toEqual(await selection.colourAround(bp.selectionModeCard('In Progress', 'Selected unassigned')));
        await page.screenshot({ path: test.info().outputPath(`selection-${mode}.png`) });
      });
    }
    const scroller = bp.column('Todo').locator('.column-content');
    const builds = await selection.canvas.getAttribute('data-outline-builds');
    await scroller.evaluate(element => { element.scrollTop = 32; });
    await expect.poll(() => selection.paintsAround(bp.selectionModeCard('Todo', 'Selected first'))).toBe(true);
    await expect(selection.canvas).toHaveAttribute('data-outline-builds', builds!);
    await scroller.evaluate(element => { element.scrollTop = 0; });
    const light = await selection.colourAround(bp.selectionModeCard('Todo', 'Selected first'));
    await page.emulateMedia({ colorScheme: 'dark', reducedMotion: 'reduce' });
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await expect.poll(() => selection.colourAround(bp.selectionModeCard('Todo', 'Selected first'))).not.toEqual(light);
    await expect(selection.canvas).toHaveCSS('animation-name', 'none');
    await page.screenshot({ path: test.info().outputPath(`selection-dark-${experimental}.png`) });
    await page.emulateMedia({ colorScheme: 'light', reducedMotion: 'no-preference' });
    await page.getByPlaceholder('Search', { exact: true }).fill('Selected unassigned');
    await expect.poll(() => selection.selectedIds()).toEqual([unassigned.id]);
    await page.getByPlaceholder('Search', { exact: true }).fill('');
    await expect.poll(() => selection.selectedIds()).toEqual([second.id, first.id, unassigned.id]);
    if (experimental) {
      await api.deleteCards(board.id, [second.id]);
      await expect(bp.selectionModeCard('Todo', 'Selected second')).toHaveCount(0);
      await expect.poll(() => selection.selectedIds()).toEqual([first.id, unassigned.id]);
    }
    await bp.selectionModeCard('Todo', 'Selected first').click();
    if (!experimental) await bp.selectionModeCard('Todo', 'Selected second').click();
    await bp.selectionModeCard('In Progress', 'Selected unassigned').click();
    await expect(selection.canvas).toHaveCount(0);
    await bp.selectCard('Todo', 'Selected first');
    await expect(selection.canvas).toBeAttached();
    await page.getByTitle('Done selecting cards').click();
    await expect(selection.canvas).toHaveCount(0);
  });
}
