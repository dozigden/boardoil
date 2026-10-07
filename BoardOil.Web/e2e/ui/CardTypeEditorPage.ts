import { expect, type Page } from '@playwright/test';

export class CardTypeEditorPage {
  public constructor(private readonly page: Page) {}

  public async open(boardId: number, cardTypeId?: number) {
    await this.page.goto(`/boards/${boardId}/admin/card-types/${cardTypeId ?? 'new'}`);
    await expect(this.page.getByRole('textbox', { name: 'Name', exact: true })).toBeVisible();
    const openFields = this.page.getByRole('button', { name: 'Custom Fields >', exact: true });
    if (await openFields.isVisible()) await openFields.click();
    await expect(this.page.getByRole('region', { name: 'Custom Fields', exact: true })).toBeVisible();
  }

  public async addField(name: string, code: string, type: 'text' | 'integer' | 'date', defaultValue = '') {
    await this.page.getByRole('button', { name: 'Add field', exact: true }).click();
    await this.page.getByRole('region', { name: 'New field Text · Optional', exact: true }).getByLabel('Display name', { exact: true }).fill(name);
    const field = this.page.getByRole('region', { name: new RegExp(`${name} (Text|Number|Date)`) });
    await field.getByLabel('Field code', { exact: true }).fill(code);
    await field.getByRole('combobox', { name: 'Type', exact: true }).selectOption(type);
    await field.getByLabel('Default value', { exact: true }).fill(defaultValue);
    await expect(this.page.getByLabel('Display name', { exact: true })).toHaveCount(1);
  }

  public async duplicate(boardId: number, name: string) {
    await this.page.goto(`/boards/${boardId}/admin/card-types`);
    const row = this.page.getByRole('article').filter({
      has: this.page.getByRole('button', { name: `Edit card type ${name}`, exact: true })
    });
    await row.getByRole('button', { name: 'Card type actions', exact: true }).click();
    await this.page.getByRole('button', { name: 'Duplicate', exact: true }).click();
    await expect(this.page.getByRole('dialog', { name: `Duplicate Card Type: ${name}`, exact: true })).toBeVisible();
  }

  public async expandField(name: string) {
    const button = this.page.getByRole('button', { name: new RegExp(`${name} (Text|Number|Date)`) });
    if (await button.getAttribute('aria-expanded') !== 'true') await button.click();
  }

  public async removeField(name: string) {
    await this.page.getByRole('button', { name: `Remove ${name}`, exact: true }).click();
  }

  public async save(create = false) {
    await this.submit(create);
    await expect(this.page.getByRole('dialog')).toBeHidden();
  }

  public async submit(create = false) {
    await this.page.getByRole('button', { name: create ? 'Create card type' : 'Save card type', exact: true }).click();
  }

  public async cancel() {
    await this.page.getByRole('button', { name: 'Cancel editing', exact: true }).filter({ hasText: 'Cancel' }).click();
    await expect(this.page.getByRole('dialog')).toBeHidden();
  }
}
