import type { Page } from '@playwright/test';

export async function expirePasswordResetSession(page: Page) {
  const reject = { status: 401, json: { success: false, message: 'Session expired.' } };
  await page.route('**/api/auth/change-password', route => route.fulfill(reject));
  await page.route('**/api/auth/refresh', route => route.fulfill(reject));
}
