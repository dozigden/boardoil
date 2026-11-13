import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest';
import { drawLiquid, drawLiquids } from './liquidSurface';
import { cardDistance, Liquid } from './physics';
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

  it('keeps the unassigned-card clearance after smoothing particle density', () => {
    const liquid = pool();
    const neighbour: GooCard = { ...liquid.cards[0], id: 2, group: null, y: 290, height: 60 };
    liquid.otherObstacles = [neighbour];
    liquid.particles = Array.from({ length: 12 }, (_, i) => ({ x: 175 + i * 5, y: 258, vx: 0, vy: 0 }));
    const outline = drawLiquid(context, liquid, viewport, '#95d4aa', false);
    expect(outline.length).toBeGreaterThan(0);
    for (const loop of outline) for (const [x, y] of loop) {
      expect(cardDistance(x, y, neighbour).distance).toBeGreaterThanOrEqual(5.5);
    }
  });

  it('resolves touching outlines independently of paint order and restores them when a rival leaves', () => {
    const a = pool(), b = pool();
    b.cards[0] = { ...b.cards[0], id: 2, group: 2, y: 324 };
    a.particles = [-4, 0, 4].map(dx => ({ x: 200 + dx, y: 262, vx: 0, vy: 0 }));
    b.particles = structuredClone(a.particles);
    const entries = [{ id: 1, liquid: a, colour: '#95d4aa', sleeping: false },
      { id: 2, liquid: b, colour: '#ddbc83', sleeping: false }];
    const outlines = drawLiquids(context, entries, viewport);
    const reversed = drawLiquids(context, [...entries].reverse().map(entry => ({ ...entry, sleeping: true })), viewport);
    expect(reversed.get(a)).toBe(outlines.get(a));
    expect(reversed.get(b)).toBe(outlines.get(b));
    const alone = drawLiquid(context, a, viewport, entries[0].colour, true);
    expect(alone).not.toEqual(outlines.get(a));
    expect(drawLiquid(context, a, viewport, entries[0].colour, false)).toEqual(alone);
  });

  it('reuses awake outlines until simulation or geometry changes, including colour-only redraws', () => {
    const liquid = pool();
    const entry = { id: 1, liquid, colour: '#95d4aa', sleeping: false, revision: 0 };
    const original = drawLiquids(context, [entry], viewport).get(liquid);
    expect(drawLiquids(context, [{ ...entry, colour: '#ddbc83' }], viewport).get(liquid)).toBe(original);
    liquid.steps++;
    const simulated = drawLiquids(context, [entry], viewport).get(liquid);
    expect(simulated).not.toBe(original);
    expect(simulated).toEqual(original);
    liquid.cards[0].x += 50;
    entry.revision++;
    const moved = drawLiquids(context, [entry], viewport).get(liquid);
    expect(moved).not.toEqual(original);
    expect(moved).toEqual(drawLiquid(context, liquid, viewport, entry.colour, false));
  });

  it('refreshes an unchanged awake outline when a rival receives a simulation result', () => {
    const a = pool(), b = pool();
    b.cards[0] = { ...b.cards[0], id: 2, group: 2, y: 324 };
    a.particles = [-4, 0, 4].map(dx => ({ x: 200 + dx, y: 262, vx: 0, vy: 0 }));
    b.particles = structuredClone(a.particles);
    const entries = [{ id: 1, liquid: a, colour: '#95d4aa', sleeping: false, revision: 0 },
      { id: 2, liquid: b, colour: '#ddbc83', sleeping: false, revision: 0 }];
    const original = drawLiquids(context, entries, viewport);
    expect(drawLiquids(context, entries, viewport).get(b)).toBe(original.get(b));
    a.particles.forEach(p => { p.x += 15; });
    a.steps++;
    const moved = drawLiquids(context, entries, viewport);
    expect(moved.get(b)).not.toEqual(original.get(b));
    const rebuilt = drawLiquids(context, entries.map(entry => ({ ...entry, revision: undefined })), viewport);
    expect(moved.get(a)).toEqual(rebuilt.get(a));
    expect(moved.get(b)).toEqual(rebuilt.get(b));
  });

  it('invalidates card samples after geometry, clearance and clipping changes', () => {
    const liquid = pool();
    const obstacle = { ...liquid.cards[0], id: 2, group: null, y: 290, height: 60 };
    liquid.otherObstacles = [obstacle];
    liquid.particles = Array.from({ length: 12 }, (_, i) => ({ x: 175 + i * 5, y: 258, vx: 0, vy: 0 }));
    for (const [dx, top, width] of [[0, 0, 1440], [0, 0, 1440], [.25, 130, 350], [20, 0, 1440]]) {
      liquid.cards[0].x += dx; liquid.cards[0].width += dx;
      obstacle.y += dx; obstacle.radius += dx;
      const view = { ...viewport, top, width };
      const cached = drawLiquid(context, liquid, view, '#95d4aa', false);
      const fresh = pool();
      fresh.cards = structuredClone(liquid.cards);
      fresh.otherObstacles = structuredClone(liquid.otherObstacles);
      fresh.particles = structuredClone(liquid.particles);
      expect(cached).toEqual(drawLiquid(context, fresh, view, '#95d4aa', false));
    }
  });
});
