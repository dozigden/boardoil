import { randomUUID } from 'node:crypto';
import { authenticatePage, expect, test } from '../fixtures/boardOilTest';

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
  await page.getByLabel('Display name', { exact: true }).fill('Updated profile');
  await page.getByLabel('Email', { exact: true }).fill(`${userName}-updated@boardoil.test`);
  await page.getByRole('button', { name: 'Save profile', exact: true }).click();
  await expect(page.getByRole('complementary', { name: 'Updated profile sections', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Save profile', exact: true })).toBeEnabled();
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
