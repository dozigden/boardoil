import type { Page } from '@playwright/test';

export class TokenCreationDialog {
  constructor(private readonly page: Page) {}
  readonly dialog = this.page.getByRole('dialog', { name: 'Create Access Token', exact: true });
  readonly name = this.dialog.getByLabel('Token name', { exact: true });
  readonly scopes = this.dialog.getByRole('group', { name: 'Scopes', exact: true });
  readonly expiry = this.dialog.getByRole('group', { name: 'Expiry', exact: true });
  readonly days = this.dialog.getByRole('spinbutton', { name: 'Expires in days', exact: true });
  readonly nonExpiring = this.dialog.getByRole('checkbox', { name: 'Non-expiring token (not recommended)', exact: true });
  readonly acknowledgement = this.dialog.getByRole('checkbox', { name: 'I understand the risk and still want a non-expiring token.', exact: true });
  readonly save = this.dialog.getByRole('button', { name: 'Create token', exact: true });
  readonly secret = this.page.getByRole('dialog', { name: 'Copy token now', exact: true });

  async open(kind: 'personal' | 'client' = 'personal') {
    const name = kind === 'personal' ? 'Create access token' : 'Create token';
    await this.page.getByRole('button', { name, exact: true }).click();
  }

  async cancel() {
    await this.dialog.getByRole('button', { name: 'Cancel creation', exact: true }).click();
  }
}
