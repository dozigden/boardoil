import test from 'node:test';
import assert from 'node:assert/strict';
import { Settling } from './settling.js';

const rectangle = (x = 100, y = 100, width = 100, height = 80) =>
  [[x, y], [x + width, y], [x + width, y + height], [x, y + height]];

test('each slick sleeps independently and waking starts a fresh observation window', () => {
  const settling = new Settling(), still = {}, moving = {};
  for (let i = 0; i < 8; i++) {
    settling.observe(still, [rectangle()], i * .11);
    settling.observe(moving, [rectangle(100 + i)], i * .11);
  }
  assert.equal(settling.sleeping(still), true);
  assert.equal(settling.sleeping(moving), false);
  settling.wake(still);
  settling.observe(still, [rectangle()], 5);
  assert.equal(settling.sleeping(still), false);
  settling.observe(still, [rectangle()], 5.55);
  assert.equal(settling.sleeping(still), true);
});

test('slow cumulative movement cannot qualify through small frame-to-frame changes', () => {
  const settling = new Settling(), liquid = {};
  for (let i = 0; i < 100; i++) settling.observe(liquid, [rectangle(100 + i * .15)], i * .11);
  assert.equal(settling.sleeping(liquid), false);
});

test('a small moving droplet cannot be hidden by a large stationary outline', () => {
  const settling = new Settling(), liquid = {};
  for (let i = 0; i < 20; i++) {
    settling.observe(liquid, [rectangle(100, 100, 400, 300), rectangle(600 + i * 2, 200, 4, 4)], i * .11);
  }
  assert.equal(settling.sleeping(liquid), false);
});

test('both appearing and disappearing contours restart settling', () => {
  for (const disappearing of [false, true]) {
    const settling = new Settling(), liquid = {};
    for (let i = 0; i < 8; i++) {
      const loops = [rectangle()];
      if ((i < 4) === disappearing) loops.push(rectangle(400, 200));
      settling.observe(liquid, loops, i * .11);
    }
    assert.equal(settling.sleeping(liquid), false);
  }
});

test('movement outside the visible board does not keep its slick awake', () => {
  const settling = new Settling(), liquid = {};
  for (let i = 0; i < 8; i++) {
    settling.observe(liquid, [rectangle(), rectangle(300 + i * 10, -100)], i * .11);
  }
  assert.equal(settling.sleeping(liquid), true);
});

test('repeated draws without simulation time do not count as settling', () => {
  const settling = new Settling(), liquid = {};
  for (let i = 0; i < 100; i++) settling.observe(liquid, [rectangle()], 1);
  assert.equal(settling.sleeping(liquid), false);
});

test('a tiny ripple across the viewport edge is measured perpendicular to its outline', () => {
  const settling = new Settling(), liquid = {};
  for (let i = 0; i < 8; i++) {
    const dy = i % 2 * .1;
    const loop = [[100, 518 + dy], [500, 522 + dy], [500, 550 + dy], [100, 546 + dy]];
    settling.observe(liquid, [loop], i * .11);
  }
  assert.equal(settling.sleeping(liquid), true);
});

test('the hard limit stops continuous motion even when the solver advances slowly', () => {
  const settling = new Settling(), liquid = {};
  for (let i = 0; i < 10; i++) {
    settling.advance(liquid, 1);
    settling.observe(liquid, [rectangle(100 + i * 10)], i * .11);
    assert.equal(settling.sleeping(liquid), i === 9);
  }
  assert.equal(settling.limited(liquid), true);
});

test('interaction resets the hard limit and clears its reason', () => {
  const settling = new Settling(), liquid = {};
  settling.advance(liquid, 9);
  settling.observe(liquid, [rectangle()], 0);
  settling.wake(liquid);
  settling.advance(liquid, 9);
  settling.observe(liquid, [rectangle(200)], 1);
  assert.equal(settling.sleeping(liquid), false);
  settling.advance(liquid, 1);
  settling.observe(liquid, [rectangle(300)], 1.01);
  assert.equal(settling.limited(liquid), true);
  settling.wake(liquid);
  assert.equal(settling.sleeping(liquid), false);
  assert.equal(settling.limited(liquid), false);
});

test('redrawing while paused does not spend the hard limit', () => {
  const settling = new Settling(), liquid = {};
  settling.advance(liquid, 9);
  for (let i = 0; i < 100; i++) settling.observe(liquid, [rectangle()], 1);
  assert.equal(settling.sleeping(liquid), false);
  assert.equal(settling.limited(liquid), false);
});
