import { expect, type Locator, type Page } from '@playwright/test';

export async function openBoardWithSlickEffect(page: Page, boardId: number, effect: string) {
  await page.addInitScript(value => localStorage.setItem('boardoil:slick-effect', value), effect);
  await page.goto(`/boards/${boardId}`);
}

export async function setSlickEffect(page: Page, effect: string) {
  await page.evaluate(value => {
    const key = 'boardoil:slick-effect';
    localStorage.setItem(key, value);
    window.dispatchEvent(new StorageEvent('storage', { key, newValue: value, storageArea: localStorage }));
  }, effect);
}

export async function expectSlickOutlineAligned(card: Locator) {
  await expect.poll(() => card.evaluate(element => {
    const rect = element.getBoundingClientRect();
    const points = [[rect.left - 6, rect.top + rect.height / 2], [rect.right + 6, rect.top + rect.height / 2]];
    if ((element as HTMLElement).dataset.movingCardId) {
      const canvas = document.querySelector<HTMLCanvasElement>('canvas.slick-goo-layer');
      if (!canvas) return false;
      const bounds = canvas.getBoundingClientRect();
      return points.every(([x, y]) => canvas.getContext('2d')!.getImageData(
        Math.floor((x - bounds.left) * canvas.width / bounds.width),
        Math.floor((y - bounds.top) * canvas.height / bounds.height), 1, 1).data[3] > 0);
    }
    const paths = [...element.closest('.column-content')!.querySelectorAll<SVGPathElement>('.bridge-column-outline path')];
    return points.every(([x, y]) => paths.some(path => {
      const matrix = path.getScreenCTM();
      return matrix && path.isPointInFill(new DOMPoint(x, y).matrixTransform(matrix.inverse()));
    }));
  })).toBe(true);
}
