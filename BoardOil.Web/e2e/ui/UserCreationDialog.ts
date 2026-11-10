import { expect, type Page } from '@playwright/test';

export class UserCreationDialog {
  readonly dialog;
  readonly username;
  readonly displayName;
  readonly email;
  readonly password;
  readonly confirmation;
  readonly role;
  readonly save;

  constructor(private readonly page: Page) {
    this.dialog = page.getByRole('dialog', { name: 'Create User', exact: true });
    this.username = this.dialog.getByLabel('Username', { exact: true });
    this.displayName = this.dialog.getByLabel('Display name', { exact: true });
    this.email = this.dialog.getByLabel('Email', { exact: true });
    this.password = this.dialog.getByLabel('Password', { exact: true });
    this.confirmation = this.dialog.getByLabel('Confirm password', { exact: true });
    this.role = this.dialog.getByRole('combobox', { name: 'Role', exact: true });
    this.save = this.dialog.getByRole('button', { name: 'Create user', exact: true });
  }

  async open() {
    await this.page.getByRole('button', { name: 'Create user', exact: true }).click();
    await expect(this.dialog).toBeVisible();
  }

  async fill(userName: string, password = 'Password1234!') {
    await this.username.fill(userName);
    await this.displayName.fill('Validation Example');
    await this.email.fill(`${userName}@example.test`);
    await this.password.fill(password);
    await this.confirmation.fill(password);
  }

  async cancel() {
    await this.dialog.getByRole('button', { name: 'Cancel creation', exact: true })
      .and(this.dialog.getByTitle('Cancel', { exact: true })).click();
    await expect(this.dialog).toBeHidden();
  }
}
