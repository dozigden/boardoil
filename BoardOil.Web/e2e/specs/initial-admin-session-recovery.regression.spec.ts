import { expect, test } from '../fixtures/boardOilTest';

test.beforeEach(async ({ page }) => {
  await page.route('**/api/auth/me', route => route.fulfill({
    json: { success: true, data: null, statusCode: 200 }
  }));
  await page.route('**/api/auth/bootstrap-status', route => route.fulfill({
    json: { success: true, data: { requiresInitialAdminSetup: true }, statusCode: 200 }
  }));
});

const timezoneOptions = {
  success: true, statusCode: 200,
  data: {
    defaultId: 'UTC',
    options: [{ id: 'UTC', displayName: 'UTC [UTC+00:00]' }, { id: 'Europe/London', displayName: 'Europe/London' }]
  }
};

test('initial setup loads timezone choices and offers sign-in if session restoration fails after registration', async ({ page }) => {
  let registrationCount = 0;
  let registrationPayload: unknown;
  let releaseOptions!: () => void;
  const optionsReady = new Promise<void>(resolve => { releaseOptions = resolve; });
  await page.route('**/api/auth/timezone-options', async route => {
    await optionsReady;
    await route.fulfill({ json: timezoneOptions });
  });
  await page.route('**/api/auth/register-initial-admin', route => {
    registrationCount++;
    registrationPayload = route.request().postDataJSON();
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
  await expect(page.getByRole('status')).toHaveText('Loading setup...');
  await expect(page.getByRole('button', { name: 'Create admin' })).toHaveCount(0);
  releaseOptions();
  await page.getByLabel('Username', { exact: true }).fill('new-admin');
  await expect(page.getByRole('status')).toHaveCount(0);
  await page.getByLabel('Email', { exact: true }).fill('admin@example.test');
  await page.getByLabel('Password', { exact: true }).fill('Password1234!');
  await page.getByLabel('Confirm password', { exact: true }).fill('Password1234!');
  await page.getByRole('combobox', { name: 'System timezone' }).fill('London');
  await page.getByRole('option', { name: 'Europe/London', exact: true }).click();
  await page.getByRole('button', { name: 'Create admin' }).click();

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('button', { name: 'Create admin' })).toHaveCount(0);
  await expect(page.getByText(/Your admin account was created, but sign-in could not be completed/)).toBeVisible();
  await expect(page.getByRole('button', { name: 'Login' })).toBeEnabled();
  expect(registrationCount).toBe(1);
  expect(registrationPayload).toMatchObject({ systemTimeZoneId: 'Europe/London' });
});

test('failed timezone loading can be retried before initial admin creation', async ({ page }) => {
  let attempts = 0;
  await page.route('**/api/auth/timezone-options', route => {
    attempts++;
    if (attempts === 1) {
      return route.fulfill({ status: 503, json: { success: false, statusCode: 503, message: 'Timezone options unavailable.' } });
    }
    return route.fulfill({ json: timezoneOptions });
  });

  await page.goto('/setup-initial-admin');
  await expect(page.getByRole('alert')).toHaveText('Timezone options unavailable.');
  await expect(page.getByRole('button', { name: 'Create admin' })).toHaveCount(0);
  await page.getByRole('button', { name: 'Retry' }).click();

  await expect(page.getByRole('combobox', { name: 'System timezone' })).toHaveValue('UTC [UTC+00:00]');
  await expect(page.getByRole('button', { name: 'Create admin' })).toBeEnabled();
  await expect(page.getByRole('alert')).toHaveCount(0);
  expect(attempts).toBe(2);
});
