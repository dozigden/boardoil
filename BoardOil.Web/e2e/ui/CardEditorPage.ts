import { expect, type Locator, type Page } from '@playwright/test';

export class CardEditorPage {
  public constructor(private readonly page: Page) {}

  public descriptionEditor() {
    return this.dialog().getByLabel('Card description', { exact: true });
  }

  public commentEditor() {
    return this.dialog().getByLabel('Comment', { exact: true });
  }

  public commentContent() {
    return this.dialog().getByLabel('Comment content', { exact: true });
  }

  public async formatAllText(editor: Locator, actionName: 'Bold' | 'Italic') {
    await editor.selectText();
    await expect(editor).toBeFocused();
    await this.toolbar().getByRole('button', { name: actionName, exact: true }).click();
  }

  public async addComment() {
    await this.dialog()
      .getByRole('region', { name: 'Card comments' })
      .getByRole('button', { name: 'Add', exact: true })
      .click();
    await expect(this.commentEditor()).toHaveText('');
  }

  public async saveCard() {
    await this.dialog().getByRole('button', { name: 'Save card' }).click();
    await expect(this.dialog()).toBeHidden();
  }

  public async openSlickPicker() {
    await this.dialog().getByTitle('Select slick', { exact: true }).click();
  }

  public slickOption(name: string) {
    return this.dialog().getByRole('menu', { name: 'Select slick' }).getByRole('button', { name, exact: true });
  }

  public async createSlick(name: string) {
    await this.openSlickPicker();
    await this.dialog().getByRole('textbox', { name: 'Search slicks' }).fill(name);
    await this.slickOption(`Create and use "${name}"`).click();
  }

  private toolbar() {
    return this.dialog().getByRole('toolbar', { name: 'Markdown formatting' });
  }

  private dialog() {
    return this.page.getByRole('dialog');
  }
}
