import { expect, test } from '../fixtures/boardOilTest';
import { rejectNextTokenCreation } from '../support/tokenCreationResponse';
import { TokenCreationDialog } from '../ui/TokenCreationDialog';
import type { CreatedAccessToken } from '../../src/shared/types/authTypes';

for (const kind of ['personal', 'client'] as const) {
  test(`${kind} token retains field errors and passes a successful retry to the secret dialog`, async ({ api, authenticatedPage: page }) => {
    let endpoint = '/api/auth/access-tokens';
    if (kind === 'client') {
      const client = await api.createClientAccount('token-form-client');
      endpoint = `/api/system/client-accounts/${client.id}/tokens`;
      await page.setViewportSize({ width: 390, height: 844 });
      await page.goto(`/admin/system/client-accounts/${client.id}/tokens`);
    } else {
      await page.goto('/user-admin/authentication/access-tokens');
    }
    const form = new TokenCreationDialog(page);
    await form.open(kind);
    const apiScope = form.scopes.getByRole('checkbox', { name: /^api:read / });
    if (kind === 'client') await expect(apiScope).toBeChecked();
    else await expect(apiScope).toHaveCount(0);
    const tokenName = `${kind} form retry`;
    await form.name.fill('   ');
    await form.days.fill('3651');
    for (const checkbox of await form.scopes.getByRole('checkbox').all()) await checkbox.uncheck();
    const rejected = page.waitForResponse(r => r.request().method() === 'POST' && new URL(r.url()).pathname === endpoint);
    await form.save.click();
    expect((await rejected).status()).toBe(400);
    await expect(form.name).toHaveAccessibleDescription('Token name is required.');
    await expect(form.name).toBeFocused();
    await expect(form.scopes).toHaveAccessibleDescription('At least one scope is required.');
    await expect(form.expiry).toHaveAccessibleDescription('Expiry must be between 1 and 3650 days.');
    await expect(page.getByText('Expiry must be between 1 and 3650 days.', { exact: true })).toHaveCount(1);
    await expect(form.name).toHaveValue('   ');
    await expect(form.days).toHaveValue('3651');
    await page.screenshot({ path: test.info().outputPath(`${kind}-token-validation.png`) });

    await form.name.fill(tokenName);
    const correctedScope = kind === 'client' ? /^mcp:read / : /^mcp:diagnostics:read /;
    await form.scopes.getByRole('checkbox', { name: correctedScope }).check();
    await expect(form.scopes).not.toHaveAttribute('aria-invalid', 'true');
    await expect(form.expiry).toHaveAttribute('aria-invalid', 'true');
    await form.days.fill('10');
    await expect(form.expiry).not.toHaveAttribute('aria-invalid', 'true');
    await rejectNextTokenCreation(page, endpoint, { message: 'Token creation is temporarily unavailable.' });
    await form.save.click();
    await expect(form.dialog.getByRole('alert')).toHaveText('Token creation is temporarily unavailable.');
    await expect(form.dialog.getByRole('alert')).toBeFocused();
    await expect(page.getByText('Token creation is temporarily unavailable.', { exact: true })).toHaveCount(1);

    // Use a real successful response to verify that the one-time secret survives orchestration.
    const response = page.waitForResponse(r => r.request().method() === 'POST' && new URL(r.url()).pathname === endpoint);
    await form.save.click();
    const result = await response;
    expect(result.ok()).toBe(true);
    const body = await result.json() as { data: CreatedAccessToken };
    await expect(form.dialog).toBeHidden();
    await expect(form.secret).toBeVisible();
    await expect(form.secret.getByText(body.data.plainTextToken, { exact: true })).toBeVisible();
    await form.secret.getByRole('button', { name: 'Done', exact: true }).click();
    await expect(page.getByText(tokenName, { exact: true })).toBeVisible();
    await form.open(kind);
    await expect(form.name).toHaveValue('');
    await expect(form.days).toHaveValue('30');
    await expect(form.dialog.getByRole('alert')).toHaveCount(0);
  });
}

test('only acknowledgement is checked locally and scope errors come from the server', async ({ authenticatedPage: page }) => {
  await page.goto('/user-admin/authentication/access-tokens');
  const form = new TokenCreationDialog(page);
  await form.open();
  await form.name.fill('Acknowledged token');
  await form.scopes.getByRole('checkbox', { name: /^mcp:read / }).uncheck();
  await form.scopes.getByRole('checkbox', { name: /^mcp:write / }).uncheck();
  await form.nonExpiring.check();
  let submissions = 0;
  page.on('request', r => { if (r.method() === 'POST' && r.url().endsWith('/api/auth/access-tokens')) submissions++; });
  await form.save.click();
  await expect(form.scopes).not.toHaveAttribute('aria-invalid', 'true');
  await expect(form.expiry).toHaveAccessibleDescription('Confirm non-expiring token risk before creating.');
  expect(submissions).toBe(0);
  await form.acknowledgement.check();
  await form.save.click();
  await expect(form.scopes).toHaveAccessibleDescription('At least one scope is required.');
  expect(submissions).toBe(1);
  await expect(form.expiry).not.toHaveAttribute('aria-invalid', 'true');
  await form.scopes.getByRole('checkbox', { name: /^mcp:read / }).check();
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
  const response = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith('/api/auth/access-tokens'));
  await form.save.click();
  const result = await response;
  expect(result.request().postDataJSON()).toEqual({ name: 'Acknowledged token', scopes: ['mcp:read'], expiresInDays: null });
  expect(result.ok()).toBe(true);
  await expect(form.secret).toBeVisible();
});

test('pending token creation blocks editing and cancellation, then allows a clean new session', async ({ authenticatedPage: page }) => {
  await page.goto('/user-admin/authentication/access-tokens');
  const form = new TokenCreationDialog(page);
  await form.open();
  await form.name.fill('Pending token');
  await form.nonExpiring.check();
  await form.acknowledgement.check();
  let release!: () => void;
  const responseReady = new Promise<void>(resolve => { release = resolve; });
  await rejectNextTokenCreation(page, '/api/auth/access-tokens', { message: 'Please retry.' }, responseReady);
  let submissions = 0;
  page.on('request', r => { if (r.method() === 'POST' && r.url().endsWith('/api/auth/access-tokens')) submissions++; });
  try {
    await form.name.press('Enter');
    await expect(form.save).toBeDisabled();
    await expect(form.name).toBeDisabled();
    await expect(form.nonExpiring).toBeDisabled();
    await expect(form.acknowledgement).toBeDisabled();
    await expect(form.scopes.getByRole('checkbox', { name: /^mcp:read / })).toBeDisabled();
    await page.keyboard.press('Escape');
    await form.dialog.getByRole('button', { name: 'Cancel access token creation', exact: true }).click();
    await page.keyboard.press('Enter');
    await expect(form.dialog).toBeVisible();
    expect(submissions).toBe(1);
  } finally {
    release();
  }
  await expect(form.dialog.getByRole('alert')).toHaveText('Please retry.');
  await form.cancel();
  await form.open();
  await expect(form.name).toHaveValue('');
  await expect(form.nonExpiring).not.toBeChecked();
  await expect(form.days).toHaveValue('30');
  await form.nonExpiring.check();
  await expect(form.acknowledgement).not.toBeChecked();
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
});
