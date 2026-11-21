import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { expect, test, type Locator, type Page } from '@playwright/test';

type StorageAccess = {
  method: string;
  key: string | null;
};

type InstrumentedWindow = Window & {
  __demoStorageAccesses: StorageAccess[];
};

const ExpectedInstallationUrl = 'https://boardoil.dozigden.com/installation/';
const CardButtonName = /#\d+/;
const CreatedCardTitle = 'Created at the top';
const EditedCardTitle = 'Edited in the static demo';
const DraggedCardTitle = 'Card to drag';
const DropTargetTitle = 'Drop target card';

test('built demo remains static, resettable, and safe', async ({ context, page }) => {
  const securityPolicy = await loadDemoSecurityPolicy();
  const forbiddenRequests: string[] = [];
  const failedRequests: string[] = [];
  const consoleErrors: string[] = [];
  const columns = page.getByRole('article', { name: / column$/ });
  const firstColumn = columns.first();
  const secondColumn = columns.nth(1);

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

  await test.step('open the demo and its licences', async () => {
    await page.goto('/');
    const welcomeDialog = page.getByRole('dialog');
    await expect(welcomeDialog.getByRole('heading', { name: 'Welcome to the BoardOil demo' })).toBeVisible();
    await expect(welcomeDialog.getByText('Anything you enter here will disappear')).toBeVisible();

    const licences = welcomeDialog.getByRole('button', { name: 'View third-party licences' });
    await licences.click();
    const licencesDialog = page.getByRole('dialog', { name: 'Third-Party Licences' });
    await expect(licencesDialog.getByText('Montserrat Font', { exact: true })).toBeVisible();
    await licencesDialog.getByRole('button', { name: 'Close licences' }).click();
    await expect(licencesDialog).not.toBeVisible();
    await expect(licences).toBeFocused();

    await welcomeDialog.getByRole('button', { name: 'Explore the demo' }).click();
    await expect(welcomeDialog).not.toBeVisible();
    const header = page.getByRole('banner');
    await expect(header.getByText('BoardOil', { exact: true })).toBeVisible();
    await expect(firstColumn).toBeVisible();
  });

  await test.step('open an attachment preview', async () => {
    const thumbnail = page.getByTestId('card-attachment-thumbnail');
    const card = columns.getByRole('button', { name: CardButtonName }).filter({ has: thumbnail }).first();
    await expect(card.getByTestId('card-attachment-thumbnail')).toBeVisible();
    await card.click();

    const cardDialog = page.getByRole('dialog');
    await cardDialog.getByRole('button', { name: /^Enlarge image:/ }).first().click();
    const imageDialog = page.getByRole('dialog', { name: 'Image preview' });
    await expect(imageDialog.getByRole('img')).toBeVisible();
    await imageDialog.getByRole('button', { name: 'Close', exact: true }).click();
    await cardDialog.getByTitle('Cancel', { exact: true }).click();
  });

  await test.step('create new cards at the top of a column', async () => {
    await createInlineCard(firstColumn, CreatedCardTitle);
    await expect(firstColumn.getByRole('button', { name: CardButtonName }).first()).toContainText(CreatedCardTitle);
  });

  await test.step('edit and move a card in memory', async () => {
    const destination = (await secondColumn.getAttribute('aria-label'))!.replace(/ column$/, '');
    await firstColumn.getByRole('button', { name: CardButtonName }).filter({ hasText: CreatedCardTitle }).click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: /^#\d+ / }).click();
    const titleInput = dialog.getByRole('textbox', { name: 'Card title' });
    await titleInput.fill(EditedCardTitle);
    await titleInput.press('Enter');
    await dialog.getByTitle('Select column').click();
    await dialog
      .getByRole('menu', { name: 'Select column' })
      .getByRole('menuitemradio', { name: destination, exact: true })
      .click();
    await dialog.getByRole('button', { name: 'Save card' }).click();

    await expect(secondColumn.getByRole('button', { name: CardButtonName }).filter({ hasText: EditedCardTitle })).toBeVisible();
  });

  await test.step('reload the original fixture', async () => {
    await page.reload();
    const welcomeDialog = page.getByRole('dialog');
    await expect(welcomeDialog.getByRole('heading', { name: 'Welcome to the BoardOil demo' })).toBeVisible();
    await welcomeDialog.getByRole('button', { name: 'Explore the demo' }).click();
    await expect(firstColumn).toBeVisible();
    await expect(page.getByText(CreatedCardTitle, { exact: true })).toHaveCount(0);
    await expect(page.getByText(EditedCardTitle, { exact: true })).toHaveCount(0);
  });

  await test.step('try the slick effects at desktop and narrow widths', async () => {
    const header = page.getByRole('banner');
    const picker = header.getByRole('group', { name: 'Slick effect', exact: true });
    const canvas = page.locator('canvas.slick-goo-layer');
    await expect(header.getByText('Slick effect', { exact: true })).toBeVisible();
    await expect(picker.getByRole('button', { name: 'Bridges', exact: true })).toHaveAttribute('aria-pressed', 'true');

    const effects = [
      { name: 'None', mode: 'none' },
      { name: 'Liquid', mode: 'liquid' },
      { name: 'Bridges', mode: 'bridges' }
    ];
    for (const effect of effects) {
      const option = picker.getByRole('button', { name: effect.name, exact: true });
      await option.click();
      await expect(option).toHaveAttribute('aria-pressed', 'true');
      await expect(canvas).toHaveAttribute('data-renderer', effect.mode);
    }
    await page.screenshot({ path: test.info().outputPath('demo-slick-effects-desktop.png') });

    const viewport = page.viewportSize()!;
    await page.setViewportSize({ width: 320, height: 720 });
    const controls = header.getByRole('button').or(header.getByRole('link'));
    for (const control of await controls.all()) {
      const bounds = (await control.boundingBox())!;
      expect(bounds.x).toBeGreaterThanOrEqual(0);
      expect(bounds.x + bounds.width).toBeLessThanOrEqual(320);
    }

    const liquid = picker.getByRole('button', { name: 'Liquid', exact: true });
    await liquid.press('Enter');
    await expect(liquid).toHaveAttribute('aria-pressed', 'true');
    await expect(canvas).toHaveAttribute('data-renderer', 'liquid');
    await page.screenshot({ path: test.info().outputPath('demo-slick-effects-mobile.png') });
    await picker.getByRole('button', { name: 'Bridges', exact: true }).click();
    await page.setViewportSize(viewport);
  });

  await test.step('drop a card at the exact indicated position', async () => {
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await createInlineCard(firstColumn, DraggedCardTitle);
    await createInlineCard(firstColumn, DropTargetTitle);
    const cards = firstColumn.getByRole('button', { name: CardButtonName });
    const movedCard = cards.filter({ hasText: DraggedCardTitle });
    const targetCard = cards.filter({ hasText: DropTargetTitle });

    await expect(cards.first()).toContainText(DropTargetTitle);
    await movedCard.dragTo(targetCard, { targetPosition: { x: 20, y: 5 } });

    await expect(cards.nth(0)).toContainText(DraggedCardTitle);
    await expect(cards.nth(1)).toContainText(DropTargetTitle);
  });

  await test.step('select cards without reflowing their titles', async () => {
    const card = firstColumn.locator('.card').filter({ hasText: DropTargetTitle });
    const title = card.locator('.card-title-text');
    const titlePositionBeforeSelection = await title.boundingBox();

    await page.getByTitle('Select cards').click();
    await expect(page.getByRole('checkbox', { name: 'Toggle card selection mode' })).toBeChecked();
    await expect(card).toHaveAttribute('role', 'checkbox');
    await expect(card).toHaveAttribute('aria-checked', 'false');
    await expect(card.locator('.card-selection-indicator')).toHaveCount(0);

    const titlePositionInSelectionMode = await title.boundingBox();
    expect(titlePositionInSelectionMode?.x).toBe(titlePositionBeforeSelection?.x);

    await expect(card).toHaveCSS('box-shadow', 'none');
    await card.hover();
    await expect(card).not.toHaveCSS('box-shadow', 'none');
    await card.click();
    await expect(card).toHaveAttribute('aria-checked', 'true');
  });

  await test.step('exercise demo-only controls', async () => {
    const installationLink = page.getByRole('link', { name: 'Get BoardOil' });
    await expect(installationLink).toHaveAttribute('href', ExpectedInstallationUrl);

    const theme = page.getByRole('group', { name: 'Theme', exact: true });
    const light = theme.getByRole('button', { name: 'Light', exact: true });
    const dark = theme.getByRole('button', { name: 'Dark', exact: true });
    await expect(light).toHaveAttribute('aria-pressed', 'true');
    await dark.click();
    await expect(dark).toHaveAttribute('aria-pressed', 'true');
    await expect(light).toHaveAttribute('aria-pressed', 'false');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark');
    await expect(page.locator('.board-selection-toggle-label')).toHaveCSS('color', 'rgb(202, 184, 239)');
    await expect(page.locator('.board-selection-toggle-count')).toHaveCSS('color', 'rgb(202, 184, 239)');

    await page.getByTitle('Done selecting cards').click();
    await firstColumn.getByRole('button', { name: CardButtonName }).filter({ hasText: DropTargetTitle }).click();
    const cardDialog = page.getByRole('dialog');
    await expect(cardDialog.locator('.fixed-chrome-dialog__surface')).toHaveCSS('color', 'rgb(233, 238, 246)');
    await expect(cardDialog.getByText('Column', { exact: true })).toHaveCSS('color', 'rgb(174, 186, 206)');
    await expect(cardDialog.getByLabel('Card description', { exact: true })).toHaveCSS('color', 'rgb(233, 238, 246)');
    await cardDialog.getByTitle('Cancel', { exact: true }).click();
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

async function createInlineCard(column: Locator, title: string) {
  await column.getByRole('button', { name: 'Add default card' }).click();
  await column.getByPlaceholder('Title').fill(title);
  await column.getByRole('button', { name: 'Save new card' }).click();
  await expect(column.getByRole('button', { name: CardButtonName }).filter({ hasText: title })).toBeVisible();
}

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
  expect(headers).toContain("img-src 'self' data: blob:");
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
