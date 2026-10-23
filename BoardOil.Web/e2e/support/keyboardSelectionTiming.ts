import type { Page } from '@playwright/test';

export async function simulateDelayedKeyboardSelection(page: Page) {
  await page.addInitScript(() => {
    const originalNow = Date.now.bind(Date);
    let elapsed = 0;
    Date.now = () => originalNow() + elapsed;

    // Run after the editor's key handler, before the browser's selectionchange.
    // Simulate a busy main thread exceeding ProseMirror's 50ms keyboard-origin
    // window without a real sleep or dependence on the runner's CPU speed (#960).
    document.addEventListener('keydown', event => {
      if (event.key === 'ArrowUp') {
        elapsed += 100;
      }
    });
  });
}
