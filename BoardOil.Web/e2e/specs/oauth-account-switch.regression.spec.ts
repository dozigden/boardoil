import { createHash } from 'node:crypto';
import { ADMIN_PASSWORD, ADMIN_USER_NAME, expect, test } from '../fixtures/boardOilTest';

const SWITCHED_USER_NAME = 'oauth-browser-user';
const SWITCHED_USER_PASSWORD = 'OAuthBrowserPassword1234!';
const REDIRECT_URI = 'http://127.0.0.1:49152/callback/playwright';

test('switch BoardOil user from the OAuth consent page without losing the authorization request', async ({
  api,
  page
}) => {
  await api.createUser(SWITCHED_USER_NAME, SWITCHED_USER_PASSWORD);
  const oauthClient = await api.registerOAuthClient('Playwright OAuth', REDIRECT_URI);
  const codeVerifier = 'playwright-oauth-code-verifier-12345678901234567890';
  const codeChallenge = createHash('sha256').update(codeVerifier).digest('base64url');
  const expectedParameters = {
    client_id: oauthClient.clientId,
    redirect_uri: oauthClient.redirectUri,
    response_type: 'code',
    scope: 'mcp:read',
    state: 'playwright-account-switch-state',
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
    resource: oauthClient.resource
  };
  const authorizationUrl = new URL(`${oauthClient.authorizationServer}/connect/authorize`);
  for (const [name, value] of Object.entries(expectedParameters)) {
    authorizationUrl.searchParams.set(name, value);
  }

  await test.step('sign in as the current OAuth user', async () => {
    await page.goto(authorizationUrl.toString());
    await page.getByLabel('Username').fill(ADMIN_USER_NAME);
    await page.getByLabel('Password').fill(ADMIN_PASSWORD);
    await page.getByRole('button', { name: 'Sign in' }).click();

    await expect(page.getByRole('heading', { name: 'Authorise Playwright OAuth' })).toBeVisible();
    await expect(page.getByText(`${ADMIN_USER_NAME} (@${ADMIN_USER_NAME})`)).toBeVisible();
    assertAuthorizationParameters(page.url(), expectedParameters);
  });

  await test.step('switch account through the rendered consent form', async () => {
    await page.getByText('Sign in as another user', { exact: true }).click();
    await page.getByLabel('Username').fill(SWITCHED_USER_NAME);
    await page.getByLabel('Password').fill(SWITCHED_USER_PASSWORD);
    await page.getByRole('button', { name: 'Switch account' }).click();

    await expect(page.getByText(`${SWITCHED_USER_NAME} (@${SWITCHED_USER_NAME})`)).toBeVisible();
    assertAuthorizationParameters(page.url(), expectedParameters);
  });
});

function assertAuthorizationParameters(
  currentUrl: string,
  expectedParameters: Record<string, string>
) {
  const actualUrl = new URL(currentUrl);
  expect(actualUrl.pathname).toBe('/connect/authorize');
  for (const [name, value] of Object.entries(expectedParameters)) {
    expect(actualUrl.searchParams.get(name), `authorization parameter ${name}`).toBe(value);
  }
}
