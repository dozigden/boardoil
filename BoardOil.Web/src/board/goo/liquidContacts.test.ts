import { describe, expect, it } from 'vitest';
import { resolveLiquidContacts, type CompetingField } from './liquidContacts';

function pool(id: number, strength = .6): CompetingField {
  return { id, field: { originX: 0, originY: 0, nx: 21, ny: 21,
    values: new Float32Array(441).fill(1), strength: new Float32Array(441).fill(strength) } };
}
const centre = 10 * 21 + 10;

describe('Liquid contact tension', () => {
  it('smooths a tiny ownership island but preserves a broad region', () => {
    const a = pool(1, .4), b = pool(2, .6);
    a.field.strength[centre] = .8;
    expect(resolveLiquidContacts(a, [b])[centre]).toBeLessThan(.5);
    const broad = pool(3, .4);
    for (let y = 6; y <= 14; y++) for (let x = 6; x <= 14; x++) broad.field.strength[y * 21 + x] = .8;
    expect(resolveLiquidContacts(broad, [b])[centre]).toBeGreaterThan(.5);
  });

  it('pins the stronger card coating and keeps complementary contact values', () => {
    const a = pool(1, .4), b = pool(2, .6);
    a.field.strength[centre] = 2;
    const av = resolveLiquidContacts(a, [b]), bv = resolveLiquidContacts(b, [a]);
    expect(av[centre]).toBeGreaterThan(.5);
    for (let i = 0; i < av.length; i++) expect(av[i] + bv[i]).toBeCloseTo(1, 6);
    expect(resolveLiquidContacts(a, [b])).toEqual(av);
  });

  it('preserves empty pockets and isolated silhouettes', () => {
    const a = pool(1), b = pool(2);
    a.field.values[centre] = 0;
    b.field.values[centre] = 0;
    expect(resolveLiquidContacts(a, [b])[centre]).toBe(0);
    expect(resolveLiquidContacts(b, [a])[centre]).toBe(0);
    expect(resolveLiquidContacts(a, [])).toBe(a.field.values);
    const absent = pool(3);
    absent.field.values.fill(0);
    expect(resolveLiquidContacts(a, [absent])).toEqual(a.field.values);
  });

  it('keeps three-way contacts independent of paint order and handles offset grids', () => {
    const a = pool(1, .4), b = pool(2, .6), c = pool(3, .8);
    c.field.originX = 30;
    c.field.originY = 30;
    expect(resolveLiquidContacts(a, [b, c])).toEqual(resolveLiquidContacts(a, [c, b]));
    const cv = resolveLiquidContacts(c, [a]);
    const av = resolveLiquidContacts(a, [c]);
    expect(cv[0] + av[centre]).toBeCloseTo(1, 6);
    expect(cv[centre + 1]).toBe(1);
  });
});
