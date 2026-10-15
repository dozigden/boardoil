import { expect, type Page } from '@playwright/test';
export class CustomFieldEditorPage {
  public constructor(private readonly page: Page) {}
  public async open(boardId: number, customFieldId?: number) {
    await this.page.goto(`/boards/${boardId}/admin/custom-fields/${customFieldId ?? 'new'}`);
    await expect(this.page.getByLabel('Display name', { exact: true })).toBeVisible();
  }
  public async fill(name: string, code: string, type: 'text' | 'integer' | 'date', value = '') {
    await this.page.getByLabel('Display name', { exact: true }).fill(name);
    await this.page.getByLabel('Field code', { exact: true }).fill(code);
    await this.page.getByRole('combobox', { name: 'Type', exact: true }).selectOption(type);
    await this.page.getByLabel('Default value', { exact: true }).fill(value);
  }
  public async save(create = false) {
    await this.page.getByRole('button', { name: create ? 'Create field' : 'Save field', exact: true }).click();
    await expect(this.page.getByRole('dialog')).toBeHidden();
  }
}
