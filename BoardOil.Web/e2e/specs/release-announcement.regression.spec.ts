import { expect, test } from '../fixtures/boardOilTest';

test('opens the slick announcement on request and keeps the selected effect after refresh', async ({ authenticatedPage: page }) => {
  // The disposable installation creates its users after the announcement migration.
  await page.route('**/api/auth/me', async route => {
    const response = await route.fetch();
    const session = await response.json();
    session.data.hasUnreadAnnouncement = true;
    await route.fulfill({ response, json: session });
  }, { times: 1 });
  await page.goto('/boards');
  const trigger = page.getByRole('button', { name: 'What’s new?', exact: true });
  const dialog = page.getByRole('dialog', { name: 'What’s New?', exact: true });
  await expect(trigger).toBeVisible();
  await expect(dialog).toHaveCount(0);

  const acknowledgement = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/users/me/announcement/read'
      && response.request().method() === 'POST');
  await trigger.press('Enter');
  expect((await acknowledgement).ok()).toBe(true);
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('heading', { name: 'Slick effect', exact: true })).toBeVisible();
  await expect(dialog.getByRole('figure', { name: 'Slick effect preview' })).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Bridges' })).toHaveAttribute('aria-pressed', 'true');
  await dialog.getByRole('button', { name: 'Liquid' }).click();
  await expect(dialog.getByRole('button', { name: 'Liquid' })).toHaveAttribute('aria-pressed', 'true');
  await dialog.getByRole('button', { name: 'Close', exact: true }).press('Enter');
  await expect(dialog).toHaveCount(0);
  await expect(trigger).toHaveCount(0);
  const userMenu = page.getByRole('button', { name: 'User menu' });
  await expect(userMenu).toBeFocused();

  await page.reload();
  await userMenu.click();
  await expect(page.getByRole('button', { name: 'Liquid' })).toHaveAttribute('aria-pressed', 'true');
  await expect(trigger).toHaveCount(0);
  await expect(dialog).toHaveCount(0);
});
