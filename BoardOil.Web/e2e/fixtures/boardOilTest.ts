import { test as base, expect, type Page } from '@playwright/test';
import { BoardOilApi } from '../support/boardOilApi';

export const ADMIN_USER_NAME = 'smoke-admin';
export const ADMIN_PASSWORD = 'SmokePassword1234!';

type BoardOilFixtures = {
  api: BoardOilApi;
  authenticatedPage: Page;
};

export const test = base.extend<BoardOilFixtures>({
  api: async ({ request }, use) => {
    const api = new BoardOilApi(request);
    await api.ensureInitialAdmin(ADMIN_USER_NAME, ADMIN_PASSWORD);
    await use(api);
  },
  authenticatedPage: async ({ api, page }, use) => {
    await authenticatePage(page);
    await use(page);
  }
});

export { expect };

export async function authenticatePage(page: Page) {
  const response = await page.request.post('/api/auth/login', {
    data: {
      userName: ADMIN_USER_NAME,
      password: ADMIN_PASSWORD
    }
  });
  expect(response.ok(), await response.text()).toBe(true);
}

export function getBaseUrl() {
  return process.env.BOARDOIL_E2E_BASE_URL ?? 'http://127.0.0.1:4173';
}
