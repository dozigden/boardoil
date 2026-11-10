import { expect, test } from '../fixtures/boardOilTest';
import { rejectNextTokenCreation } from '../support/tokenCreationResponse';
import { ClientAccountCreationDialog } from '../ui/ClientAccountCreationDialog';
import type { CreatedClientAccount } from '../../src/shared/types/authTypes';

const endpoint = '/api/system/client-accounts';

test('client creation retains account and token errors and hands the real secret to its existing dialog', async ({ authenticatedPage: page }) => {
  await page.goto('/admin/system/client-accounts');
  const form = new ClientAccountCreationDialog(page);
  await form.open();
  await expect(form.tokenName).toHaveValue('Initial token');
  await expect(form.days).toHaveValue('30');
  for (const scope of ['api:read', 'api:write', 'api:admin', 'api:system']) {
    await expect(form.scopes.getByRole('checkbox', { name: new RegExp(`^${scope} `) })).toBeChecked();
  }
  await expect(form.scopes.getByRole('checkbox', { name: /^mcp:read / })).not.toBeChecked();
  await form.save.click();
  await expect(form.username).toHaveAccessibleDescription('Username is required.');
  await expect(form.username).toBeFocused();
  await expect(form.displayName).toHaveAccessibleDescription('Display name is required.');
  await expect(form.email).toHaveAccessibleDescription('Email is required.');

  await form.fill('validation-client');
  await form.tokenName.fill('x'.repeat(121));
  for (const checkbox of await form.scopes.getByRole('checkbox').all()) await checkbox.uncheck();
  await form.days.fill('3651');
  await form.save.click();
  await expect(form.tokenName).toHaveAttribute('aria-invalid', 'true');
  await expect(form.tokenName).toBeFocused();
  await expect(form.scopes).toHaveAccessibleDescription('At least one scope is required.');
  await expect(form.expiry).toHaveAccessibleDescription('Expiry must be between 1 and 3650 days.');
  await expect(form.username).toHaveValue('validation-client');
  await expect(form.days).toHaveValue('3651');
  await page.screenshot({ path: test.info().outputPath('client-account-validation.png') });

  await form.tokenName.fill('');
  await form.scopes.getByRole('checkbox', { name: /^mcp:read / }).check();
  await expect(form.scopes).not.toHaveAttribute('aria-invalid', 'true');
  await expect(form.expiry).toHaveAttribute('aria-invalid', 'true');
  await form.days.fill('10');
  const response = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith(endpoint));
  await form.save.click();
  const result = await response;
  expect(result.status()).toBe(201);
  const body = await result.json() as { data: CreatedClientAccount };
  expect(body.data.token.token.name).toBe('Initial token');
  expect(body.data.token.token.scopes).toEqual(['mcp:read']);
  await expect(form.dialog).toBeHidden();
  await expect(form.secret.getByText(body.data.token.plainTextToken, { exact: true })).toBeVisible();
  await form.secret.getByRole('button', { name: 'Done', exact: true }).click();
  await expect(page.getByText('validation-client', { exact: true })).toBeVisible();
  await form.open();
  await expect(form.username).toHaveValue('');
  await expect(form.tokenName).toHaveValue('Initial token');
  await expect(form.days).toHaveValue('30');
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
});

test('duplicate client account fields allow correction and retry on a narrow screen', async ({ api, authenticatedPage: page }) => {
  await api.createClientAccount('existing-client');
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/admin/system/client-accounts');
  const form = new ClientAccountCreationDialog(page);
  await form.open();
  await form.fill('existing-client');
  await form.email.fill('existing-client@boardoil.test');
  await form.save.click();
  await expect(form.username).toHaveAccessibleDescription('Username already exists.');
  await expect(form.username).toBeFocused();
  await expect(form.email).toHaveAccessibleDescription('Email already exists.');
  await page.screenshot({ path: test.info().outputPath('client-account-validation-narrow.png') });
  await form.username.fill('corrected-client');
  await expect(form.username).not.toHaveAttribute('aria-invalid', 'true');
  await expect(form.email).toHaveAttribute('aria-invalid', 'true');
  await form.email.fill('corrected-client@example.test');
  await form.save.click();
  await expect(form.secret).toBeVisible();
});

test('client creation keeps acknowledgement local and blocks cancellation and repeated requests while saving', async ({ authenticatedPage: page }) => {
  await page.goto('/admin/system/client-accounts');
  const form = new ClientAccountCreationDialog(page);
  await form.open();
  await form.fill('pending-client');
  await form.nonExpiring.check();
  let submissions = 0;
  page.on('request', r => { if (r.method() === 'POST' && r.url().endsWith(endpoint)) submissions++; });
  await form.save.click();
  await expect(form.expiry).toHaveAccessibleDescription('Confirm non-expiring token risk before creating.');
  expect(submissions).toBe(0);
  await form.acknowledgement.check();
  let release!: () => void;
  const responseReady = new Promise<void>(resolve => { release = resolve; });
  await rejectNextTokenCreation(page, endpoint, { message: 'Please retry.' }, responseReady);
  try {
    await form.username.press('Enter');
    await expect(form.save).toBeDisabled();
    await expect(form.username).toBeDisabled();
    await expect(form.nonExpiring).toBeDisabled();
    await expect(form.acknowledgement).toBeDisabled();
    await expect(form.scopes.getByRole('checkbox', { name: /^api:read / })).toBeDisabled();
    await page.keyboard.press('Escape');
    await form.dialog.getByRole('button', { name: 'Cancel client account creation', exact: true }).click();
    await page.keyboard.press('Enter');
    await expect(form.dialog).toBeVisible();
    expect(submissions).toBe(1);
  } finally {
    release();
  }
  await expect(form.dialog.getByRole('alert')).toHaveText('Please retry.');
  await expect(form.dialog.getByRole('alert')).toBeFocused();
  await expect(page.getByText('Please retry.', { exact: true })).toHaveCount(1);
  const response = page.waitForResponse(r => r.request().method() === 'POST' && r.url().endsWith(endpoint));
  await form.save.click();
  const result = await response;
  expect(result.request().postDataJSON()).toEqual({
    userName: 'pending-client', displayName: 'Validation Client', email: 'pending-client@example.test', role: 'Standard',
    tokenName: 'Initial token', scopes: ['api:read', 'api:write', 'api:admin', 'api:system'], expiresInDays: null
  });
  expect(result.ok()).toBe(true);
  await expect(form.secret).toBeVisible();
  await form.secret.getByRole('button', { name: 'Done', exact: true }).click();
  await form.open();
  await expect(form.nonExpiring).not.toBeChecked();
  await form.nonExpiring.check();
  await expect(form.acknowledgement).not.toBeChecked();
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
});
