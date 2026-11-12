// Compare the actual visible contour over a time window, not particle speeds.
// An anchor prevents slow accumulated drift from looking like rest frame by frame.
const CELL = 8, RMS_TOLERANCE = .3, PEAK_TOLERANCE = 1.25, WINDOW = .5, SAMPLE_INTERVAL = .1;
const MAX_AWAKE_SECONDS = 10;
function visibleSurface(loops) {
  const cells = new Map(), points = [];
  for (const loop of loops) for (let i = 0; i < loop.length; i++) {
    const a = loop[i], b = loop[(i + 1) % loop.length];
    // Compare visible samples against the full nearby contour. Clipping the
    // target too turns a small ripple at the viewport edge into a large jump
    // along the clipping line when its intersection moves sideways.
    const segment = [...a, ...b];
    const minY = Math.max(56 - CELL, Math.min(a[1], b[1]));
    const maxY = Math.min(520 + CELL, Math.max(a[1], b[1]));
    for (let y = Math.floor(minY / CELL); y <= Math.floor(maxY / CELL); y++) {
      for (let x = Math.floor(Math.min(a[0], b[0]) / CELL); x <= Math.floor(Math.max(a[0], b[0]) / CELL); x++) {
        const key = `${x},${y}`;
        if (!cells.has(key)) cells.set(key, []);
        cells.get(key).push(segment);
      }
    }
    let start = 0, end = 1;
    const dy = b[1] - a[1];
    if (dy === 0) {
      if (a[1] < 56 || a[1] > 520) continue;
    } else {
      const t0 = (56 - a[1]) / dy, t1 = (520 - a[1]) / dy;
      start = Math.max(0, Math.min(t0, t1)); end = Math.min(1, Math.max(t0, t1));
      if (start > end) continue;
    }
    const dx = b[0] - a[0];
    const [x0, y0, x1, y1] = [a[0] + dx * start, a[1] + dy * start, a[0] + dx * end, a[1] + dy * end];
    const length = Math.hypot(x1 - x0, y1 - y0);
    const samples = Math.max(1, Math.ceil(length / 3));
    for (let s = 0; s <= samples; s++) points.push([x0 + (x1 - x0) * s / samples, y0 + (y1 - y0) * s / samples, length / (samples + 1)]);
  }
  return { cells, points };
}
function pointsNearSurface(points, surface) {
  let squaredMotion = 0, length = 0;
  for (const [px, py, weight] of points) {
    if (weight === 0) continue;
    const gx = Math.floor(px / CELL), gy = Math.floor(py / CELL);
    let nearest = Infinity;
    for (let y = gy - 1; y <= gy + 1; y++) for (let x = gx - 1; x <= gx + 1; x++) {
      for (const [x0, y0, x1, y1] of surface.cells.get(`${x},${y}`) ?? []) {
        const dx = x1 - x0, dy = y1 - y0, lengthSquared = dx * dx + dy * dy;
        let t = 0;
        if (lengthSquared) t = Math.max(0, Math.min(1, ((px - x0) * dx + (py - y0) * dy) / lengthSquared));
        nearest = Math.min(nearest, (px - x0 - t * dx) ** 2 + (py - y0 - t * dy) ** 2);
      }
    }
    // A small moving feature cannot disappear into the whole-outline average.
    if (nearest > PEAK_TOLERANCE ** 2) return false;
    squaredMotion += nearest * weight; length += weight;
  }
  return length === 0 || squaredMotion / length <= RMS_TOLERANCE ** 2;
}
export class Settling {
  constructor() { this.states = new WeakMap(); }
  state(liquid) {
    if (!this.states.has(liquid)) this.wake(liquid);
    return this.states.get(liquid);
  }
  wake(liquid) {
    this.states.set(liquid, { sleeping: false, limited: false, awakeFor: 0, anchor: null, since: 0, checked: -Infinity });
  }
  sleeping(liquid) { return this.state(liquid).sleeping; }
  limited(liquid) { return this.state(liquid).limited; }
  advance(liquid, seconds) {
    const state = this.state(liquid);
    if (!state.sleeping) state.awakeFor += seconds;
  }
  observe(liquid, loops, time) {
    const state = this.state(liquid);
    if (state.sleeping) return;
    // Wall time spent running, independent of solver speed. Wake resets this;
    // pausing, hiding the page and using Smooth do not spend the allowance.
    // Apply it after building the outline so the latest shape freezes in place.
    if (state.awakeFor >= MAX_AWAKE_SECONDS) {
      state.sleeping = true; state.limited = true; return;
    }
    if (time - state.checked < SAMPLE_INTERVAL) return;
    state.checked = time;
    const surface = visibleSurface(loops);
    if (!state.anchor || !pointsNearSurface(surface.points, state.anchor) || !pointsNearSurface(state.anchor.points, surface)) {
      state.anchor = surface; state.since = time;
    } else if (time - state.since >= WINDOW) {
      state.sleeping = true;
    }
  }
}
