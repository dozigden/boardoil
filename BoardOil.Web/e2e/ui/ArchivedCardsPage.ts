import { expect, type Locator, type Page } from '@playwright/test';

export class ArchivedCardsPage {
  public constructor(private readonly page: Page) {}

  public row(cardTitle: string) {
    return this.page.getByRole('row').filter({ hasText: cardTitle });
  }

  public async openCard(cardTitle: string) {
    await this.row(cardTitle).click();
    await expect(this.dialog(cardTitle)).toBeVisible();
  }

  public async unarchiveOpenCard(cardTitle: string) {
    const dialog = this.dialog(cardTitle);
    await dialog.getByRole('button', { name: 'Unarchive' }).click();
    await expect(dialog).toBeHidden();
  }

  public async goBackToBoard() {
    await this.page.getByRole('button', { name: 'Back to board' }).click();
    await expect(this.page).toHaveURL(/\/boards\/\d+$/);
  }

  private dialog(cardTitle: string): Locator {
    return this.page.getByRole('dialog').filter({
      has: this.page.getByRole('heading', { name: cardTitle })
    });
  }
}
