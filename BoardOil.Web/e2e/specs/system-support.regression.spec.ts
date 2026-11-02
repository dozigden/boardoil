import { expect, test } from '../fixtures/boardOilTest';

test('an administrator can open Support and refresh database information', async ({ authenticatedPage: page }) => {
  await page.goto('/admin/system');
  await page.getByRole('link', { name: 'Support', exact: true }).click();
  await expect(page).toHaveURL(/\/admin\/system\/support$/);
  await expect(page.getByRole('heading', { name: 'Support', exact: true })).toBeVisible();
  const database = page.getByRole('region', { name: 'Database', exact: true });
  await expect(database).toContainText('SQLite');
  await expect(database).toContainText('WAL');
  await expect(database).toContainText('bytes');
  const refreshed = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/system/support/info' && response.request().method() === 'GET');
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  expect((await refreshed).ok()).toBe(true);
  await expect(database).toContainText('WAL');
  await page.screenshot({ path: '/tmp/boardoil-system-support.png', fullPage: true });
});
