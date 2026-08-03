import { expect, type Locator, type Page } from '@playwright/test';

export class BoardPage {
  public constructor(private readonly page: Page) {}

  public async open(boardId: number) {
    await this.page.goto(`/boards/${boardId}`);
    await expect(this.column('Todo')).toBeVisible();
  }

  public column(title: string) {
    return this.page.getByRole('article', { name: `${title} column` });
  }

  public card(columnTitle: string, cardTitle: string) {
    return this.column(columnTitle).getByRole('button').filter({ hasText: cardTitle });
  }

  public async bringColumnIntoView(columnTitle: string) {
    const column = this.column(columnTitle);
    await column.scrollIntoViewIfNeeded();
    await expect(column).toBeInViewport();
  }

  public async openCardFilters() {
    await this.page.getByRole('button', { name: 'Card filters', exact: true }).click();
    await expect(this.page.getByRole('region', { name: 'Card filter matrix' })).toBeVisible();
  }

  public async setTagFilterWithKeyboard(tagName: string, state: 'include' | 'exclude') {
    const actionName = state === 'include'
      ? `Move to include ${tagName}`
      : `Move to exclude ${tagName}`;
    const action = this.page
      .getByRole('region', { name: 'Tag filter matrix' })
      .getByRole('button', { name: actionName });

    await action.focus();
    await this.page.keyboard.press('Enter');
  }

  public async clearCardFilters() {
    await this.page.getByRole('button', { name: 'Clear card filters' }).click();
    await expect(this.page.getByRole('region', { name: 'Card filter matrix' })).toBeHidden();
  }

  public async createCard(columnTitle: string, cardTitle: string) {
    const column = this.column(columnTitle);
    await column.getByRole('button', { name: 'Add default card' }).click();
    await column.getByPlaceholder('Title').fill(cardTitle);
    await column.getByRole('button', { name: 'Save new card' }).click();
  }

  public async enterCardSelectionMode() {
    const selectionToggle = this.page.getByRole('checkbox', { name: 'Toggle card selection mode' });
    await this.page.getByTitle('Select cards').click();
    await expect(selectionToggle).toBeChecked();
  }

  public async selectCard(columnTitle: string, cardTitle: string) {
    const card = this.column(columnTitle).getByRole('checkbox').filter({ hasText: cardTitle });
    await card.click();
    await expect(card).toHaveAttribute('aria-checked', 'true');
  }

  public async archiveSelectedCards(selectedCount: number) {
    await this.page.getByRole('button', { name: `Archive ${selectedCount} selected cards` }).click();
    const dialog = this.page.getByRole('dialog').filter({
      has: this.page.getByRole('heading', { name: 'Archive Selected Cards' })
    });
    await expect(dialog).toBeVisible();
    await dialog.getByRole('button', { name: 'Archive selected' }).click();
    await expect(dialog).toBeHidden();
  }

  public async openArchivedCards() {
    await this.page.getByRole('button', { name: 'View archived cards' }).click();
    await expect(this.page).toHaveURL(/\/boards\/\d+\/archived$/);
  }

  public async openCard(columnTitle: string, cardTitle: string) {
    await this.card(columnTitle, cardTitle).click();
    await expect(this.dialog()).toBeVisible();
  }

  public async renameOpenCard(title: string) {
    const dialog = this.dialog();
    await dialog.getByRole('button', { name: /^#\d+ / }).click();
    const titleInput = dialog.getByRole('textbox', { name: 'Card title' });
    await titleInput.fill(title);
    await titleInput.press('Enter');
  }

  public async moveOpenCardTo(columnTitle: string) {
    const dialog = this.dialog();
    await dialog.getByTitle('Select column').click();
    await dialog
      .getByRole('menu', { name: 'Select column' })
      .getByRole('button', { name: columnTitle, exact: true })
      .click();
  }

  public async saveOpenCard() {
    await this.dialog().getByRole('button', { name: 'Save card' }).click();
    await expect(this.dialog()).toBeHidden();
  }

  private dialog(): Locator {
    return this.page.getByRole('dialog');
  }
}
