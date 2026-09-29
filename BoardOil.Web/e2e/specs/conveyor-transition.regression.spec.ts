import type { Page } from '@playwright/test';
import { expect, test } from '../fixtures/boardOilTest';
import { BoardPage } from '../ui/BoardPage';
import { ArchivedCardsPage } from '../ui/ArchivedCardsPage';

for (const width of [1280, 390]) {
  test(`filter bar shifts locally and its arrows slide when changing pages at ${width}px`, async ({ api, authenticatedPage: page }) => {
    await page.setViewportSize({ width, height: 800 });
    const board = await api.createBoard('Conveyor transition');
    const boardPage = new BoardPage(page);
    const archivePage = new ArchivedCardsPage(page);
    await boardPage.open(board.id);
    const controls = page.getByRole('region', { name: 'Card controls' });
    const originalBar = await controls.elementHandle();
    const boardPosition = await controls.boundingBox();
    expect(boardPosition).not.toBeNull();
    const transitions = await recordTransforms(page);

    await test.step('move to archive with the same bar and a left arrow', async () => {
      await boardPage.openArchivedCards();
      await expect(page.getByRole('button', { name: 'View archived cards' })).toHaveCount(0);
      await expect.poll(() => transitions.some(item => item.label === 'Card controls')).toBe(true);

      const movement = transitions.find(item => item.label === 'Card controls')!;
      expect(movement.fromX).toBeLessThan(0);
      expect(Math.abs(movement.fromX)).toBeLessThan(boardPosition!.width / 2);
      expect(transitions).toContainEqual(expect.objectContaining({ label: 'Back to board', fromX: expect.any(Number) }));
      expect(transitions.find(item => item.label === 'Back to board')!.fromX).toBeLessThan(0);
      expect(await originalBar!.evaluate(element => element.isConnected)).toBe(true);
      await expect(controls).toHaveCount(1);
      const archivePosition = await controls.boundingBox();
      expect(archivePosition!.x).toBeGreaterThan(boardPosition!.x);
      expect(Math.abs(archivePosition!.width - boardPosition!.width)).toBeLessThan(Math.abs(movement.fromX));
    });

    await test.step('shift back and bring the right arrow into view', async () => {
      transitions.length = 0;
      await archivePage.goBackToBoard();
      await expect(page.getByRole('button', { name: 'Back to board' })).toHaveCount(0);
      await expect.poll(() => transitions.some(item => item.label === 'Card controls')).toBe(true);

      const movement = transitions.find(item => item.label === 'Card controls')!;
      expect(movement.fromX).toBeGreaterThan(0);
      expect(Math.abs(movement.fromX)).toBeLessThan(boardPosition!.width / 2);
      expect(transitions.find(item => item.label === 'View archived cards')!.fromX).toBeGreaterThan(0);
      expect(await originalBar!.evaluate(element => element.isConnected)).toBe(true);
      expect((await controls.boundingBox())!.x).toBeCloseTo(boardPosition!.x, 0);
      await expect(page.getByRole('searchbox')).toHaveCount(1);
    });
  });
}

type TransformRecord = { label: string; fromX: number };

async function recordTransforms(page: Page) {
  const records: TransformRecord[] = [];
  await page.exposeFunction('recordConveyorTransform', (record: TransformRecord) => records.push(record));
  await page.evaluate(() => {
    document.addEventListener('transitionrun', event => {
      if (event.propertyName !== 'transform' || !(event.target instanceof HTMLElement)) return;
      const label = event.target.getAttribute('aria-label');
      if (!label || !['Card controls', 'Back to board', 'View archived cards'].includes(label)) return;
      const record = {
        label,
        fromX: new DOMMatrixReadOnly(getComputedStyle(event.target).transform).m41
      };
      void (window as unknown as { recordConveyorTransform: (value: TransformRecord) => Promise<void> })
        .recordConveyorTransform(record);
    });
  });
  return records;
}
