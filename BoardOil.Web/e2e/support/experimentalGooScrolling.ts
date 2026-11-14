import type { Locator } from '@playwright/test';

export async function reportHorizontalOverscroll(board: Locator, direction: 'left' | 'right') {
  return await board.evaluate(async (element, edge) => {
    const width = element.scrollWidth;
    const position = edge === 'left' ? -element.clientWidth : width + element.clientWidth;
    // Deliver Safari's out-of-range offset to the real renderer; native wheel bounce requires a Mac.
    Object.defineProperty(element, 'scrollLeft', { configurable: true, get: () => position });
    try {
      element.dispatchEvent(new Event('scroll'));
      await new Promise<void>(resolve => requestAnimationFrame(() => requestAnimationFrame(() => resolve())));
      return { before: width, after: element.scrollWidth };
    } finally {
      delete (element as Partial<HTMLElement>).scrollLeft;
      element.dispatchEvent(new Event('scroll'));
    }
  }, direction);
}
