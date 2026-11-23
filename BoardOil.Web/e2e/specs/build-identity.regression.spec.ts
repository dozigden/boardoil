import { expect, test } from '../fixtures/boardOilTest';

// The package accepts HTTPS forwarding from its loopback reverse proxy.
test.use({ extraHTTPHeaders: { 'X-Forwarded-Proto': 'https' } });

test('About shows a shared build identity for the served frontend and API', async ({ authenticatedPage: page }) => {
  await page.goto('/');
  await page.getByRole('button', { name: 'System admin', exact: true }).click();
  await page.getByRole('button', { name: 'About', exact: true }).click();

  const summary = page.getByRole('dialog', { name: 'About BoardOil' }).getByRole('region', { name: 'Build summary' });
  // About shows Runtime only when both sides agree, including the full commit.
  await expect(summary.getByText('Runtime', { exact: true })).toBeVisible();
});
