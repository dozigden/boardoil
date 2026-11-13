import { describe, expect, it } from 'vitest';
import { GooScene } from './scene';
import { Liquid, liquidSettings, SUPPORT } from './physics';
import { blendedOutline } from './surface';
import type { GooCard, Viewport } from './types';

const viewport: Viewport = { left: 0, top: 0, width: 960, height: 600, clipTop: 60, clipBottom: 600 };
const appearance = { id: 1, name: 'One slick', colour: '#95d4aa' };
const card = (id: number, x = 200, y = 200): GooCard => ({ id, x, y, width: 244, height: 100, radius: 12, column: 0, group: 1 });

describe('experimental board goo', () => {
  it('keeps the existing pool through scrolling, resizing and cross-column movement', () => {
    const scene = new GooScene();
    scene.update([card(1), card(2, 500)], [appearance], viewport);
    scene.step(.02);
    const before = scene.diagnostics;
    scene.update([{ ...card(1, 220, 180), column: 1, height: 130 }, card(2, 500, 180)], [appearance], viewport);
    expect(scene.diagnostics).toEqual(before);
    scene.step(.02);
    expect(scene.diagnostics[0].steps).toBeGreaterThan(before[0].steps);
  });
  it('removes departed slicks and creates pools for changed membership', () => {
    const scene = new GooScene();
    scene.update([card(1)], [appearance], viewport);
    scene.update([{ ...card(1), group: 2 }], [{ ...appearance, id: 2 }], viewport);
    expect(scene.diagnostics.map(s => s.id)).toEqual([2]);
    scene.update([], [], viewport);
    expect(scene.diagnostics).toEqual([]);
    expect(scene.awake).toBe(false);
  });
  it('freezes particles outside either viewport axis without discarding them', () => {
    const cards = [card(1)];
    const liquid = new Liquid(cards, cards, { left: 0, right: 1000, top: 0, bottom: 1000 });
    liquid.activeBand = { left: 0, right: 400, top: 0, bottom: 400 };
    liquid.particles = [{ x: 800, y: 200, vx: 5, vy: 8 }, { x: 200, y: 800, vx: 5, vy: 8 }];
    const before = structuredClone(liquid.particles);
    liquid.step({ attraction: .3, viscosity: .4, cohesion: .5 });
    expect(liquid.particles).toEqual(before);
  });
  it('finds neighbours on wide boards without mixing spatial buckets', () => {
    const cards = [card(1)];
    const liquid = new Liquid(cards, cards, { left: 0, right: 1000, top: 0, bottom: 1000 });
    const particles = [{ x: 1900, y: 200, vx: 0, vy: 0 }, { x: 1910, y: 200, vx: 0, vy: 0 },
      { x: 1900 - 1536, y: 224, vx: 0, vy: 0 }];
    expect(liquid.pairs(particles)).toEqual([1, 0]);
  });
  it('preserves neighbour traversal across negative coordinates and changing pool widths', () => {
    const cards = [card(1)];
    const liquid = new Liquid(cards, cards, { left: -1000, right: 20000, top: -500, bottom: 500 });
    for (const width of [48, 1600, 20000, 48]) {
      const particles = Array.from({ length: 180 }, (_, i) => ({
        x: (i % 6) * width / 6 + Math.sin(i * 3) * 18 - 100,
        y: Math.cos(i * 5) * 60 - 20, vx: 0, vy: 0
      }));
      const expected: [number, number][] = [];
      particles.forEach((p, i) => {
        const neighbours = particles.slice(0, i).map((q, j) => ({ q, j }))
          .filter(({ q }) => (p.x - q.x) ** 2 + (p.y - q.y) ** 2 < SUPPORT ** 2)
          .sort((a, b) => Math.floor(a.q.y / SUPPORT) - Math.floor(b.q.y / SUPPORT) ||
            Math.floor(a.q.x / SUPPORT) - Math.floor(b.q.x / SUPPORT) || a.j - b.j);
        for (const { j } of neighbours) expected.push([i, j]);
      });
      expect(liquid.pairs(particles)).toEqual(expected.flat());
    }
    expect(liquid.pairs([])).toEqual([]);
  });
  it('handles a slick entirely outside the drawing grid', () => {
    const loops = blendedOutline([{ x: -160, y: 200, width: 50, height: 80, column: 0 }], [], viewport);
    expect(loops).toEqual([]);
  });
  it('does not retain neighbour or density state as particles leave and re-enter view', () => {
    const cards = [card(1)];
    const bounds = { left: -100, right: 600, top: -100, bottom: 600 };
    const liquid = new Liquid(cards, cards, bounds);
    for (const count of [80, 12, 0, 160, 20]) {
      liquid.particles = Array.from({ length: count }, (_, i) => ({
        x: 320 + (i % 8) * 8, y: 100 + Math.floor(i / 8) * 8,
        vx: Math.sin(i) * 15, vy: Math.cos(i) * 15
      }));
      const fresh = new Liquid(cards, cards, bounds);
      fresh.particles = structuredClone(liquid.particles);
      fresh.steps = liquid.steps;
      for (const right of [350, 300, 600]) {
        liquid.activeBand = { ...bounds, right };
        fresh.activeBand = { ...bounds, right };
        liquid.step(liquidSettings);
        fresh.step(liquidSettings);
        expect(liquid.particles).toEqual(fresh.particles);
      }
    }
  });
});
