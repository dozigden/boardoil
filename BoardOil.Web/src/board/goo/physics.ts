import type { GooCard, Particle, Bounds } from './types';
// A small 2D experiment derived from Clavet et al.'s double-density relaxation.
// No springs, prescribed bridges, particle respawning, or magnetic spike model.
// Lengths are screen pixels; coefficients are artistic, not material measurements.
export const SUPPORT = 24, DT = 1 / 120;
export const liquidSettings = { attraction: .3, viscosity: .4, cohesion: .5 };
const length2d = (x: number, y: number) => Math.sqrt(x * x + y * y);
const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));

export function cardDistance(x: number, y: number, card: GooCard) {
  const dx = x - card.x, dy = y - card.y;
  const radius = Math.min(card.radius, card.width / 2, card.height / 2);
  const qx = Math.abs(dx) - card.width / 2 + radius;
  const qy = Math.abs(dy) - card.height / 2 + radius;
  const ox = Math.max(0, qx), oy = Math.max(0, qy), length = length2d(ox, oy);
  const distance = length + Math.min(Math.max(qx, qy), 0) - radius;
  if (length > 0) return { distance, nx: Math.sign(dx) * ox / length, ny: Math.sign(dy) * oy / length };
  if (qx > qy) return { distance, nx: Math.sign(dx) || 1, ny: 0 };
  return { distance, nx: 0, ny: Math.sign(dy) || 1 };
}

function wettedBodies(cards: GooCard[]) {
  const bodies: GooCard[] = [];
  const ordered = [...cards].sort((a, b) => (a.column ?? 0) - (b.column ?? 0) || a.y - b.y);
  for (const card of ordered) {
    const previous = bodies[bodies.length - 1];
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
  particles: Particle[] = [];
  steps = 0;
  private pairIndices: number[] = [];
  private bucketIndices = new Map<number, number>();
  private buckets: number[][] = [];
  private columns: number[] = [];
  private neighbours: number[][] = [];
  private density = new Float64Array(0);
  private nearDensity = new Float64Array(0);
  otherObstacles: GooCard[];
  activeBand: Bounds;
  constructor(public cards: GooCard[], public obstacles: GooCard[], public bounds: Bounds) {
    this.otherObstacles = obstacles.filter(card => !cards.includes(card));
    this.activeBand = { ...bounds };
    this.seedBoard();
  }
  surfaceBodies() { return wettedBodies(this.cards); }
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
      for (let x = this.bounds.left + (row % 2) * 5; x < this.bounds.right; x += 10) {
        const distance = Math.min(...bodies.map(c => cardDistance(x, y, c).distance));
        if (distance < 3) continue;
        let puddle = false;
        for (const [a, b] of necks) {
          if (x < a.x || x > b.x) continue;
          const t = (x - a.x) / (b.x - a.x);
          if (Math.abs(y - (a.y + (b.y - a.y) * t)) < 14) puddle = true;
        }
        if (distance > 28 && !puddle) continue;
        if (this.obstacles.some(c => cardDistance(x, y, c).distance < 3)) continue;
        const jitter = Math.sin(x * 13.4 + y * 9.7) * .35;
        this.particles.push({ x: x + jitter, y: y - jitter, vx: 0, vy: 0 });
      }
    }
  }
  activeParticles() {
    if (!this.activeBand) return this.particles;
    return this.particles.filter(p => p.y >= this.activeBand.top && p.y <= this.activeBand.bottom && p.x >= this.activeBand.left && p.x <= this.activeBand.right);
  }
  pairs(p: Particle[]) {
    // Scratch storage belongs to the pool and is reused by each neighbour pass.
    const { bucketIndices, buckets, pairIndices: pairs, columns } = this;
    bucketIndices.clear();
    pairs.length = 0;
    let minColumn = Infinity, maxColumn = -Infinity, bucketCount = 0;
    for (let i = 0; i < p.length; i++) {
      const column = Math.floor(p[i].x / SUPPORT);
      columns[i] = column;
      minColumn = Math.min(minColumn, column);
      maxColumn = Math.max(maxColumn, column);
    }
    // Leave a cell on either side for neighbour queries. A fixed stride aliases
    // distant cells on wide boards; a per-pool stride keeps numeric keys unique.
    const stride = maxColumn - minColumn + 3;
    for (let i = 0; i < p.length; i++) {
      const gy = Math.floor(p[i].y / SUPPORT);
      const key = gy * stride + columns[i] - minColumn + 1;
      for (let y = -1; y <= 1; y++) for (let x = -1; x <= 1; x++) {
        const bucketIndex = bucketIndices.get(key + y * stride + x);
        if (bucketIndex === undefined) continue;
        const bucket = buckets[bucketIndex];
        for (const j of bucket) {
          if ((p[i].x - p[j].x) ** 2 + (p[i].y - p[j].y) ** 2 < SUPPORT ** 2) pairs.push(i, j);
        }
      }
      let bucketIndex = bucketIndices.get(key);
      if (bucketIndex === undefined) {
        bucketIndex = bucketCount++;
        bucketIndices.set(key, bucketIndex);
        if (!buckets[bucketIndex]) buckets[bucketIndex] = [];
        buckets[bucketIndex].length = 0;
      }
      buckets[bucketIndex].push(i);
    }
    return pairs;
  }
  step({ attraction, viscosity, cohesion }: typeof liquidSettings) {
    // Offscreen particles keep their state, but do not spend time in the solver.
    // The board's scroll transport moves them back into this padded active band.
    const particles = this.activeParticles();
    const bodies = this.surfaceBodies();
    const obstacles = [...bodies, ...this.otherObstacles];
    let pairs = this.pairs(particles);
    // Symmetric velocity smoothing dissipates relative motion without pulling
    // a stationary outline into a different shape (viscosity != surface tension).
    for (let pair = 0; pair < pairs.length; pair += 2) {
      const i = pairs[pair], j = pairs[pair + 1];
      const a = particles[i], b = particles[j];
      const q = 1 - length2d(a.x - b.x, a.y - b.y) / SUPPORT;
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
      const limit = Math.max(1, length2d(p.vx, p.vy) / 650);
      p.x += p.vx / limit * DT; p.y += p.vy / limit * DT;
    }
    pairs = this.pairs(particles);
    if (this.density.length < particles.length) {
      this.density = new Float64Array(particles.length);
      this.nearDensity = new Float64Array(particles.length);
    }
    const { density, nearDensity: near, neighbours } = this;
    density.fill(0, 0, particles.length);
    near.fill(0, 0, particles.length);
    for (let i = 0; i < particles.length; i++) {
      if (!neighbours[i]) neighbours[i] = [];
      neighbours[i].length = 0;
    }
    for (let pair = 0; pair < pairs.length; pair += 2) {
      const i = pairs[pair], j = pairs[pair + 1];
      const a = particles[i], b = particles[j];
      const q = 1 - length2d(a.x - b.x, a.y - b.y) / SUPPORT;
      density[i] += q * q; density[j] += q * q;
      const nearDensity = q * q * q;
      near[i] += nearDensity; near[j] += nearDensity;
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
        const b = particles[j], dx = b.x - a.x, dy = b.y - a.y, length = length2d(dx, dy);
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
        if (Math.abs(p.x - card.x) >= card.width / 2 + 3 ||
            Math.abs(p.y - card.y) >= card.height / 2 + 3) continue;
        const { distance, nx, ny } = cardDistance(p.x, p.y, card);
        if (distance < 3) { p.x += nx * (3 - distance); p.y += ny * (3 - distance); }
      }
      p.x = clamp(p.x, this.bounds.left, this.bounds.right);
      p.y = clamp(p.y, this.bounds.top, this.bounds.bottom);
      p.vx = clamp((p.x - p.px!) / DT, -650, 650); p.vy = clamp((p.y - p.py!) / DT, -650, 650);
    }
    this.steps++;
  }
}
