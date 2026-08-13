import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Page } from '@playwright/test';

type StorageAccess = {
  method: string;
  key: string | null;
};

type InstrumentedWindow = Window & {
  __demoStorageAccesses: StorageAccess[];
};

const ExpectedInstallationUrl = 'https://boardoil.dozigden.com/installation/';

test('built demo remains static, resettable, and safe', async ({ context, page }) => {
  const securityPolicy = await loadDemoSecurityPolicy();
  const forbiddenRequests: string[] = [];
  const failedRequests: string[] = [];
  const consoleErrors: string[] = [];

  await verifyDeploymentProvenance();

  await instrumentStorage(page);
  await applySecurityPolicy(page, securityPolicy);

  page.on('request', request => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/api') || url.pathname.startsWith('/hubs')) {
      forbiddenRequests.push(request.url());
    }
  });
  page.on('requestfailed', request => {
    failedRequests.push(`${request.url()} ${request.failure()?.errorText ?? 'unknown failure'}`);
  });
  page.on('console', message => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });

  await test.step('load the seeded board', async () => {
    await page.goto('/');
    await expect(page.getByText('Live Demo', { exact: true })).toBeVisible();

    const doneColumn = page.getByRole('article', { name: 'Done column' });
    await expect(doneColumn).toBeVisible();
    await expect(doneColumn.getByRole('button').filter({ hasText: /#\d+/ })).toHaveCount(10);
  });

  await test.step('edit and move a card in memory', async () => {
    const ideasColumn = page.getByRole('article', { name: 'Ideas column' });
    await ideasColumn.getByRole('button').filter({ hasText: 'Customer interview highlights' }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: /^#101 / }).click();
    const titleInput = dialog.getByRole('textbox', { name: 'Card title' });
    await titleInput.fill('Edited in the static demo');
    await titleInput.press('Enter');
    await dialog.getByTitle('Select column').click();
    await dialog
      .getByRole('menu', { name: 'Select column' })
      .getByRole('button', { name: 'Ready', exact: true })
      .click();
    await dialog.getByRole('button', { name: 'Save card' }).click();

    const readyColumn = page.getByRole('article', { name: 'Ready column' });
    await expect(readyColumn.getByRole('button').filter({ hasText: 'Edited in the static demo' })).toBeVisible();
  });

  await test.step('reload the original fixture', async () => {
    await page.reload();
    const ideasColumn = page.getByRole('article', { name: 'Ideas column' });
    await expect(ideasColumn.getByRole('button').filter({ hasText: 'Customer interview highlights' })).toBeVisible();
    await expect(page.getByText('Edited in the static demo', { exact: true })).toHaveCount(0);
  });

  await test.step('exercise demo-only controls', async () => {
    const installationLink = page.getByRole('link', { name: 'Get BoardOil' });
    await expect(installationLink).toHaveAttribute('href', ExpectedInstallationUrl);

    await page.getByRole('button', { name: 'Use dark mode' }).click();
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');

    await page.getByRole('button', { name: 'View third-party licences' }).click();
    const licencesDialog = page.getByRole('dialog');
    await expect(licencesDialog).toBeVisible();
    await expect(licencesDialog.getByText('Montserrat Font', { exact: true })).toBeVisible();
    await licencesDialog.getByRole('button', { name: 'Close licences' }).click();
  });

  await test.step('verify the static safety boundary', async () => {
    const storageState = await page.evaluate(() => ({
      accesses: (window as unknown as InstrumentedWindow).__demoStorageAccesses,
      localStorageLength: globalThis.localStorage.length,
      sessionStorageLength: globalThis.sessionStorage.length
    }));

    expect(storageState).toEqual({
      accesses: [],
      localStorageLength: 0,
      sessionStorageLength: 0
    });
    expect(await context.cookies()).toEqual([]);
    expect(forbiddenRequests).toEqual([]);
    expect(failedRequests).toEqual([]);
    expect(consoleErrors).toEqual([]);
  });
});

async function verifyDeploymentProvenance() {
  const deploymentPath = path.resolve(process.cwd(), 'dist-demo', 'deployment.json');
  const noticePath = path.resolve(process.cwd(), 'dist-demo', 'DISTRIBUTION_NOTICE.txt');
  const deployment = JSON.parse(await readFile(deploymentPath, 'utf8')) as {
    artifact: string;
    sourceRepository: string;
    sourceCommit: string;
  };
  const expectedCommit = process.env.BOARDOIL_DEMO_SOURCE_SHA?.trim() || 'local';

  expect(deployment).toEqual({
    artifact: 'static-demo',
    sourceRepository: 'https://github.com/dozigden/boardoil',
    sourceCommit: expectedCommit
  });

  const notice = await readFile(noticePath, 'utf8');
  expect(notice).toContain('This repository is generated. Do not edit its files directly.');
  expect(notice).toContain(`Commit: ${expectedCommit}`);
}

async function loadDemoSecurityPolicy() {
  const headersPath = path.resolve(process.cwd(), 'dist-demo', '_headers');
  const headers = await readFile(headersPath, 'utf8');

  expect(headers).toContain("frame-ancestors 'self' https://boardoil.dozigden.com");
  expect(headers).toContain('Permissions-Policy: camera=(), geolocation=(), microphone=(), payment=(), usb=()');
  expect(headers).toContain('Referrer-Policy: no-referrer');
  expect(headers).toContain('X-Robots-Tag: noindex, nofollow');
  expect(headers).toContain('Cache-Control: public, max-age=31536000, immutable');

  const policyLine = headers
    .split(/\r?\n/)
    .map(line => line.trim())
    .find(line => line.startsWith('Content-Security-Policy:'));
  expect(policyLine).toBeDefined();
  return policyLine!.slice('Content-Security-Policy:'.length).trim();
}

async function instrumentStorage(page: Page) {
  await page.addInitScript(() => {
    const instrumentedWindow = window as unknown as InstrumentedWindow;
    instrumentedWindow.__demoStorageAccesses = [];

    const record = (method: string, key: string | null = null) => {
      instrumentedWindow.__demoStorageAccesses.push({ method, key });
    };
    const originalGetItem = Storage.prototype.getItem;
    const originalSetItem = Storage.prototype.setItem;
    const originalRemoveItem = Storage.prototype.removeItem;
    const originalClear = Storage.prototype.clear;
    const originalKey = Storage.prototype.key;

    Storage.prototype.getItem = function getItem(key: string) {
      record('getItem', key);
      return originalGetItem.call(this, key);
    };
    Storage.prototype.setItem = function setItem(key: string, value: string) {
      record('setItem', key);
      originalSetItem.call(this, key, value);
    };
    Storage.prototype.removeItem = function removeItem(key: string) {
      record('removeItem', key);
      originalRemoveItem.call(this, key);
    };
    Storage.prototype.clear = function clear() {
      record('clear');
      originalClear.call(this);
    };
    Storage.prototype.key = function key(index: number) {
      record('key');
      return originalKey.call(this, index);
    };
  });
}

async function applySecurityPolicy(page: Page, policy: string) {
  await page.route('**/*', async route => {
    const response = await route.fetch();
    await route.fulfill({
      response,
      headers: {
        ...response.headers(),
        'content-security-policy': policy
      }
    });
  });
}
