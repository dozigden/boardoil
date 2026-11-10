import { expect, test } from '../fixtures/boardOilTest';
import { rejectNextUserCreation } from '../support/userCreationResponse';
import { UserCreationDialog } from '../ui/UserCreationDialog';

test('keyboard submission blocks cancellation and repeat requests until the response arrives', async ({ authenticatedPage: page }) => {
  await page.goto('/admin/system/users');
  const form = new UserCreationDialog(page);
  await form.open();
  await form.fill('validation-pending');
  let release!: () => void;
  const responseReady = new Promise<void>(resolve => { release = resolve; });
  await rejectNextUserCreation(page, { message: 'Please try again.' }, responseReady);
  let submissions = 0;
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/api/system/users')) submissions++;
  });

  try {
    await form.confirmation.press('Enter');
    await expect(form.save).toBeDisabled();
    await expect(form.username).toBeDisabled();
    await page.keyboard.press('Escape');
    await expect(form.dialog).toBeVisible();
    await form.dialog.getByRole('button', { name: 'Cancel creation', exact: true })
      .and(form.dialog.getByTitle('Cancel creation', { exact: true })).click();
    await page.keyboard.press('Enter');
    await expect(form.dialog).toBeVisible();
    expect(submissions).toBe(1);
  } finally {
    release();
  }

  await expect(form.dialog.getByRole('alert')).toHaveText('Please try again.');
  await expect(form.dialog.getByRole('alert')).toBeFocused();
  await expect(form.save).toBeEnabled();
  await expect(form.username).toHaveValue('validation-pending');
  await page.keyboard.press('Escape');
  await expect(form.dialog).toBeHidden();
  await form.open();
  await expect(form.username).toHaveValue('');
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
});

test('user creation keeps confirmation local and displays backend field validation with correction', async ({ authenticatedPage: page }) => {
  await page.goto('/admin/system/users');
  const form = new UserCreationDialog(page);
  await form.open();
  await form.fill('validation-fields', 'short');
  await form.displayName.fill('   ');
  await form.email.fill('invalid-email');
  await form.confirmation.fill('different');
  let submissions = 0;
  page.on('request', request => {
    if (request.method() === 'POST' && request.url().endsWith('/api/system/users')) {
      submissions++;
    }
  });
  await form.save.click();
  await expect(form.displayName).not.toHaveAttribute('aria-invalid', 'true');
  await expect(form.confirmation).toBeFocused();
  await expect(form.confirmation).toHaveAccessibleDescription('Passwords do not match.');
  expect(submissions).toBe(0);

  await form.password.fill('shorter');
  await expect(form.confirmation).not.toHaveAttribute('aria-invalid', 'true');
  await form.password.fill('short');
  await form.confirmation.fill('short');
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
  await form.save.click();
  const message = 'Password must be at least 10 characters.';
  await expect(form.password).toHaveAccessibleDescription(message);
  await expect(form.password).toHaveAttribute('aria-invalid', 'true');
  await expect(form.displayName).toHaveAccessibleDescription('Display name is required.');
  await expect(form.displayName).toBeFocused();
  await expect(form.email).toHaveAccessibleDescription("Email must contain '@' with characters before and after it.");
  expect(submissions).toBe(1);
  await expect(form.username).toHaveValue('validation-fields');
  await expect(form.password).toHaveValue('short');
  await expect(form.confirmation).toHaveValue('short');
  await expect(page.getByText(message, { exact: true })).toHaveCount(1);
  await page.screenshot({ path: test.info().outputPath('user-password-validation.png') });
  await form.save.click();
  await expect(form.password).toHaveAccessibleDescription(message);

  await form.displayName.fill('Validation Example');
  await expect(form.displayName).not.toHaveAttribute('aria-invalid', 'true');
  await expect(form.email).toHaveAttribute('aria-invalid', 'true');
  await form.email.fill('validation-fields@example.test');
  await form.password.fill('Password1234!');
  await form.confirmation.fill('Password1234!');
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
  await form.save.click();
  await expect(form.dialog).toBeHidden();
  await expect(page.getByText('validation-fields', { exact: true })).toBeVisible();
  await form.open();
  await expect(form.username).toHaveValue('');
  await expect(form.password).toHaveValue('');
  await expect(form.confirmation).toHaveValue('');
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
});

test('duplicate usernames and emails appear beside their fields and a corrected retry succeeds', async ({ api, authenticatedPage: page }) => {
  await api.createUser('validation-existing', 'Password1234!');
  await page.goto('/admin/system/users');
  const form = new UserCreationDialog(page);
  await form.open();
  await form.fill('validation-existing');
  await form.email.fill('validation-existing@boardoil.test');
  await form.save.click();
  await expect(form.username).toHaveAccessibleDescription('Username already exists.');
  await expect(form.username).toBeFocused();
  await expect(form.email).toHaveAccessibleDescription('Email already exists.');
  await expect(page.getByText('Username already exists.', { exact: true })).toHaveCount(1);
  await expect(form.email).toHaveValue('validation-existing@boardoil.test');
  await form.username.fill('validation-renamed');
  await expect(form.username).not.toHaveAttribute('aria-invalid', 'true');
  await expect(form.email).toHaveAttribute('aria-invalid', 'true');
  await form.email.fill('validation-renamed@example.test');
  await form.save.click();
  await expect(form.dialog).toBeHidden();
  await expect(page.getByText('validation-renamed', { exact: true })).toBeVisible();
});

test('user creation retains unmapped errors, replaces results and clears a cancelled session', async ({ authenticatedPage: page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/admin/system/users');
  const form = new UserCreationDialog(page);
  await form.open();
  await form.fill('validation-retained');
  await rejectNextUserCreation(page, {
    message: 'Validation failed.',
    validationErrors: {
      userName: ['Choose a different username.'],
      role: ['That role is not available.'],
      futureField: ['Additional information is required.']
    }
  });
  await form.save.click();
  await expect(form.username).toHaveAccessibleDescription('Choose a different username.');
  await expect(form.username).toBeFocused();
  await expect(form.role).toHaveAccessibleDescription('That role is not available.');
  await expect(form.dialog.getByText('Additional information is required.', { exact: true })).toBeVisible();
  await expect(form.email).toHaveValue('validation-retained@example.test');
  await page.screenshot({ path: test.info().outputPath('user-validation-narrow.png') });

  await form.username.fill('validation-corrected');
  await expect(form.username).not.toHaveAttribute('aria-invalid', 'true');
  await expect(form.role).toHaveAttribute('aria-invalid', 'true');
  await expect(form.username).toBeFocused();
  await rejectNextUserCreation(page, { message: 'User creation is temporarily unavailable.' });
  await form.save.click();
  await expect(form.dialog.getByRole('alert')).toHaveCount(1);
  await expect(form.dialog.getByRole('alert')).toHaveText('User creation is temporarily unavailable.');
  await expect(form.dialog.getByRole('alert')).toBeFocused();
  await expect(form.role).not.toHaveAttribute('aria-invalid', 'true');
  await form.cancel();
  await form.open();
  await expect(form.dialog.getByRole('alert')).toHaveCount(0);
  await expect(form.username).toHaveValue('');
  await expect(form.password).toHaveValue('');
});
