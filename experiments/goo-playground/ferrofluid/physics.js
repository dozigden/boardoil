// A small 2D experiment derived from Clavet et al.'s double-density relaxation.
// No springs, prescribed bridges, particle respawning, or magnetic spike model.
// Lengths are screen pixels; coefficients are artistic, not material measurements.
export const WIDTH = 960, HEIGHT = 520, SUPPORT = 24, DT = 1 / 120;
const clamp = (v, lo, hi) => Math.max(lo, Math.min(hi, v));

export function cardDistance(x, y, card) {
  const dx = x - card.x, dy = y - card.y;
  const radius = card.radius ?? 9;
  const qx = Math.abs(dx) - (card.width ?? 180) / 2 + radius;
  const qy = Math.abs(dy) - (card.height ?? 96) / 2 + radius;
  const ox = Math.max(0, qx), oy = Math.max(0, qy), length = Math.hypot(ox, oy);
  const distance = length + Math.min(Math.max(qx, qy), 0) - radius;
  if (length > 0) return { distance, nx: Math.sign(dx) * ox / length, ny: Math.sign(dy) * oy / length };
  if (qx > qy) return { distance, nx: Math.sign(dx) || 1, ny: 0 };
  return { distance, nx: 0, ny: Math.sign(dy) || 1 };
}

function wettedBodies(cards) {
  const bodies = [];
  const ordered = [...cards].sort((a, b) => (a.column ?? 0) - (b.column ?? 0) || a.y - b.y);
  for (const card of ordered) {
    const previous = bodies.at(-1);
    const top = card.y - card.height / 2;
    const gap = previous ? top - previous.y - previous.height / 2 : Infinity;
    // A narrow slot between aligned cards cannot hold a useful liquid layer.
    // Wet the outside of that stack as one surface, rather than attracting and
    // squeezing particles between every pair of rounded card corners.
    if (previous && card.column !== undefined && previous.column === card.column &&
        previous.x === card.x && previous.width === card.width && gap >= 0 && gap <= 16) {
      const stackTop = previous.y - previous.height / 2;
      const bottom = card.y + card.height / 2;
      previous.y = (stackTop + bottom) / 2;
      previous.height = bottom - stackTop;
    } else bodies.push({ ...card });
  }
  return bodies;
}

export class Liquid {
  constructor(cards, options = {}) {
    this.cards = cards;
    this.obstacles = options.obstacles ?? cards;
    this.otherObstacles = this.obstacles.filter(card => !cards.includes(card));
    this.bounds = options.bounds ?? { left: 18, right: WIDTH - 18, top: 77, bottom: HEIGHT - 18 };
    this.activeBand = options.activeBand;
    this.board = options.board ?? false;
    this.reset();
  }
  surfaceBodies() {
    return wettedBodies(this.cards);
  }
  reset() {
    this.particles = [];
    this.steps = 0;
    if (this.board) { this.seedBoard(); return; }
    // Start with wetted cards and one finite puddle between them. This seed is
    // discarded after reset: no connection geometry participates in dynamics.
    const [a, b] = this.cards;
    for (let y = 80, row = 0; y < HEIGHT - 35; y += 6.9, row++) {
      for (let x = 40 + (row % 2) * 4; x < WIDTH - 40; x += 8) {
        const da = cardDistance(x, y, a).distance, db = cardDistance(x, y, b).distance;
        const t = clamp((x - a.x) / (b.x - a.x), 0, 1);
        const middle = a.y + (b.y - a.y) * t;
        const bridge = x > a.x && x < b.x && Math.abs(y - middle) < 19;
        if (Math.min(da, db) < 3 || (Math.min(da, db) > 25 && !bridge)) continue;
        const jitter = Math.sin(x * 13.4 + y * 9.7) * .35;
        this.particles.push({ x: x + jitter, y: y - jitter, vx: 0, vy: 0 });
      }
    }
  }
  seedBoard() {
    // One initial coating per wetted surface, with finite puddles between peers.
    // After this, scrolling transports the existing particles; it never reseeds.
    const bodies = this.surfaceBodies();
    const necks = [];
    for (const a of this.cards) for (const b of this.cards) {
      if (b.column !== a.column + 1 || Math.abs(a.y - b.y) > 150) continue;
      necks.push([a, b]);
    }
    const top = Math.min(...this.cards.map(c => c.y - c.height / 2)) - 28;
    const bottom = Math.max(...this.cards.map(c => c.y + c.height / 2)) + 28;
    for (let y = top, row = 0; y < bottom; y += 8.5, row++) {
      for (let x = 40 + (row % 2) * 5; x < WIDTH - 40; x += 10) {
        const distance = Math.min(...bodies.map(c => cardDistance(x, y, c).distance));
        if (distance < 3) continue;
        let puddle = false;
        for (const [a, b] of necks) {
          if (x < a.x || x > b.x) continue;
          const t = (x - a.x) / (b.x - a.x);
          if (Math.abs(y - (a.y + (b.y - a.y) * t)) < 14) puddle = true;
        }
        if (distance > 21 && !puddle) continue;
        if (this.obstacles.some(c => cardDistance(x, y, c).distance < 3)) continue;
        const jitter = Math.sin(x * 13.4 + y * 9.7) * .35;
        this.particles.push({ x: x + jitter, y: y - jitter, vx: 0, vy: 0 });
      }
    }
  }
  activeParticles() {
    if (!this.activeBand) return this.particles;
    return this.particles.filter(p => p.y >= this.activeBand.top && p.y <= this.activeBand.bottom);
  }
  pairs(p) {
    const buckets = new Map(), pairs = [];
    for (let i = 0; i < p.length; i++) {
      const gx = Math.floor(p[i].x / SUPPORT), gy = Math.floor(p[i].y / SUPPORT);
      for (let y = gy - 1; y <= gy + 1; y++) for (let x = gx - 1; x <= gx + 1; x++) {
        const bucket = buckets.get(y * 64 + x);
        if (!bucket) continue;
        for (const j of bucket) {
          if ((p[i].x - p[j].x) ** 2 + (p[i].y - p[j].y) ** 2 < SUPPORT ** 2) pairs.push([i, j]);
        }
      }
      const key = gy * 64 + gx;
      if (!buckets.has(key)) buckets.set(key, []);
      buckets.get(key).push(i);
    }
    return pairs;
  }
  step({ attraction, viscosity, cohesion }) {
    // Offscreen particles keep their state, but do not spend time in the solver.
    // The board's scroll transport moves them back into this padded active band.
    const particles = this.activeParticles();
    const bodies = this.surfaceBodies();
    const obstacles = [...bodies, ...this.otherObstacles];
    let pairs = this.pairs(particles);
    // Symmetric velocity smoothing dissipates relative motion without pulling
    // a stationary outline into a different shape (viscosity != surface tension).
    for (const [i, j] of pairs) {
      const a = particles[i], b = particles[j];
      const q = 1 - Math.hypot(a.x - b.x, a.y - b.y) / SUPPORT;
      const amount = Math.min(.12, viscosity * q * .035);
      const ix = (b.vx - a.vx) * amount, iy = (b.vy - a.vy) * amount;
      a.vx += ix; a.vy += iy; b.vx -= ix; b.vy -= iy;
    }
    for (const p of particles) {
      p.px = p.x; p.py = p.y;
      for (const card of bodies) {
        const { distance, nx, ny } = cardDistance(p.x, p.y, card);
        const pull = attraction * 1500 / (1 + (Math.max(distance, 0) / 55) ** 2);
        p.vx -= nx * pull * DT; p.vy -= ny * pull * DT;
      }
      const damping = Math.exp(-1.2 * DT);
      p.vx *= damping; p.vy *= damping;
      const limit = Math.max(1, Math.hypot(p.vx, p.vy) / 650);
      p.x += p.vx / limit * DT; p.y += p.vy / limit * DT;
    }
    pairs = this.pairs(particles);
    const density = new Float64Array(particles.length), near = new Float64Array(particles.length);
    const neighbours = particles.map(() => []);
    for (const [i, j] of pairs) {
      const a = particles[i], b = particles[j];
      const q = 1 - Math.hypot(a.x - b.x, a.y - b.y) / SUPPORT;
      density[i] += q * q; density[j] += q * q;
      near[i] += q ** 3; near[j] += q ** 3;
      neighbours[i].push(j); neighbours[j].push(i);
    }
    // Opposing density and near-density pressures keep a liquid neighbourhood
    // together without allowing particles to collapse into a single point.
    // Alternate traversal direction to reduce persistent directional bias.
    for (let n = 0; n < particles.length; n++) {
      let i = n;
      if (this.steps % 2) i = particles.length - 1 - n;
      const a = particles[i];
      let pressure = 3500 * (density[i] - 5);
      if (pressure < 0) pressure *= cohesion;
      const nearPressure = 8500 * near[i];
      let sx = 0, sy = 0;
      for (const j of neighbours[i]) {
        const b = particles[j], dx = b.x - a.x, dy = b.y - a.y, length = Math.hypot(dx, dy);
        if (length < .0001 || length >= SUPPORT) continue;
        const q = 1 - length / SUPPORT;
        const amount = clamp(DT * DT * (pressure * q + nearPressure * q * q) / 2, -.6, .6);
        const mx = dx / length * amount, my = dy / length * amount;
        b.x += mx; b.y += my; sx -= mx; sy -= my;
      }
      a.x += sx; a.y += sy;
    }
    for (const p of particles) {
      // Cards are solid obstacles as well as attractive bodies.
      for (const card of obstacles) {
        const { distance, nx, ny } = cardDistance(p.x, p.y, card);
        if (distance < 3) { p.x += nx * (3 - distance); p.y += ny * (3 - distance); }
      }
      p.x = clamp(p.x, this.bounds.left, this.bounds.right);
      p.y = clamp(p.y, this.bounds.top, this.bounds.bottom);
      p.vx = clamp((p.x - p.px) / DT, -650, 650); p.vy = clamp((p.y - p.py) / DT, -650, 650);
    }
    this.steps++;
  }
}
