import { expect, type Page } from '@playwright/test';

export class ColumnsManagerPage {
  public constructor(private readonly page: Page) {}

  public async open(boardId: number) {
    await this.page.goto(`/boards/${boardId}/admin/columns`);
    await expect(this.page.getByRole('heading', { name: 'Columns', exact: true })).toBeVisible();
  }

  public async expectOrder(titles: string[]) {
    await expect(this.page.getByRole('button', { name: /^Edit column / })).toHaveText(titles);
  }

  public async dragColumnOnto(title: string, targetTitle: string) {
    const response = this.page.waitForResponse(result =>
      result.request().method() === 'PUT' && /\/columns\/order$/.test(new URL(result.url()).pathname));
    await this.page.getByRole('article', { name: `${title} column`, exact: true })
      .getByRole('img', { name: 'Drag to reorder column', exact: true })
      .dragTo(this.page.getByRole('article', { name: `${targetTitle} column`, exact: true }));
    expect((await response).ok()).toBe(true);
  }
}
