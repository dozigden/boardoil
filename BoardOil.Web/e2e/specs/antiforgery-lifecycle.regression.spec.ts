import { randomUUID } from 'node:crypto';
import { authenticatePage, expect, test } from '../fixtures/boardOilTest';
import { BoardOilApi } from '../support/boardOilApi';

for (const refreshRequired of [false, true]) {
  test(`a stale tab rejects a profile write after an account switch${refreshRequired ? ' and refresh' : ''}`, async ({
    api, authenticatedPage: page, context
  }) => {
    const userName = `csrf-${refreshRequired ? 'refresh' : 'direct'}-${randomUUID().slice(0, 8)}`;
    const password = 'SwitchPassword1234!';
    await api.createUser(userName, password);
    const originalProfile = await api.getOwnProfile();

    await test.step('leave an unsaved edit in the original tab', async () => {
      await page.goto('/user-admin/profile');
      await expect(page.getByLabel('Email', { exact: true })).toHaveValue(originalProfile.email);
      await page.getByLabel('Display name', { exact: true }).fill('Must not be saved');
    });

    const secondTab = await context.newPage();
    await test.step('switch the shared cookies to another account in a second tab', async () => {
      // Sign in without logging out, preserving the shared antiforgery cookie.
      // The first tab must be rejected because its request token binds the old identity.
      await authenticatePage(secondTab, userName, password);
      await secondTab.goto('/user-admin/profile');
      await expect(secondTab.getByLabel('Display name', { exact: true })).toHaveValue(userName);
      if (refreshRequired) {
        await context.clearCookies({ name: 'boardoil_access' });
      }
    });

    const requests: Array<{ path: string; method: string }> = [];
    page.on('request', request => requests.push({ path: new URL(request.url()).pathname, method: request.method() }));
    await test.step('reject the stale mutation and clear the local session without replay', async () => {
      const rejectedResponse = page.waitForResponse(response =>
        new URL(response.url()).pathname === '/api/users/me'
        && response.request().method() === 'PUT' && response.status() === 403);
      await page.getByRole('button', { name: 'Save profile' }).click();
      expect(await (await rejectedResponse).json()).toMatchObject({
        success: false, statusCode: 403, message: 'CSRF validation failed.'
      });
      await expect(page).toHaveURL(/\/unauthorized\?redirect=/);
      await expect(page.getByRole('heading', { name: 'Session Expired or Unauthorized' })).toBeVisible();
      await expect(page.getByRole('button', { name: 'User menu' })).toHaveCount(0);

      // With no access cookie, one 401/refresh/retry is expected; never retry the 403.
      expect(requests.filter(request => request.path === '/api/users/me' && request.method === 'PUT'))
        .toHaveLength(refreshRequired ? 2 : 1);
      expect(requests.filter(request => request.path === '/api/auth/refresh')).toHaveLength(refreshRequired ? 1 : 0);
      expect(requests.filter(request => request.path === '/api/auth/csrf')).toHaveLength(0);
      expect(await api.getOwnProfile()).toEqual(originalProfile);
      const currentProfile = await new BoardOilApi(secondTab.request).getOwnProfile();
      expect(currentProfile.userName).toBe(userName);
      expect(currentProfile.displayName).toBe(userName);
      expect(currentProfile.email).toBe(`${userName}@boardoil.test`);
    });
  });
}

test('a same-account write survives access-cookie expiry without replacing the request token', async ({
  api, authenticatedPage: page, context
}) => {
  const originalProfile = await api.getOwnProfile();
  await page.goto('/user-admin/profile');
  await expect(page.getByLabel('Email', { exact: true })).toHaveValue(originalProfile.email);
  // Submit the unchanged profile so this test does not alter the shared admin fixture.
  await context.clearCookies({ name: 'boardoil_access' });
  const tokens: Array<string | undefined> = [];
  let refreshCount = 0;
  let tokenAcquisitionCount = 0;
  page.on('request', request => {
    const path = new URL(request.url()).pathname;
    if (path === '/api/users/me' && request.method() === 'PUT') {
      tokens.push(request.headers()['x-boardoil-csrf']);
    }
    if (path === '/api/auth/refresh') { refreshCount++; }
    if (path === '/api/auth/csrf') { tokenAcquisitionCount++; }
  });

  const savedResponse = page.waitForResponse(response =>
    new URL(response.url()).pathname === '/api/users/me'
    && response.request().method() === 'PUT' && response.status() === 200);
  await page.getByRole('button', { name: 'Save profile' }).click();
  await savedResponse;
  await expect(page.getByText('Saved successfully.', { exact: true })).toBeVisible();

  expect(tokens).toHaveLength(2);
  expect(tokens[0]).toBeTruthy();
  expect(tokens[1]).toBe(tokens[0]);
  expect(refreshCount).toBe(1);
  expect(tokenAcquisitionCount).toBe(0);
  await expect(page).toHaveURL(/\/user-admin\/profile$/);
  expect(await api.getOwnProfile()).toEqual(originalProfile);
});
