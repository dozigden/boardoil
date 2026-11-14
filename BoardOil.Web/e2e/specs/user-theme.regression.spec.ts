import { authenticatePage, expect, getBaseUrl, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';

test('slick effect saves in this browser and uses Bridges in a fresh browser context', async ({ authenticatedPage: page, browser }) => {
  await page.goto('/user-admin/theme');
  const save = page.getByRole('button', { name: 'Save', exact: true });
  await expect(page.getByRole('button', { name: 'Bridges', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Liquid', exact: true }).click();
  expect(await page.evaluate(() => localStorage.getItem('boardoil:slick-effect'))).toBeNull();
  await save.click();
  await expect(save).toBeDisabled();
  await page.reload();
  await expect(page.getByRole('button', { name: 'Liquid', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'None', exact: true }).click();
  await page.getByRole('link', { name: 'Profile', exact: true }).click();
  await page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true }).getByRole('button', { name: 'Discard', exact: true }).click();
  await expect(page).toHaveURL('/user-admin/profile');
  await page.getByRole('link', { name: 'Theme', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Liquid', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.screenshot({ path: test.info().outputPath('slick-effect-settings.png') });
  const fresh = await browser.newContext({ baseURL: getBaseUrl() });
  try {
    const other = await fresh.newPage();
    await authenticatePage(other);
    await other.goto('/user-admin/theme');
    await expect(other.getByRole('button', { name: 'Bridges', exact: true })).toHaveAttribute('aria-pressed', 'true');
  } finally {
    await fresh.close();
  }
});

test('another tab applies a saved slick effect without resetting board selection or scroll', async ({ api, authenticatedPage: page }) => {
  const board = await api.createBoard('Live slick preference');
  await api.createSlick(board, 'Preference slick');
  for (let index = 0; index < 18; index++) await api.createCard(board, 'Todo', `Preference card ${index}`, '', [], 'Preference slick');
  const bp = new BoardPage(page);
  await bp.open(board.id);
  await expect(page.getByRole('combobox', { name: 'Goo renderer', exact: true })).toHaveCount(0);
  await bp.enterCardSelectionMode();
  await bp.selectCard('Todo', 'Preference card 17');
  const scroller = bp.column('Todo').locator('.column-content');
  const scroll = await scroller.evaluate(element => { element.scrollTop = 100; return element.scrollTop; });
  expect(scroll).toBeGreaterThan(0);
  const settings = await page.context().newPage();
  try {
    await settings.goto('/user-admin/theme');
    const canvas = page.locator('canvas.slick-goo-layer');
    for (const effect of ['Liquid', 'Bridges', 'None']) {
      await settings.getByRole('button', { name: effect, exact: true }).click();
      await settings.getByRole('button', { name: 'Save', exact: true }).click();
      await expect(canvas).toHaveAttribute('data-renderer', effect.toLowerCase());
      await expect(bp.selectionModeCard('Todo', 'Preference card 17')).toHaveAttribute('aria-checked', 'true');
      expect(await scroller.evaluate(element => element.scrollTop)).toBe(scroll);
    }
    await expect(canvas).toHaveAttribute('data-renderer', 'none');
    const secondBoard = await api.createBoard('Same browser preference');
    await bp.open(secondBoard.id);
    await expect(canvas).toHaveAttribute('data-renderer', 'none');
  } finally {
    await settings.close();
  }
});

test('theme changes apply on Save, persist locally, and unsaved changes can be discarded', async ({ authenticatedPage: page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/user-admin/theme');
  const save = page.getByRole('button', { name: 'Save', exact: true });
  await expect(save).toBeDisabled();
  await expect(page.getByRole('button', { name: 'System', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Dark', exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await save.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(save).toBeDisabled();
  await page.screenshot({ path: test.info().outputPath('theme-dark.png') });
  await page.reload();
  await expect(page.getByRole('button', { name: 'Dark', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'Light', exact: true }).click();
  await page.getByRole('link', { name: 'Profile', exact: true }).click();
  const discard = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
  await discard.getByRole('button', { name: 'Discard', exact: true }).click();
  await expect(page).toHaveURL('/user-admin/profile');
  await page.getByRole('link', { name: 'Theme', exact: true }).click();
  await expect(page.getByRole('button', { name: 'Dark', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.getByRole('button', { name: 'System', exact: true }).click();
  await save.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(save).toBeDisabled();
});

for (const scenario of [
  { editedGroup: 'Appearance', editedOption: 'Dark', menuGroup: 'Slick effect', menuOption: 'Liquid' },
  { editedGroup: 'Slick effect', editedOption: 'None', menuGroup: 'Appearance', menuOption: 'Dark' }
]) {
  test(`saving the ${scenario.editedGroup} draft retains ${scenario.menuGroup} saved through the menu`, async ({ authenticatedPage: page }) => {
    await page.goto('/user-admin/theme');
    const save = page.getByRole('button', { name: 'Save', exact: true });
    await expect(save).toBeDisabled();
    await page.getByRole('group', { name: scenario.editedGroup, exact: true })
      .getByRole('button', { name: scenario.editedOption, exact: true }).click();
    const userMenu = page.getByRole('button', { name: 'User menu', exact: true });
    await userMenu.click();
    await page.getByRole('dialog', { name: 'User menu', exact: true })
      .getByRole('group', { name: scenario.menuGroup, exact: true })
      .getByRole('button', { name: scenario.menuOption, exact: true }).click();
    await userMenu.click();
    await expect(page.getByRole('group', { name: scenario.menuGroup, exact: true })
      .getByRole('button', { name: scenario.menuOption, exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('group', { name: scenario.editedGroup, exact: true })
      .getByRole('button', { name: scenario.editedOption, exact: true })).toHaveAttribute('aria-pressed', 'true');
    await save.click();
    await expect(save).toBeDisabled();
    await page.reload();
    await expect(page.getByRole('group', { name: scenario.menuGroup, exact: true })
      .getByRole('button', { name: scenario.menuOption, exact: true })).toHaveAttribute('aria-pressed', 'true');
    await expect(page.getByRole('group', { name: scenario.editedGroup, exact: true })
      .getByRole('button', { name: scenario.editedOption, exact: true })).toHaveAttribute('aria-pressed', 'true');
  });
}

test('user menu switches theme immediately, persists it, and keeps settings drafts consistent', async ({ authenticatedPage: page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/user-admin/theme');
  const save = page.getByRole('button', { name: 'Save', exact: true });
  const userMenu = page.getByRole('button', { name: 'User menu', exact: true });
  const menu = page.getByRole('dialog', { name: 'User menu', exact: true });
  const choices = menu.getByRole('group', { name: 'Appearance', exact: true });

  await expect(save).toBeDisabled();
  await userMenu.click();
  await expect(choices.getByRole('button', { name: 'System', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await choices.getByRole('button', { name: 'Dark', exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(choices.getByRole('button', { name: 'Dark', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.screenshot({ path: test.info().outputPath('user-menu-theme-dark.png') });
  await userMenu.click();
  await expect(page.getByRole('button', { name: 'Dark', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(save).toBeDisabled();

  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.getByRole('button', { name: 'Light', exact: true }).click();
  await userMenu.click();
  await choices.getByRole('button', { name: 'System', exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await userMenu.click();
  await expect(page.getByRole('button', { name: 'Light', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await expect(save).toBeEnabled();
  await save.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await expect(save).toBeDisabled();

  await page.setViewportSize({ width: 390, height: 844 });
  await userMenu.click();
  await expect(choices.getByRole('button', { name: 'Light', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await choices.getByRole('button', { name: 'Dark', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.screenshot({ path: test.info().outputPath('user-menu-theme-mobile.png') });
  await page.keyboard.press('Escape');
  await expect(menu).not.toBeVisible();
});
