import { createHash } from 'node:crypto';
import { expect, test } from '../fixtures/boardOilTest';

test('diagnostic PAT permission is optional and can be selected explicitly', async ({ authenticatedPage: page }, testInfo) => {
  await page.goto('/user-admin/authentication/access-tokens');
  await page.getByRole('button', { name: 'Create access token', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Create Access Token', exact: true });
  const diagnostic = dialog.getByRole('checkbox', { name: /mcp:diagnostics:read/ });
  await expect(diagnostic).not.toBeChecked();
  await expect(dialog.getByRole('checkbox', { name: /^mcp:read / })).toBeChecked();
  await expect(dialog.getByRole('checkbox', { name: /^mcp:write / })).toBeChecked();
  await diagnostic.check();
  await page.screenshot({ path: testInfo.outputPath('diagnostic-pat-permission.png') });
  await dialog.getByLabel('Token name', { exact: true }).fill('Diagnostic browser test');
  const created = page.waitForResponse(response => response.request().method() === 'POST'
    && new URL(response.url()).pathname === '/api/auth/access-tokens');

  await dialog.getByRole('button', { name: 'Create token', exact: true }).click();

  const response = await created;
  expect(response.ok()).toBe(true);
  const body = await response.json() as { data: { token: { scopes: string[] } } };
  expect(body.data.token.scopes).toEqual(['mcp:read', 'mcp:write', 'mcp:diagnostics:read']);
});

test('OAuth diagnostic consent starts unchecked and explains system-wide access', async ({ api, authenticatedPage: page }, testInfo) => {
  const scopes = 'mcp:read mcp:diagnostics:read';
  const oauth = await api.registerOAuthClient('Diagnostic consent browser test',
    'http://127.0.0.1:49152/callback/diagnostics', scopes);
  const verifier = 'diagnostic-browser-code-verifier-12345678901234567890';
  const url = new URL(`${oauth.authorizationServer}/connect/authorize`);
  url.search = new URLSearchParams({
    client_id: oauth.clientId, redirect_uri: oauth.redirectUri, response_type: 'code', scope: scopes,
    state: 'diagnostic-browser-consent', resource: oauth.resource,
    code_challenge: createHash('sha256').update(verifier).digest('base64url'), code_challenge_method: 'S256'
  }).toString();

  await page.goto(url.toString());

  const diagnostic = page.getByRole('checkbox', { name: /mcp:diagnostics:read/ });
  await expect(diagnostic).not.toBeChecked();
  await expect(page.getByRole('checkbox', { name: 'mcp:read', exact: true })).toBeChecked();
  await expect(page.getByText(/Diagnostic access can read system-wide application errors/)).toBeVisible();
  await diagnostic.check();
  await expect(diagnostic).toBeChecked();
  await page.screenshot({ path: testInfo.outputPath('diagnostic-oauth-consent.png'), fullPage: true });
});
