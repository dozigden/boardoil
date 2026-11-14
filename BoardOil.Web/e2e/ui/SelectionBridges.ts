import type { Locator, Page } from '@playwright/test';

export class SelectionBridges {
  readonly canvas: Locator;

  constructor(page: Page) {
    this.canvas = page.locator('canvas.selection-bridge-layer');
  }

  async selectedIds(): Promise<number[]> {
    return JSON.parse((await this.canvas.getAttribute('data-selected-card-ids')) ?? '[]');
  }

  async paintsBetween(left: Locator, right: Locator) {
    const a = (await left.boundingBox())!, b = (await right.boundingBox())!;
    return this.paintsAt((a.x + a.width + b.x) / 2, (Math.max(a.y, b.y) + Math.min(a.y + a.height, b.y + b.height)) / 2);
  }

  async paintsAround(card: Locator) {
    return (await this.colourAround(card))[3] > 0;
  }

  async colourAround(card: Locator) {
    const rect = (await card.boundingBox())!;
    return this.colourAt(rect.x - 4, rect.y + rect.height / 2);
  }

  async paintsInHeader(column: Locator) {
    const content = (await column.locator('.column-content').boundingBox())!;
    return this.paintsAt(content.x + content.width / 2, content.y - 12);
  }

  async paintsBelowHeader(column: Locator, card: Locator) {
    const content = (await column.locator('.column-content').boundingBox())!;
    const rect = (await card.boundingBox())!;
    return this.paintsAt(rect.x - 4, content.y + 4);
  }

  private async paintsAt(x: number, y: number) {
    return (await this.colourAt(x, y))[3] > 0;
  }

  private async colourAt(x: number, y: number) {
    return this.canvas.evaluate((element: HTMLCanvasElement, point) => {
      const rect = element.getBoundingClientRect();
      const px = Math.floor((point.x - rect.left) * element.width / rect.width);
      const py = Math.floor((point.y - rect.top) * element.height / rect.height);
      return Array.from(element.getContext('2d')!.getImageData(px, py, 1, 1).data);
    }, { x, y });
  }
}
