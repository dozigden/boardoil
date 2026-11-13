import { describe, expect, it } from 'vitest';
import { Liquid, liquidSettings } from './physics';
import { LiquidResidency } from './liquidResidency';
import type { Bounds, GooCard } from './types';

const active: Bounds = { left: 0, right: 640, top: 0, bottom: 400 };
const card = (id: number, centreY: number, column = 0): GooCard => ({ id, group: 1, column,
  centreX: 170 + column * 300, centreY, width: 240, height: 100, radius: 12 });
function setup(cards: GooCard[]) {
  const liquid = new Liquid(cards, cards, { left: 0, right: 640, top: -10000, bottom: 10000 }, false);
  const residency = new LiquidResidency();
  residency.update(liquid, active);
  return { liquid, residency };
}
function scroll(liquid: Liquid, dy: number) {
  liquid.cards.forEach(c => { c.centreY += dy; });
  liquid.particles.forEach(p => { p.y += dy; });
}

describe('viewport liquid residency', () => {
  it.each([0, 149.99, 150, 150.01, -150])('matches initial seeding after leaving and returning with a %spx bridge offset', offset => {
    const cards = [card(1, 250), card(2, 250 + offset, 1), card(3, 358)];
    const bounds = { left: 0, right: 640, top: 0, bottom: 640 };
    const initial = new Liquid(cards, cards, bounds);
    const returning = new Liquid(cards, cards, bounds, false);
    const residency = new LiquidResidency();
    residency.update(returning, bounds);
    expect(returning.particles).toEqual(initial.particles);
    residency.update(returning, { ...bounds, top: 2000, bottom: 2640 });
    expect(returning.particles).toEqual([]);
    residency.update(returning, bounds);
    expect(returning.particles).toEqual(initial.particles);
  });

  it('seeds only the nearby portion of a long board and restores defaults after returning', () => {
    const { liquid, residency } = setup(Array.from({ length: 80 }, (_, i) => card(i, 100 + i * 140)));
    const initial = liquid.particles.length;
    expect(initial).toBeGreaterThan(100);
    expect(initial).toBeLessThan(1000);
    expect(liquid.particles.every(p => p.y >= 0 && p.y < 400)).toBe(true);
    const original = new Set(liquid.particles);
    scroll(liquid, -5000);
    residency.update(liquid, active);
    expect(liquid.particles.length).toBeGreaterThan(100);
    expect(liquid.particles.some(p => original.has(p))).toBe(false);
    scroll(liquid, 5000);
    residency.update(liquid, active);
    expect(liquid.particles.length).toBe(initial);
    expect(liquid.particles.every(p => p.vx === 0 && p.vy === 0)).toBe(true);
  });

  it('preserves existing particles and fills only the newly revealed part of a tall card', () => {
    const { liquid, residency } = setup([{ ...card(1, 400), height: 700 }]);
    for (let i = 0; i < 8; i++) liquid.step(liquidSettings);
    scroll(liquid, -80);
    const existing = new Set(liquid.particles);
    const visible = liquid.particles.filter(p => p.y >= 0 && p.y < 320);
    const before = structuredClone(visible);
    residency.update(liquid, active);
    expect(visible.every(p => liquid.particles.includes(p))).toBe(true);
    expect(visible).toEqual(before);
    expect(liquid.particles.filter(p => !existing.has(p)).every(p => p.y >= 320 && p.y < 400)).toBe(true);
    const old = new Set(visible);
    expect(liquid.particles.filter(p => !old.has(p) && p.y >= 320).length).toBeGreaterThan(0);
  });

  it('does not accumulate particles during small scroll reversals', () => {
    const { liquid, residency } = setup([{ ...card(1, 300), height: 500 }]);
    scroll(liquid, -10); residency.update(liquid, active);
    scroll(liquid, 10); residency.update(liquid, active);
    const particles = [...liquid.particles];
    for (let i = 0; i < 30; i++) {
      scroll(liquid, -10); residency.update(liquid, active);
      scroll(liquid, 10); residency.update(liquid, active);
    }
    expect(liquid.particles).toEqual(particles);
  });

  it('handles horizontal reveals, viewport growth and disappearance without retaining distant particles', () => {
    const { liquid, residency } = setup([card(1, 150), card(2, 150, 1)]);
    const before = liquid.particles.length;
    residency.update(liquid, { ...active, left: 1000, right: 1640 });
    expect(liquid.particles).toEqual([]);
    residency.update(liquid, { ...active, right: 300 });
    expect(liquid.particles.length).toBeGreaterThan(0);
    expect(liquid.particles.length).toBeLessThan(before);
    residency.update(liquid, active);
    expect(liquid.particles.length).toBe(before);
  });

  it('does not reseed a stationary column when its neighbour scrolls', () => {
    const { liquid, residency } = setup([card(1, 150), card(2, 150, 1), card(3, 900, 1)]);
    const stationary = liquid.particles.filter(p => p.x < 270);
    liquid.cards.filter(c => c.column === 1).forEach(c => { c.centreY -= 500; });
    liquid.particles.filter(p => p.x >= 320).forEach(p => { p.y -= 500; });
    residency.update(liquid, active);
    expect(liquid.particles.filter(p => p.x < 270)).toEqual(stationary);
  });
});
