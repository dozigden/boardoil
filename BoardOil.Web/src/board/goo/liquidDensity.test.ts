import { describe, expect, it } from 'vitest';
import { DensitySmoother } from './liquidDensity';

const size = 25;
function field(value: (x: number, y: number) => number) {
  return Float32Array.from({ length: size * size }, (_, i) => value(i % size, Math.floor(i / size)));
}
function smooth(values: Float32Array) {
  const smoother = new DensitySmoother();
  smoother.prepare(size, size);
  smoother.include(0, 0, size - 1, size - 1);
  smoother.smooth(values);
}

describe('Liquid particle density smoothing', () => {
  it('removes fine ripples while retaining the broader density gradient', () => {
    const values = field(x => .6 + .01 * x + (x % 2 ? .2 : -.2));
    smooth(values);
    for (let x = 6; x < size - 6; x++) expect(values[12 * size + x]).toBeCloseTo(.6 + .01 * x, 6);
  });

  it('preserves interior particle mass and treats both axes equally', () => {
    const values = field((x, y) => x === 12 && y === 12 ? 1 : 0);
    smooth(values);
    expect(values.reduce((sum, value) => sum + value, 0)).toBeCloseTo(1, 6);
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
      expect(values[y * size + x]).toBe(values[x * size + y]);
    }
  });

  it('retains the complete kernel across tile and viewport edges after resizing and clearing', () => {
    const smoother = new DensitySmoother();
    const kernel = [1, 6, 15, 20, 15, 6, 1];
    for (const [nx, ny] of [[25, 25], [51, 35], [9, 7], [25, 25]]) {
      const sources = [[0, 0], [7, 7], [8, 8], [nx - 1, ny - 1]];
      for (const points of [sources, [], sources.slice(1, 2)]) {
        const values = new Float32Array(nx * ny);
        smoother.prepare(nx, ny);
        const inside = points.filter(([x, y]) => x < nx && y < ny);
        for (const [x, y] of inside) {
          values[y * nx + x] += 1;
          smoother.include(x, y, x, y);
        }
        smoother.smooth(values);
        for (let y = 0; y < ny; y++) for (let x = 0; x < nx; x++) {
          let expected = 0;
          if (x >= 3 && x < nx - 3 && y >= 3 && y < ny - 3) {
            for (const [px, py] of inside) {
              if (Math.abs(x - px) <= 3 && Math.abs(y - py) <= 3) {
                expected += kernel[x - px + 3] * kernel[y - py + 3] / 4096;
              }
            }
          }
          expect(values[y * nx + x]).toBe(expected);
        }
      }
    }
  });
});
