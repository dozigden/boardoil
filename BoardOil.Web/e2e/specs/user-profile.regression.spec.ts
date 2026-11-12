import { randomUUID } from 'node:crypto';
import { authenticatePage, expect, test } from '../fixtures/boardOilTest';

test('experimental features opt-in applies on Save and survives a new session', async ({ api, page }) => {
  const userName = `experimental-${randomUUID().slice(0, 8)}`;
  const password = 'ProfilePassword1234!';
  await api.createUser(userName, password);
  await authenticatePage(page, userName, password);
  await page.goto('/user-admin/profile');
  const experimental = page.getByRole('checkbox', { name: 'Experimental features', exact: true });
  const save = page.getByRole('button', { name: 'Save profile', exact: true });

  await test.step('leave an unsaved opt-in and discard it', async () => {
    await expect(experimental).not.toBeChecked();
    await expect(save).toBeDisabled();
    await experimental.check();
    await page.getByRole('link', { name: 'Theme', exact: true }).click();
    await page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true })
      .getByRole('button', { name: 'Discard', exact: true }).click();
    await page.getByRole('link', { name: 'Profile', exact: true }).click();
    await expect(experimental).not.toBeChecked();
  });

  await test.step('save the opt-in and restore it after signing in again', async () => {
    await experimental.check();
    await save.click();
    await expect(save).toBeDisabled();
    await page.screenshot({ path: test.info().outputPath('experimental-features-profile.png') });
    await page.getByRole('button', { name: 'User menu', exact: true }).click();
    await page.getByRole('button', { name: 'Logout', exact: true }).click();
    await expect(page).toHaveURL(/\/login$/);
    await authenticatePage(page, userName, password);
    await page.goto('/user-admin/profile');
    await expect(experimental).toBeChecked();
  });

  await test.step('save an opt-out and retain it on reload', async () => {
    await experimental.uncheck();
    await save.click();
    await expect(save).toBeDisabled();
    await page.reload();
    await expect(experimental).not.toBeChecked();
  });
});

test('saving a profile updates the account display without refetching', async ({ api, page }) => {
  const userName = `profile-${randomUUID().slice(0, 8)}`;
  const password = 'ProfilePassword1234!';
  await api.createUser(userName, password);
  await authenticatePage(page, userName, password);
  let profileLoads = 0;
  page.on('request', request => {
    if (request.method() === 'GET' && new URL(request.url()).pathname === '/api/users/me') {
      profileLoads++;
    }
  });

  await page.goto('/user-admin/profile');
  await expect(page.getByLabel('Email', { exact: true })).toHaveValue(`${userName}@boardoil.test`);
  await page.getByLabel('Display name', { exact: true }).fill('  Updated profile  ');
  await page.getByLabel('Email', { exact: true }).fill(`${userName}-updated@boardoil.test`);
  await page.getByRole('button', { name: 'Save profile', exact: true }).click();
  await expect(page.getByRole('complementary', { name: 'Updated profile sections', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeDisabled();
  await expect(page.getByLabel('Display name', { exact: true })).toHaveValue('Updated profile');
  await expect(page.getByLabel('Email', { exact: true })).toHaveValue(`${userName}-updated@boardoil.test`);
  expect(profileLoads).toBe(1);

  await page.getByRole('link', { name: 'Theme', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Theme', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Profile', exact: true }).click();
  await expect(page.getByLabel('Email', { exact: true })).toHaveValue(`${userName}-updated@boardoil.test`);
  await expect(page.getByLabel('Display name', { exact: true })).toHaveValue('Updated profile');
  expect(profileLoads).toBe(2);
});

test('profile field errors retain the draft and navigation can discard it', async ({ api, page }) => {
  const userName = `profile-errors-${randomUUID().slice(0, 8)}`;
  const otherName = `profile-other-${randomUUID().slice(0, 8)}`;
  const password = 'ProfilePassword1234!';
  await api.createUser(userName, password);
  await api.createUser(otherName, password);
  await authenticatePage(page, userName, password);
  await page.goto('/user-admin/profile');
  const name = page.getByLabel('Display name', { exact: true });
  const email = page.getByLabel('Email', { exact: true });
  const save = page.getByRole('button', { name: 'Save profile', exact: true });
  await expect(email).toHaveValue(`${userName}@boardoil.test`);
  await name.fill('');
  await email.fill(`${otherName}@boardoil.test`);
  await save.click();
  await expect(name).toHaveAccessibleDescription('Display name is required.');
  await expect(name).toBeFocused();
  await expect(email).toHaveValue(`${otherName}@boardoil.test`);
  await name.fill('Retained draft');
  await save.click();
  await expect(email).toHaveAccessibleDescription('Email already exists.');
  await expect(email).toBeFocused();
  await expect(name).toHaveValue('Retained draft');
  await expect(page.getByText('Email already exists.', { exact: true })).toHaveCount(1);
  await page.screenshot({ path: test.info().outputPath('profile-validation.png') });

  await page.getByRole('link', { name: 'Theme', exact: true }).click();
  const discard = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
  await discard.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(email).toHaveValue(`${otherName}@boardoil.test`);
  await page.getByRole('link', { name: 'Theme', exact: true }).click();
  await discard.getByRole('button', { name: 'Discard', exact: true }).click();
  await expect(page).toHaveURL('/user-admin/theme');
  await page.getByRole('link', { name: 'Profile', exact: true }).click();
  await expect(name).toHaveValue(userName);
  await expect(email).toHaveValue(`${userName}@boardoil.test`);
});

test('a failed initial profile load can be retried before editing', async ({ authenticatedPage: page }) => {
  await page.route('**/api/users/me', async route => {
    await route.fulfill({ status: 503, json: { success: false, message: 'Profile temporarily unavailable.' } });
  }, { times: 1 });
  await page.goto('/user-admin/profile');
  await expect(page.getByRole('alert')).toHaveText('Profile temporarily unavailable.');
  await expect(page.getByLabel('Display name', { exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Retry', exact: true }).click();
  await expect(page.getByLabel('Display name', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeDisabled();
});
