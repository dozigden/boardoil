import type { Page } from '@playwright/test';

export class SystemTimeZoneSetting {
  public constructor(private readonly page: Page) {}
  public async saveTimeZone(zone: string) {
    await this.page.getByRole('combobox', { name: 'System timezone', exact: true }).fill(zone);
    await this.page.getByRole('option').filter({ hasText: zone.replace(/_/g, ' ') }).click();
    await this.page.getByRole('button', { name: 'Save', exact: true }).click();
  }
}
