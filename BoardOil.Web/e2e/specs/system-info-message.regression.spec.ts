import { expect, test } from '../fixtures/boardOilTest';
import { rejectNextSystemInfoMessageSave } from '../support/systemInfoMessageSubmission';

test('saving and disabling a system message updates the header without opening a board', async ({ api, authenticatedPage: page }) => {
  const original = await api.getSystemInfoMessage();
  try {
    await api.updateSystemInfoMessage(null);
    await page.goto('/admin/system/system-info-message');
    await expect(page.getByRole('heading', { name: 'System Info Message', exact: true })).toBeVisible();
    const enabled = page.getByRole('checkbox', { name: 'Enabled', exact: true });
    const save = page.getByRole('button', { name: 'Save', exact: true });
    const title = page.getByRole('textbox', { name: 'Title', exact: true });
    const style = page.getByRole('combobox', { name: 'Style', exact: true });
    await expect(save).toBeDisabled();
    await expect(style.getByRole('option')).toHaveText(['Auto', 'Presets', 'Solid']);
    await enabled.check();
    await title.fill('  Scheduled maintenance  ');
    await page.getByRole('textbox', { name: 'Description', exact: true }).fill('Service resumes shortly.');
    await page.getByRole('button', { name: 'Preset 8', exact: true }).click();
    await expect(page.getByRole('group', { name: 'Header preview', exact: true })).toContainText('Scheduled maintenance');
    await save.click();
    const headerMessage = page.getByRole('button', { name: 'Scheduled maintenance', exact: true });
    await expect(headerMessage).toBeVisible();
    await expect(title).toHaveValue('Scheduled maintenance');
    await expect(save).toBeDisabled();
    await page.reload();
    await expect(page.getByRole('button', { name: 'Preset 8', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await enabled.uncheck();
    await save.click();
    await expect(headerMessage).toHaveCount(0);
    await expect(save).toBeDisabled();
  } finally {
    await api.updateSystemInfoMessage(original);
  }
});

test('system message field errors retain the draft and dependent corrections clear them', async ({ api, authenticatedPage: page }) => {
  const original = await api.getSystemInfoMessage();
  try {
    await api.updateSystemInfoMessage(null);
    await page.goto('/admin/system/system-info-message');
    const title = page.getByRole('textbox', { name: 'Title', exact: true });
    const enabled = page.getByRole('checkbox', { name: 'Enabled', exact: true });
    const description = page.getByRole('textbox', { name: 'Description', exact: true });
    const save = page.getByRole('button', { name: 'Save', exact: true });
    const style = page.getByRole('group', { name: 'Style', exact: true });
    await enabled.check();
    await description.fill('Retain these details.');
    await style.getByRole('combobox', { name: 'Style', exact: true }).selectOption('solid');
    await style.getByLabel('Background Color', { exact: true }).fill('#123456');
    await style.getByRole('combobox', { name: 'Text Color Mode', exact: true }).selectOption('custom');
    await style.getByLabel('Text Color', { exact: true }).fill('#abcdef');
    await style.getByRole('combobox', { name: 'Border', exact: true }).selectOption('none');
    await save.click();
    await expect(title).toHaveAccessibleDescription('Title is required when the message is enabled.');
    await expect(title).toBeFocused();
    await expect(page.getByText('Title is required when the message is enabled.', { exact: true })).toHaveCount(1);
    await expect(description).toHaveValue('Retain these details.');
    await expect(style.getByLabel('Background Color', { exact: true })).toHaveValue('#123456');
    await enabled.uncheck();
    await expect(title).not.toHaveAttribute('aria-invalid', 'true');
    await title.fill('Recovery');
    await enabled.check();

    await rejectNextSystemInfoMessageSave(page, { message: 'Validation failed.', validationErrors: {
      'systemInfoMessage.stylePropertiesJson': ['Style rejected.']
    } });
    await save.click();
    await expect(style).toHaveAccessibleDescription('Style rejected.');
    await expect(style).toBeFocused();
    await expect(page.getByText('Style rejected.', { exact: true })).toHaveCount(1);
    await style.getByLabel('Background Color', { exact: true }).fill('#654321');
    await expect(style).not.toHaveAttribute('aria-invalid', 'true');

    await rejectNextSystemInfoMessageSave(page, { message: 'Saving is temporarily unavailable.' });
    await save.click();
    await expect(page.getByRole('alert').filter({ hasText: 'Saving is temporarily unavailable.' })).toBeVisible();
    await expect(page.getByText('Saving is temporarily unavailable.', { exact: true })).toHaveCount(1);
    await expect(description).toHaveValue('Retain these details.');
    await save.click();
    await expect(save).toBeDisabled();
    await page.reload();
    await expect(style.getByLabel('Background Color', { exact: true })).toHaveValue('#654321');
    await expect(style.getByLabel('Text Color', { exact: true })).toHaveValue('#abcdef');
    await expect(style.getByRole('combobox', { name: 'Border', exact: true })).toHaveValue('none');
    for (const width of [1280, 390]) {
      await page.setViewportSize({ width, height: 1000 });
      await page.screenshot({ path: test.info().outputPath(`system-info-message-${width}.png`), fullPage: true });
    }
  } finally {
    await api.updateSystemInfoMessage(original);
  }
});

test('system message page protects unsaved changes and discards them on request', async ({ api, authenticatedPage: page }) => {
  const original = await api.getSystemInfoMessage();
  try {
    await api.updateSystemInfoMessage(null);
    await page.goto('/admin/system/system-info-message');
    const title = page.getByRole('textbox', { name: 'Title', exact: true });
    await title.fill('Unsaved message');
    await page.getByRole('link', { name: 'Users', exact: true }).click();
    const discard = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
    await expect(discard).toBeVisible();
    await discard.getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(title).toHaveValue('Unsaved message');
    await page.getByRole('link', { name: 'Users', exact: true }).click();
    await discard.getByRole('button', { name: 'Discard', exact: true }).click();
    await expect(page).toHaveURL('/admin/system/users');
    expect(await api.getSystemInfoMessage()).toBeNull();
  } finally {
    await api.updateSystemInfoMessage(original);
  }
});
