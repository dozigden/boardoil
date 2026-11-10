import type { Page } from '@playwright/test';

export class AccountEditor {
  constructor(private readonly page: Page, readonly kind: 'user' | 'client') {}

  get dialog() { return this.page.getByRole('dialog', { name: this.kind === 'user' ? 'Edit User' : 'Edit Client Account', exact: true }); }
  get username() { return this.dialog.getByLabel('Username', { exact: true }); }
  get displayName() { return this.dialog.getByLabel('Display name', { exact: true }); }
  get email() { return this.dialog.getByLabel('Email', { exact: true }); }
  get role() { return this.dialog.getByLabel('Role', { exact: true }); }
  get active() { return this.dialog.getByLabel('Active account', { exact: true }); }
  get save() { return this.dialog.getByRole('button', { name: this.kind === 'user' ? 'Save user changes' : 'Save client account changes', exact: true }); }

  async open(id: number) {
    await this.page.getByRole('button', { name: `#${id}`, exact: true }).click();
  }

  async cancel() {
    await this.page.keyboard.press('Escape');
  }
}
