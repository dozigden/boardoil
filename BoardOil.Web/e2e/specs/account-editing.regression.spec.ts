import { randomUUID } from 'node:crypto';
import { authenticatePage, expect, test } from '../fixtures/boardOilTest';
import { holdRejectedAccountUpdate } from '../support/accountUpdateResponse';
import { expirePasswordResetSession } from '../support/passwordResetResponse';
import { AccountEditor } from '../ui/AccountEditor';

for (const kind of ['user', 'client'] as const) {
  test(`${kind} account editing retains rejected values and updates all details on retry`, async ({ api, authenticatedPage: page }) => {
    const name = `edit-${kind}-${randomUUID().slice(0, 8)}`;
    const account = kind === 'user' ? await api.createUser(name, 'Password1234!') : await api.createClientAccount(name);
    const other = await api.createUser(`${name}-other`, 'Password1234!');
    if (kind === 'client') await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(kind === 'user' ? '/admin/system/users' : '/admin/system/client-accounts');
    const form = new AccountEditor(page, kind);
    await form.open(account.id);
    await expect(form.username).toBeDisabled();
    await expect(form.username).toHaveValue(name);
    await form.displayName.fill('   ');
    await form.email.fill(`${other.userName}@boardoil.test`);
    await form.role.selectOption('Admin');
    await form.active.uncheck();
    await form.save.click();
    await expect(form.displayName).toHaveAccessibleDescription('Display name is required.');
    await expect(form.displayName).toBeFocused();
    await expect(form.role).toHaveValue('Admin');
    await expect(form.active).not.toBeChecked();
    await form.displayName.fill('  Updated account  ');
    await form.save.click();
    await expect(form.email).toHaveAccessibleDescription('Email already exists.');
    await expect(form.email).toBeFocused();
    await expect(page.getByText('Email already exists.', { exact: true })).toHaveCount(1);
    await page.screenshot({ path: test.info().outputPath(`${kind}-edit-validation.png`) });
    await form.email.fill(`${name}-updated@boardoil.test`);
    await form.save.click();
    await expect(form.dialog).toBeHidden();
    await form.open(account.id);
    await expect(form.displayName).toHaveValue('Updated account');
    await expect(form.role).toHaveValue('Admin');
    await expect(form.active).not.toBeChecked();
    await form.displayName.fill('Discard this');
    await form.cancel();
    await form.open(account.id);
    await expect(form.displayName).toHaveValue('Updated account');
    await expect(form.dialog.getByRole('alert')).toHaveCount(0);
  });
}

test('account edit locks during a request and retains the draft after a general rejection', async ({ api, authenticatedPage: page }) => {
  const account = await api.createUser(`pending-${randomUUID().slice(0, 8)}`, 'Password1234!');
  await page.goto('/admin/system/users');
  const form = new AccountEditor(page, 'user');
  await form.open(account.id);
  await form.displayName.fill('Retained edit');
  const release = await holdRejectedAccountUpdate(page, account.id);
  try {
    await form.save.click();
    await expect(form.displayName).toBeDisabled();
    await expect(form.save).toBeDisabled();
    await page.keyboard.press('Escape');
    await expect(form.dialog).toBeVisible();
  } finally {
    release();
  }
  await expect(form.dialog.getByRole('alert')).toHaveText('Account update is temporarily unavailable.');
  await expect(form.dialog.getByRole('alert')).toBeFocused();
  await expect(page.getByText('Account update is temporarily unavailable.', { exact: true })).toHaveCount(1);
  await expect(form.displayName).toHaveValue('Retained edit');
  await form.save.click();
  await expect(form.dialog).toBeHidden();
});

test('admin password reset keeps confirmation local and displays backend password errors', async ({ api, authenticatedPage: page }) => {
  const name = `reset-${randomUUID().slice(0, 8)}`;
  const account = await api.createUser(name, 'Password1234!');
  await page.goto('/admin/system/users');
  const row = page.getByRole('row').filter({ has: page.getByRole('button', { name: `#${account.id}`, exact: true }) });
  await row.getByRole('button', { name: 'User actions', exact: true }).click();
  await page.getByRole('button', { name: 'Reset password', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Reset User Password', exact: true });
  const password = dialog.getByLabel('New password', { exact: true });
  const confirmation = dialog.getByLabel('Confirm new password', { exact: true });
  const save = dialog.getByRole('button', { name: 'Reset password', exact: true });
  await password.fill('short');
  await save.click();
  await expect(confirmation).toHaveAccessibleDescription('Passwords do not match.');
  await confirmation.fill('short');
  await save.click();
  await expect(password).toHaveAccessibleDescription('Password must be at least 10 characters.');
  await expect(password).toBeFocused();
  await password.fill('ChangedPassword1234!');
  await confirmation.fill('ChangedPassword1234!');
  await save.click();
  await expect(dialog).toBeHidden();
  await authenticatePage(page, name, 'ChangedPassword1234!');
});

test('own password reset retains the session after rejection and signs out after success', async ({ api, page }) => {
  const name = `own-reset-${randomUUID().slice(0, 8)}`;
  await api.createUser(name, 'Password1234!');
  await authenticatePage(page, name, 'Password1234!');
  await page.goto('/user-admin/reset-password');
  const current = page.getByLabel('Current password', { exact: true });
  const password = page.getByLabel('New password', { exact: true });
  const confirmation = page.getByLabel('Confirm new password', { exact: true });
  const save = page.getByRole('button', { name: 'Reset password', exact: true });
  await current.fill('wrong-password');
  await password.fill('ChangedPassword1234!');
  await confirmation.fill('ChangedPassword1234!');
  await save.click();
  await expect(current).toHaveAccessibleDescription('Current password is incorrect.');
  await expect(current).toBeFocused();
  await expect(page).toHaveURL(/\/user-admin\/reset-password$/);
  await expect(password).toHaveValue('ChangedPassword1234!');
  await page.screenshot({ path: test.info().outputPath('password-reset-validation.png') });
  await current.fill('Password1234!');
  await save.click();
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByText('Password reset successfully.', { exact: true })).toBeVisible();
  await expect(page.getByRole('dialog', { name: 'Discard unsaved changes' })).toHaveCount(0);
  await authenticatePage(page, name, 'ChangedPassword1234!');
});


test('expired sessions leave a pending password form without a discard prompt', async ({ api, page }) => {
  const name = `expired-${randomUUID().slice(0, 8)}`;
  await api.createUser(name, 'Password1234!');
  await authenticatePage(page, name, 'Password1234!');
  await page.goto('/user-admin/reset-password');
  await page.getByLabel('Current password', { exact: true }).fill('Password1234!');
  await page.getByLabel('New password', { exact: true }).fill('ChangedPassword1234!');
  await page.getByLabel('Confirm new password', { exact: true }).fill('ChangedPassword1234!');
  await expirePasswordResetSession(page);
  await page.getByRole('button', { name: 'Reset password', exact: true }).click();
  await expect(page).toHaveURL(/\/unauthorized\?redirect=/);
  await expect(page.getByRole('dialog', { name: 'Discard unsaved changes' })).toHaveCount(0);
  await expect(page.getByLabel('Current password', { exact: true })).toHaveCount(0);
});

test('ordinary navigation protects a password draft but logout can leave it', async ({ authenticatedPage: page }) => {
  await page.goto('/user-admin/reset-password');
  const password = page.getByLabel('New password', { exact: true });
  await password.fill('UnsavedPassword1234!');
  await page.getByRole('link', { name: 'Profile', exact: true }).click();
  const discard = page.getByRole('dialog', { name: 'Discard unsaved changes', exact: true });
  await expect(discard).toBeVisible();
  await discard.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(password).toHaveValue('UnsavedPassword1234!');
  await page.getByRole('button', { name: 'User menu', exact: true }).click();
  await page.getByRole('button', { name: 'Logout', exact: true }).click();
  await expect(page).toHaveURL(/\/login$/);
  await expect(discard).toHaveCount(0);
});
