import { describe, expect, it } from 'vitest';
import { smoothParticleDensity } from './liquidDensity';

const size = 25;
function field(value: (x: number, y: number) => number) {
  return Float32Array.from({ length: size * size }, (_, i) => value(i % size, Math.floor(i / size)));
}

describe('Liquid particle density smoothing', () => {
  it('removes fine ripples while retaining the broader density gradient', () => {
    const values = field(x => .6 + .01 * x + (x % 2 ? .2 : -.2));
    smoothParticleDensity(values, size, size);
    for (let x = 6; x < size - 6; x++) expect(values[12 * size + x]).toBeCloseTo(.6 + .01 * x, 6);
  });

  it('preserves interior particle mass and treats both axes equally', () => {
    const values = field((x, y) => x === 12 && y === 12 ? 1 : 0);
    smoothParticleDensity(values, size, size);
    expect(values.reduce((sum, value) => sum + value, 0)).toBeCloseTo(1, 6);
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
      expect(values[y * size + x]).toBe(values[x * size + y]);
    }
  });
});
