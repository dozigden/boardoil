import { cardDistance, DT, Liquid, liquidSettings } from './physics';
import { Settling } from './settling';
import { LiquidResidency } from './liquidResidency';
import { LiquidWorker, type SimulationWorker } from './liquidWorker';
import { drawLiquids } from './liquidSurface';
import type { GooCard, Viewport } from './types';

export type SlickAppearance = { id: number; name: string; colour: string };
export type SlickActivity = SlickAppearance & { sleeping: boolean; limited: boolean };
type Pool = { liquid: Liquid; membership: string; residency: LiquidResidency };

export class GooScene {
  private cards = new Map<number, GooCard>();
  private pools = new Map<number, Pool>();
  private settling = new Settling(0, 0);
  private appearances: SlickAppearance[] = [];
  private accumulator = 0;
  private worker: LiquidWorker | null = null;
  private layoutRevision = 0;
  private surfaceRevision = 0;
  private simulationRunning = false;
  private viewport: Viewport = { left: 0, top: 0, width: 0, height: 0, clipTop: 0, clipBottom: 0 };

  constructor(private workerFactory?: () => SimulationWorker | null, private ready: () => void = () => {}) {}

  update(nextCards: GooCard[], appearances: SlickAppearance[], viewport: Viewport, reason: 'layout' | 'scroll' = 'layout') {
    const previous = new Map([...this.cards].map(([id, card]) => [id, { ...card }]));
    const next = new Map(nextCards.map(card => [card.id, card]));
    const geometryChanged = next.size !== previous.size || nextCards.some(card => {
      const old = previous.get(card.id);
      return !old || Object.keys(card).some(key => card[key as keyof GooCard] !== old[key as keyof GooCard]);
    });
    const viewportChanged = Object.keys(viewport).some(key => viewport[key as keyof Viewport] !== this.viewport[key as keyof Viewport]);
    if (geometryChanged || viewportChanged) this.surfaceRevision++;
    if (reason !== 'scroll' && (geometryChanged || viewportChanged)) this.layoutRevision++;
    for (const card of nextCards) {
      const existing = this.cards.get(card.id);
      if (existing) { Object.assign(existing, card); next.set(card.id, existing); }
    }
    this.cards = next;
    this.viewport = viewport;
    this.appearances = appearances;
    this.settling.top = viewport.clipTop; this.settling.bottom = viewport.clipBottom;
    const obstacles = [...next.values()];
    const groups = new Set(obstacles.map(c => c.group).filter((id): id is number => id !== null));
    for (const id of this.pools.keys()) if (!groups.has(id)) this.pools.delete(id);
    for (const id of groups) {
      const members = obstacles.filter(c => c.group === id);
      const membership = members.map(c => c.id).sort((a, b) => a - b).join(',');
      const bounds = {
        left: Math.min(...members.map(c => c.centreX - c.width / 2)) - 40,
        right: Math.max(...members.map(c => c.centreX + c.width / 2)) + 40,
        top: Math.min(...members.map(c => c.centreY - c.height / 2)) - 120,
        bottom: Math.max(...members.map(c => c.centreY + c.height / 2)) + 120
      };
      let pool = this.pools.get(id);
      if (!pool || pool.membership !== membership) {
        pool = { liquid: new Liquid(members, obstacles, bounds, false), membership, residency: new LiquidResidency() };
        this.pools.set(id, pool);
      } else if (geometryChanged) {
        transportParticles(pool.liquid, previous);
      }
      pool.liquid.cards = members;
      pool.liquid.obstacles = obstacles;
      pool.liquid.otherObstacles = obstacles.filter(c => c.group !== id);
      pool.liquid.bounds = bounds;
      pool.residency.update(pool.liquid, { left: viewport.left - 120, right: viewport.left + viewport.width + 120,
        top: viewport.clipTop - 120, bottom: viewport.clipBottom + 120 });
      if (geometryChanged || viewportChanged) this.settling.wake(pool.liquid, reason === 'scroll');
    }
  }

  get awake() { return [...this.pools.values()].some(pool => !this.settling.sleeping(pool.liquid)); }
  get activity(): SlickActivity[] {
    return this.appearances.filter(slick => this.pools.has(slick.id)).map(slick => {
      const liquid = this.pools.get(slick.id)!.liquid;
      return { ...slick, sleeping: this.settling.sleeping(liquid), limited: this.settling.limited(liquid) };
    });
  }
  get diagnostics() {
    return [...this.pools].map(([id, { liquid }]) => ({ id, steps: liquid.steps, particles: liquid.particles.length,
      sleeping: this.settling.sleeping(liquid), limited: this.settling.limited(liquid) }));
  }
  wake() {
    for (const { liquid } of this.pools.values()) this.settling.wake(liquid);
    this.accumulator = 0;
  }
  get simulationBackend() { return this.worker?.available ? 'worker' : 'main'; }
  pause() {
    this.simulationRunning = false;
    this.worker?.cancel();
    this.accumulator = 0;
  }
  dispose() { this.worker?.dispose(); }
  step(elapsed: number) {
    this.simulationRunning = true;
    for (const { liquid } of this.pools.values()) this.settling.advance(liquid, elapsed);
    this.accumulator = Math.min(this.accumulator + elapsed, 4 * DT);
    if (!this.worker && this.workerFactory) this.worker = new LiquidWorker(this.workerFactory, () => {
      if (this.simulationRunning && this.awake) this.ready();
    });
    if (this.worker?.available) {
      if (this.worker.busy) return;
      const steps = Math.floor(this.accumulator / DT);
      if (!steps) return;
      const pools = new Map([...this.pools].filter(([, pool]) => !this.settling.sleeping(pool.liquid))
        .map(([id, pool]) => [id, pool.liquid]));
      if (!pools.size) return;
      const layoutRevision = this.layoutRevision;
      const submitted = this.worker.submit(pools, [...this.cards.values()], steps, (id, liquid) =>
        this.layoutRevision === layoutRevision && this.pools.get(id)?.liquid === liquid && !this.settling.sleeping(liquid));
      if (submitted) {
        this.accumulator -= steps * DT;
        return;
      }
    }
    while (this.accumulator >= DT) {
      for (const { liquid } of this.pools.values()) if (!this.settling.sleeping(liquid)) liquid.step(liquidSettings);
      this.accumulator -= DT;
    }
  }
  draw(ctx: CanvasRenderingContext2D) {
    const viewport = this.viewport;
    ctx.clearRect(0, 0, viewport.width, viewport.height);
    ctx.save(); ctx.translate(-viewport.left, -viewport.top);
    ctx.beginPath(); ctx.rect(viewport.left, viewport.clipTop, viewport.width, Math.max(0, viewport.clipBottom - viewport.clipTop)); ctx.clip();
    const entries = this.appearances.flatMap(appearance => {
      const pool = this.pools.get(appearance.id);
      if (!pool) return [];
      return [{ id: appearance.id, liquid: pool.liquid, colour: appearance.colour,
        sleeping: this.settling.sleeping(pool.liquid), revision: this.surfaceRevision }];
    });
    const outlines = drawLiquids(ctx, entries, viewport);
    for (const [liquid, outline] of outlines) this.settling.observe(liquid, outline, liquid.steps * DT);
    ctx.restore();
  }
}

function transportParticles(liquid: Liquid, previous: Map<number, GooCard>) {
  const movements = liquid.cards.map(card => {
    const old = previous.get(card.id)!;
    return { old, dx: card.centreX - old.centreX, dy: card.centreY - old.centreY };
  });
  if (movements.every(m => m.dx === 0 && m.dy === 0)) return;
  // Move the existing liquid with its nearby cards, blending across bridges.
  // This also handles individual travelling cards and unequal column scrolls.
  for (const particle of liquid.particles) {
    let dx = 0, dy = 0, total = 0;
    for (const movement of movements) {
      const distance = Math.max(1, cardDistance(particle.x, particle.y, movement.old).distance);
      const weight = 1 / (distance * distance);
      dx += movement.dx * weight; dy += movement.dy * weight; total += weight;
    }
    dx /= total; dy /= total;
    particle.x += dx; particle.y += dy;
    if (particle.px !== undefined) particle.px += dx;
    if (particle.py !== undefined) particle.py += dy;
  }
}
