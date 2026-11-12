import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';
import { drawLiquid } from './liquidSurface';
import { Liquid } from './physics';
import type { GooCard, Viewport } from './types';

beforeAll(() => vi.stubGlobal('Path2D', class {
  moveTo() {}
  lineTo() {}
  closePath() {}
}));
afterAll(() => vi.unstubAllGlobals());

const viewport: Viewport = { left: 0, top: 0, width: 1440, height: 840, clipTop: 56, clipBottom: 840 };
const context = { fill() {}, stroke() {} } as unknown as CanvasRenderingContext2D;
function pool() {
  const card: GooCard = { id: 1, group: 1, column: 0, x: 200, y: 200, width: 180, height: 100, radius: 12 };
  return new Liquid([card], [card], { left: 60, right: 340, top: 100, bottom: 300 });
}

describe('bounded Liquid surface', () => {
  it('keeps an unclipped outline unchanged when the viewport moves or grows', () => {
    const liquid = pool();
    const original = drawLiquid(context, liquid, viewport, '#95d4aa', false);
    expect(original.length).toBeGreaterThan(0);
    const shifted = drawLiquid(context, liquid, { ...viewport, left: 42, top: 33, width: 2400, height: 1800 }, '#95d4aa', false);
    expect(shifted).toEqual(original);
  });

  it('retains surfaces crossing either viewport edge and culls completely distant pools', () => {
    const liquid = pool();
    for (const [left, top] of [[190, 0], [0, 190], [-1150, 0], [0, -650]]) {
      expect(drawLiquid(context, liquid, { ...viewport, left, top }, '#95d4aa', false).length).toBeGreaterThan(0);
    }
    expect(drawLiquid(context, liquid, { ...viewport, left: 2000 }, '#95d4aa', false)).toEqual([]);
  });

  it('keeps a visible detached droplet when its cards are offscreen', () => {
    const liquid = pool();
    liquid.cards[0].x = 3000;
    liquid.particles = [{ x: 100, y: 100, vx: 0, vy: 0 }];
    const outline = drawLiquid(context, liquid, viewport, '#95d4aa', false);
    expect(outline).toHaveLength(1);
    for (const [x, y] of outline[0]) {
      expect(Math.abs(x - 100)).toBeLessThan(17);
      expect(Math.abs(y - 100)).toBeLessThan(17);
    }
  });
});
