import { expect, type Page } from '@playwright/test';

export class CardTypeEditorPage {
  public constructor(private readonly page: Page) {}

  public async open(boardId: number, cardTypeId?: number) {
    await this.page.goto(`/boards/${boardId}/admin/card-types/${cardTypeId ?? 'new'}`);
    await expect(this.page.getByRole('textbox', { name: 'Name', exact: true })).toBeVisible();
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
