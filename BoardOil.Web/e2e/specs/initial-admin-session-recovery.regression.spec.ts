import { expect, test } from '../fixtures/boardOilTest';

test('successful admin creation followed by a token failure offers sign-in instead of registration', async ({ page }) => {
  let registrationCount = 0;
  await page.route('**/api/auth/me', route => route.fulfill({
    json: { success: true, data: null, statusCode: 200 }
  }));
  await page.route('**/api/auth/bootstrap-status', route => route.fulfill({
    json: { success: true, data: { requiresInitialAdminSetup: true }, statusCode: 200 }
  }));
  await page.route('**/api/auth/register-initial-admin', route => {
    registrationCount++;
    return route.fulfill({
      status: 201,
      json: {
        success: true,
        statusCode: 201,
        data: { user: { id: 1, userName: 'new-admin', displayName: 'New admin', role: 'Admin' } }
      }
    });
  });
  await page.route('**/api/auth/csrf', route => route.fulfill({
    status: 503,
    json: { success: false, statusCode: 503, message: 'Token request temporarily unavailable.' }
  }));

  await page.goto('/setup-initial-admin');
  await page.getByLabel('Username', { exact: true }).fill('new-admin');
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Password', { exact: true }).fill('Password1234!');
  await page.getByLabel('Confirm password', { exact: true }).fill('Password1234!');
  await page.getByRole('button', { name: 'Create admin' }).click();

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('button', { name: 'Create admin' })).toHaveCount(0);
  await expect(page.getByText(/Your admin account was created, but sign-in could not be completed/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Login' })).toBeEnabled();
  expect(registrationCount).toBe(1);
});
