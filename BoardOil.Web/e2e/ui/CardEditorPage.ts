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

  public editedCommentEditor() {
    return this.dialog().getByLabel('Edit comment', { exact: true });
  }

  public async editComment(text: string) {
    const comment = this.dialog().getByRole('article').filter({ hasText: text });
    await comment.getByRole('button', { name: 'Comment actions', exact: true }).click();
    await comment.getByRole('button', { name: 'Edit', exact: true }).click();
    await expect(this.editedCommentEditor()).toBeVisible();
  }

  public async saveCommentEdit() {
    await this.editingCommentRegion().getByRole('button', { name: 'Save comment', exact: true }).click();
  }

  public async cancelCommentEdit() {
    await this.editingCommentRegion().getByRole('button', { name: 'Cancel', exact: true }).click();
    await expect(this.editedCommentEditor()).toBeHidden();
  }

  public async editCommentImageAlternativeText(currentAlt: string, nextAlt: string) {
    await this.editedCommentEditor().getByRole('button', { name: `Enlarge image: ${currentAlt}`, exact: true }).click();
    const imageDialog = this.page.getByRole('dialog', { name: 'Image preview', exact: true });
    await imageDialog.getByRole('textbox', { name: 'Alternative text' }).fill(nextAlt);
    await imageDialog.getByRole('button', { name: 'Save alternative text', exact: true }).click();
    await expect(imageDialog).toBeHidden();
  }

  private editingCommentRegion() {
    return this.dialog().getByRole('article').filter({ has: this.page.getByLabel('Edit comment', { exact: true }) });
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

  public async setDescriptionMarkdown(markdown: string) {
    await this.toolbar().getByRole('button', { name: 'Switch to markdown text editor', exact: true }).click();
    await this.dialog().getByRole('textbox', { name: 'Card description markdown', exact: true }).fill(markdown);
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
