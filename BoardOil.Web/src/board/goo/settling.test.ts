import { test, expect, vi } from 'vitest';
import type { Point } from './types';
import { Settling } from './settling';

const rectangle = (x = 100, y = 100, width = 100, height = 80): Point[] =>
  [[x, y], [x + width, y], [x + width, y + height], [x, y + height]];

test('each slick sleeps independently and waking starts a fresh observation window', () => {
  const settling = new Settling(56, 520), still = {}, moving = {};
  for (let i = 0; i < 8; i++) {
    settling.observe(still, [rectangle()], i * .11);
    settling.observe(moving, [rectangle(100 + i)], i * .11);
  }
  expect(settling.sleeping(still)).toBe(true);
  expect(settling.sleeping(moving)).toBe(false);
  settling.wake(still);
  settling.observe(still, [rectangle()], 5);
  expect(settling.sleeping(still)).toBe(false);
  settling.observe(still, [rectangle()], 5.55);
  expect(settling.sleeping(still)).toBe(true);
});

test('slow cumulative movement cannot qualify through small frame-to-frame changes', () => {
  const settling = new Settling(56, 520), liquid = {};
  for (let i = 0; i < 100; i++) settling.observe(liquid, [rectangle(100 + i * .15)], i * .11);
  expect(settling.sleeping(liquid)).toBe(false);
});

test('a small moving droplet cannot be hidden by a large stationary outline', () => {
  const settling = new Settling(56, 520), liquid = {};
  for (let i = 0; i < 20; i++) {
    settling.observe(liquid, [rectangle(100, 100, 400, 300), rectangle(600 + i * 2, 200, 4, 4)], i * .11);
  }
  expect(settling.sleeping(liquid)).toBe(false);
});

test('both appearing and disappearing contours restart settling', () => {
  for (const disappearing of [false, true]) {
    const settling = new Settling(56, 520), liquid = {};
    for (let i = 0; i < 8; i++) {
      const loops = [rectangle()];
      if ((i < 4) === disappearing) loops.push(rectangle(400, 200));
      settling.observe(liquid, loops, i * .11);
    }
    expect(settling.sleeping(liquid)).toBe(false);
  }
});

test('movement outside the visible board does not keep its slick awake', () => {
  const settling = new Settling(56, 520), liquid = {};
  for (let i = 0; i < 8; i++) {
    settling.observe(liquid, [rectangle(), rectangle(300 + i * 10, -100)], i * .11);
  }
  expect(settling.sleeping(liquid)).toBe(true);
});

test('repeated draws without simulation time do not count as settling', () => {
  const settling = new Settling(56, 520), liquid = {};
  for (let i = 0; i < 100; i++) settling.observe(liquid, [rectangle()], 1);
  expect(settling.sleeping(liquid)).toBe(false);
});

test('a tiny ripple across the viewport edge is measured perpendicular to its outline', () => {
  const settling = new Settling(56, 520), liquid = {};
  for (let i = 0; i < 8; i++) {
    const dy = i % 2 * .1;
    const loop: Point[] = [[100, 518 + dy], [500, 522 + dy], [500, 550 + dy], [100, 546 + dy]];
    settling.observe(liquid, [loop], i * .11);
  }
  expect(settling.sleeping(liquid)).toBe(true);
});

test('the hard limit stops continuous motion even when the solver advances slowly', () => {
  const settling = new Settling(56, 520), liquid = {};
  for (let i = 0; i < 10; i++) {
    settling.advance(liquid, 1);
    settling.observe(liquid, [rectangle(100 + i * 10)], i * .11);
    expect(settling.sleeping(liquid)).toBe(i === 9);
  }
  expect(settling.limited(liquid)).toBe(true);
});

test('interaction resets the hard limit and clears its reason', () => {
  const settling = new Settling(56, 520), liquid = {};
  settling.advance(liquid, 9);
  settling.observe(liquid, [rectangle()], 0);
  settling.wake(liquid);
  settling.advance(liquid, 9);
  settling.observe(liquid, [rectangle(200)], 1);
  expect(settling.sleeping(liquid)).toBe(false);
  settling.advance(liquid, 1);
  settling.observe(liquid, [rectangle(300)], 1.01);
  expect(settling.limited(liquid)).toBe(true);
  settling.wake(liquid);
  expect(settling.sleeping(liquid)).toBe(false);
  expect(settling.limited(liquid)).toBe(false);
});

test('redrawing while paused does not spend the hard limit', () => {
  const settling = new Settling(56, 520), liquid = {};
  settling.advance(liquid, 9);
  for (let i = 0; i < 100; i++) settling.observe(liquid, [rectangle()], 1);
  expect(settling.sleeping(liquid)).toBe(false);
  expect(settling.limited(liquid)).toBe(false);
});

test('continuous scrolling skips contour comparisons then starts a fresh settling window', () => {
  const settling = new Settling(56, 520), liquid = {}, loops = [rectangle()];
  const iterate = vi.spyOn(loops, Symbol.iterator);
  for (let i = 0; i < 120; i++) {
    settling.wake(liquid, true);
    settling.advance(liquid, .016);
    settling.observe(liquid, loops, i * .016);
  }
  expect(iterate).not.toHaveBeenCalled();
  expect(settling.sleeping(liquid)).toBe(false);
  settling.advance(liquid, .085);
  settling.observe(liquid, loops, 2);
  expect(iterate).toHaveBeenCalledOnce();
  expect(settling.sleeping(liquid)).toBe(false);
  settling.advance(liquid, .4);
  settling.observe(liquid, loops, 2.4);
  expect(settling.sleeping(liquid)).toBe(false);
  settling.advance(liquid, .11);
  settling.observe(liquid, loops, 2.51);
  expect(settling.sleeping(liquid)).toBe(true);
  expect(settling.limited(liquid)).toBe(false);
});

test('deferred observation does not spend its quiet interval while paused or bypass the hard limit', () => {
  const settling = new Settling(56, 520), liquid = {}, loops = [rectangle()];
  const iterate = vi.spyOn(loops, Symbol.iterator);
  settling.wake(liquid, true);
  for (let i = 0; i < 20; i++) settling.observe(liquid, loops, i);
  expect(iterate).not.toHaveBeenCalled();
  settling.advance(liquid, 10);
  settling.observe(liquid, loops, 20);
  expect(settling.sleeping(liquid)).toBe(true);
  expect(settling.limited(liquid)).toBe(true);
});
