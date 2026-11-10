import type { Page } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';

const endpoint = '/api/system/configuration';
const route = '/admin/system/configuration';
const urlError = 'mcpPublicBaseUrl must be an absolute URL.';

function fields(page: Page) {
  return {
    url: page.getByRole('textbox', { name: 'MCP public base URL', exact: true }),
    logging: page.getByRole('checkbox', { name: 'Log OAuth requests', exact: true }),
    timezone: page.getByRole('combobox', { name: 'System timezone', exact: true }),
    save: page.getByRole('button', { name: 'Save', exact: true }),
    clear: page.getByRole('button', { name: 'Clear', exact: true })
  };
}

async function rejectNextSave(page: Page, error: { message: string; validationErrors?: Record<string, string[]> }, ready = Promise.resolve()) {
  await page.route(`**${endpoint}`, async intercepted => {
    if (intercepted.request().method() !== 'PUT') return intercepted.continue();
    await ready;
    await intercepted.fulfill({ status: 400, json: { success: false, ...error } });
    await page.unroute(`**${endpoint}`);
  });
}

test('configuration retains rejected drafts, adopts saved values and protects unsaved changes', async ({ api, authenticatedPage: page }) => {
  const original = await api.getConfiguration();
  try {
    await api.updateConfiguration({ ...original, mcpPublicBaseUrl: null, systemTimeZoneId: 'UTC' });
    await page.goto(route);
    const form = fields(page);
    await expect(form.save).toBeDisabled();
    await expect(page.getByRole('heading', { name: 'Runtime information' })).toBeVisible();
    await form.url.fill('relative/path');
    await form.logging.setChecked(!original.oauthLifecycleDiagnosticsEnabled);
    await form.save.click();
    await expect(form.url).toHaveAccessibleDescription(new RegExp(urlError));
    await expect(form.url).toBeFocused();
    await expect(form.url).toHaveValue('relative/path');
    await expect(form.logging).toBeChecked({ checked: !original.oauthLifecycleDiagnosticsEnabled });
    await expect(page.getByText(urlError, { exact: true })).toHaveCount(1);
    await page.screenshot({ path: test.info().outputPath('configuration-field-error.png') });
    const preventsUnload = () => page.evaluate(() => !window.dispatchEvent(new Event('beforeunload', { cancelable: true })));
    expect(await preventsUnload()).toBe(true);
    await page.getByRole('link', { name: 'Users', exact: true }).click();
    const discard = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
    await expect(discard).toBeVisible();
    await discard.getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(form.url).toHaveValue('relative/path');

    await form.url.fill('  https://forms.example.test/  ');
    await expect(form.url).not.toHaveAttribute('aria-invalid', 'true');
    await form.timezone.fill('Pacific/Honolulu');
    await form.timezone.press('ArrowDown');
    await form.timezone.press('Enter');
    await expect(form.timezone).toHaveValue(/Pacific\/Honolulu/);
    await form.save.click();
    await expect(form.url).toHaveValue('https://forms.example.test');
    await expect(form.save).toBeDisabled();
    await expect(page.getByRole('status').filter({ hasText: 'Saved successfully.' })).toBeVisible();
    expect(await preventsUnload()).toBe(false);
    const saved = await api.getConfiguration();
    expect(saved.mcpPublicBaseUrl).toBe('https://forms.example.test');
    expect(saved.oauthLifecycleDiagnosticsEnabled).toBe(!original.oauthLifecycleDiagnosticsEnabled);
    expect(saved.systemTimeZoneId).toBe('Pacific/Honolulu');
    await page.screenshot({ path: test.info().outputPath('configuration-saved.png') });

    await form.clear.click();
    await expect(form.url).toHaveValue('');
    await expect(form.save).toBeEnabled();
    await form.save.click();
    await expect(form.save).toBeDisabled();
    expect((await api.getConfiguration()).mcpPublicBaseUrl).toBeNull();
    await form.url.fill('https://discard.example.test');
    await page.getByRole('link', { name: 'Users', exact: true }).click();
    await discard.getByRole('button', { name: 'Discard', exact: true }).click();
    await expect(page).toHaveURL('/admin/system/users');
    await page.getByRole('link', { name: 'Configuration', exact: true }).click();
    await expect(form.url).toHaveValue('');
    await expect(form.save).toBeDisabled();
  } finally {
    await api.updateConfiguration(original);
  }
});

test('timezone errors reach the combobox and unmapped errors use a toast', async ({ api, authenticatedPage: page }) => {
  const original = await api.getConfiguration();
  try {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(route);
    const form = fields(page);
    await form.url.fill('https://retry.example.test/');
    await rejectNextSave(page, { message: 'Validation failed.', validationErrors: {
      systemTimeZoneId: ['Choose another timezone.'], futureSetting: ['That combination is unavailable.']
    } });
    await form.save.click();
    await expect(form.timezone).toHaveAttribute('aria-invalid', 'true');
    await expect(form.timezone).toHaveAccessibleDescription(/Choose another timezone\./);
    await expect(form.timezone).toBeFocused();
    await expect(page.getByRole('alert').filter({ hasText: 'That combination is unavailable.' })).toBeVisible();
    await expect(page.getByText('That combination is unavailable.', { exact: true })).toHaveCount(1);
    await form.timezone.press('Escape');
    await page.getByText('Choose another timezone.', { exact: true }).scrollIntoViewIfNeeded();
    await page.screenshot({ path: test.info().outputPath('configuration-timezone-narrow.png') });
    await form.timezone.fill('Europe/London');
    await form.timezone.press('Enter');
    await expect(form.timezone).not.toHaveAttribute('aria-invalid', 'true');
    await form.save.click();
    await expect(form.url).toHaveValue('https://retry.example.test');
    await expect(form.save).toBeDisabled();
    await page.getByRole('button', { name: 'Toggle section navigation', exact: true }).click();
    await page.getByRole('link', { name: 'Users', exact: true }).click();
    await expect(page).toHaveURL('/admin/system/users');
    await expect(page.getByRole('dialog', { name: 'Discard unsaved changes' })).toHaveCount(0);
  } finally {
    await api.updateConfiguration(original);
  }
});

test('pending configuration saves block editing, navigation and repeat submissions, retaining failure for retry', async ({ authenticatedPage: page }) => {
  await page.goto(route);
  const form = fields(page);
  await form.url.fill('https://pending.example.test');
  let release!: () => void;
  const ready = new Promise<void>(resolve => { release = resolve; });
  await rejectNextSave(page, { message: 'Configuration could not be saved.' }, ready);
  let writes = 0;
  page.on('request', request => { if (request.method() === 'PUT' && request.url().endsWith(endpoint)) writes++; });
  try {
    await form.url.press('Enter');
    await expect(page.getByRole('button', { name: 'Saving...', exact: true })).toBeDisabled();
    await expect(form.url).toBeDisabled();
    await expect(form.logging).toBeDisabled();
    await expect(form.timezone).toBeDisabled();
    await expect(form.clear).toBeDisabled();
    await page.getByRole('link', { name: 'Users', exact: true }).click();
    await expect(page).toHaveURL(route);
    await page.keyboard.press('Enter');
    expect(writes).toBe(1);
  } finally {
    release();
  }
  await expect(page.getByRole('alert').filter({ hasText: 'Configuration could not be saved.' })).toBeVisible();
  await expect(page.getByText('Configuration could not be saved.', { exact: true })).toHaveCount(1);
  await expect(form.url).toHaveValue('https://pending.example.test');
  await expect(form.save).toBeEnabled();
});

test('failed timezone loading is separate from submission feedback and can be retried', async ({ authenticatedPage: page }) => {
  await page.route('**/api/system/timezone/options', intercepted => intercepted.fulfill({
    status: 503, json: { success: false, message: 'Choices unavailable.' }
  }), { times: 1 });
  await page.goto(route);
  await expect(page.getByRole('alert')).toHaveText('Could not load timezone choices: Choices unavailable.');
  await expect(page.getByRole('button', { name: 'Save', exact: true })).toHaveCount(0);
  await page.getByRole('button', { name: 'Retry', exact: true }).click();
  await expect(fields(page).timezone).toBeEnabled();
  await expect(fields(page).save).toBeDisabled();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
