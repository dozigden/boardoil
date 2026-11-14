import { expect, test } from '../fixtures/boardOilTest';

test('theme changes apply on Save, persist locally, and unsaved changes can be discarded', async ({ authenticatedPage: page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/user-admin/theme');
  const save = page.getByRole('button', { name: 'Save', exact: true });
  await expect(save).toBeDisabled();
  await expect(page.getByRole('radio', { name: 'System', exact: true })).toBeChecked();
  await page.getByRole('radio', { name: 'Dark', exact: true }).check();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await save.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(save).toBeDisabled();
  await expect(page.getByText('Active theme: Dark', { exact: true })).toBeVisible();
  await page.screenshot({ path: test.info().outputPath('theme-dark.png') });
  await page.reload();
  await expect(page.getByRole('radio', { name: 'Dark', exact: true })).toBeChecked();
  await page.getByRole('radio', { name: 'Light', exact: true }).check();
  await page.getByRole('link', { name: 'Profile', exact: true }).click();
  const discard = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
  await discard.getByRole('button', { name: 'Discard', exact: true }).click();
  await expect(page).toHaveURL('/user-admin/profile');
  await page.getByRole('link', { name: 'Theme', exact: true }).click();
  await expect(page.getByRole('radio', { name: 'Dark', exact: true })).toBeChecked();
  await page.getByRole('radio', { name: 'System', exact: true }).check();
  await save.click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(save).toBeDisabled();
});

test('user menu switches theme immediately, persists it, and keeps settings drafts consistent', async ({ authenticatedPage: page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/user-admin/theme');
  const save = page.getByRole('button', { name: 'Save', exact: true });
  const userMenu = page.getByRole('button', { name: 'User menu', exact: true });
  const menu = page.getByRole('dialog', { name: 'User menu', exact: true });
  const choices = menu.getByRole('group', { name: 'Theme mode', exact: true });

  await userMenu.click();
  await expect(choices.getByRole('button', { name: 'System', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await choices.getByRole('button', { name: 'Dark', exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await expect(choices.getByRole('button', { name: 'Dark', exact: true })).toHaveAttribute('aria-pressed', 'true');
  await page.screenshot({ path: test.info().outputPath('user-menu-theme-dark.png') });
  await userMenu.click();
  await expect(page.getByRole('radio', { name: 'Dark', exact: true })).toBeChecked();
  await expect(save).toBeDisabled();

  await page.reload();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await page.getByRole('radio', { name: 'Light', exact: true }).check();
  await userMenu.click();
  await choices.getByRole('button', { name: 'System', exact: true }).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
  await userMenu.click();
  await expect(page.getByRole('radio', { name: 'Light', exact: true })).toBeChecked();
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
