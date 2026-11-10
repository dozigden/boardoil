import type { Page } from '@playwright/test';

export class ClientAccountCreationDialog {
  constructor(private readonly page: Page) {}
  readonly dialog = this.page.getByRole('dialog', { name: 'Create Client Account', exact: true });
  readonly username = this.dialog.getByLabel('Username', { exact: true });
  readonly displayName = this.dialog.getByLabel('Display name', { exact: true });
  readonly email = this.dialog.getByLabel('Email', { exact: true });
  readonly tokenName = this.dialog.getByLabel('Token name', { exact: true });
  readonly scopes = this.dialog.getByRole('group', { name: 'Scopes', exact: true });
  readonly expiry = this.dialog.getByRole('group', { name: 'Expiry', exact: true });
  readonly days = this.dialog.getByRole('spinbutton', { name: 'Expires in days', exact: true });
  readonly nonExpiring = this.dialog.getByRole('checkbox', { name: 'Non-expiring token (not recommended)', exact: true });
  readonly acknowledgement = this.dialog.getByRole('checkbox', { name: 'I understand the risk and still want a non-expiring token.', exact: true });
  readonly save = this.dialog.getByRole('button', { name: 'Create client account', exact: true });
  readonly secret = this.page.getByRole('dialog', { name: 'Copy token now', exact: true });

  async open() {
    await this.page.getByRole('button', { name: 'Create client account', exact: true }).click();
  }

  async fill(userName: string) {
    await this.username.fill(userName);
    await this.displayName.fill('Validation Client');
    await this.email.fill(`${userName}@example.test`);
  }
}
