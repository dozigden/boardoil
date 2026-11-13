import { expect, it } from 'vitest';
import { traceContours } from './contours';

function fieldWithCentre(a: number, b = 0, c = 0, d = 0) {
  const field = new Float32Array(16);
  field[5] = a; field[6] = b; field[9] = c; field[10] = d;
  return field;
}

it('preserves interpolated coordinates, loop start and winding', () => {
  expect(traceContours(fieldWithCentre(1), 4, 4, -9, 6, 3))
    .toEqual([[[-6, 7.5], [-7.5, 9], [-6, 10.5], [-4.5, 9]]]);
  expect(traceContours(fieldWithCentre(.5), 4, 4, 0, 0, 1))
    .toEqual([[[1, 1], [1, 1], [1, 1], [1, 1]]]);
});

it('retains the saddle decision for joined and separate diagonal droplets', () => {
  expect(traceContours(fieldWithCentre(1, 0, 0, 1), 4, 4, 0, 0, 1))
    .toEqual([[[1, .5], [.5, 1], [1, 1.5], [1.5, 2], [2, 2.5], [2.5, 2], [2, 1.5], [1.5, 1]]]);
  const separate = traceContours(fieldWithCentre(.75, 0, 0, .75), 4, 4, 0, 0, 1);
  expect(separate).toHaveLength(2);
  expect(separate.map(loop => loop.length)).toEqual([4, 4]);
  expect(separate[0][0]).toEqual([1, 2 / 3]);
  expect(separate[1][0]).toEqual([2, 1 + 2 / 3]);
});

it('retains holes and discards open contours at grid boundaries', () => {
  const field = new Float32Array(49);
  for (let y = 1; y < 6; y++) for (let x = 1; x < 6; x++) field[y * 7 + x] = 1;
  field[24] = 0;
  const loops = traceContours(field, 7, 7, 0, 0, 1);
  expect(loops).toHaveLength(2);
  expect(loops.map(loop => loop.length)).toEqual([20, 4]);
  expect(traceContours(new Float32Array([0, 1, 0, 1]), 2, 2, 0, 0, 1)).toEqual([]);
});

it('clears reused storage across growing, shrinking and empty grids without changing previous outlines', () => {
  const first = traceContours(fieldWithCentre(1), 4, 4, 0, 0, 1);
  const saved = structuredClone(first);
  for (const size of [80, 3, 0, 40, 4]) {
    const field = new Float32Array(size * size);
    for (let y = 1; y < size - 1; y += 2) for (let x = 1; x < size - 1; x += 2) field[y * size + x] = 1;
    const loops = traceContours(field, size, size, -100, -30, 3);
    expect(loops).toHaveLength(Math.ceil(Math.max(0, size - 2) / 2) ** 2);
    expect(traceContours(new Float32Array(size * size), size, size, 0, 0, 1)).toEqual([]);
    expect(traceContours(fieldWithCentre(1), 4, 4, 0, 0, 1)).toEqual(saved);
    expect(first).toEqual(saved);
  }
});
