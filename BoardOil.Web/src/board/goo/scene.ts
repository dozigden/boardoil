import { cardDistance, DT, Liquid, liquidSettings } from './physics';
import { Settling } from './settling';
import { LiquidResidency } from './liquidResidency';
import { drawLiquids } from './liquidSurface';
import { buildOrganicPath } from './smooth';
import type { ExperimentalGooMode, GooCard, Viewport } from './types';

export type SlickAppearance = { id: number; name: string; colour: string };
export type SlickActivity = SlickAppearance & { sleeping: boolean; limited: boolean };
type Pool = { liquid: Liquid; membership: string; residency: LiquidResidency };

export class GooScene {
  private smoothPaths = new Map<number, { signature: string; path: Path2D }>();
  private cards = new Map<number, GooCard>();
  private pools = new Map<number, Pool>();
  private settling = new Settling(0, 0);
  private appearances: SlickAppearance[] = [];
  private accumulator = 0;
  private viewport: Viewport = { left: 0, top: 0, width: 0, height: 0, clipTop: 0, clipBottom: 0 };

  update(nextCards: GooCard[], appearances: SlickAppearance[], viewport: Viewport) {
    const previous = new Map([...this.cards].map(([id, card]) => [id, { ...card }]));
    const next = new Map(nextCards.map(card => [card.id, card]));
    const geometryChanged = next.size !== previous.size || nextCards.some(card => {
      const old = previous.get(card.id);
      return !old || Object.keys(card).some(key => card[key as keyof GooCard] !== old[key as keyof GooCard]);
    });
    const viewportChanged = Object.keys(viewport).some(key => viewport[key as keyof Viewport] !== this.viewport[key as keyof Viewport]);
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
    for (const id of this.pools.keys()) if (!groups.has(id)) { this.pools.delete(id); this.smoothPaths.delete(id); }
    for (const id of groups) {
      const members = obstacles.filter(c => c.group === id);
      const membership = members.map(c => c.id).sort((a, b) => a - b).join(',');
      const bounds = {
        left: Math.min(...members.map(c => c.x - c.width / 2)) - 40,
        right: Math.max(...members.map(c => c.x + c.width / 2)) + 40,
        top: Math.min(...members.map(c => c.y - c.height / 2)) - 120,
        bottom: Math.max(...members.map(c => c.y + c.height / 2)) + 120
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
      if (geometryChanged || viewportChanged) this.settling.wake(pool.liquid);
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
  step(elapsed: number) {
    for (const { liquid } of this.pools.values()) this.settling.advance(liquid, elapsed);
    this.accumulator = Math.min(this.accumulator + elapsed, 4 * DT);
    while (this.accumulator >= DT) {
      for (const { liquid } of this.pools.values()) if (!this.settling.sleeping(liquid)) liquid.step(liquidSettings);
      this.accumulator -= DT;
    }
  }
  draw(ctx: CanvasRenderingContext2D, mode: ExperimentalGooMode, reducedMotion: boolean) {
    const viewport = this.viewport;
    ctx.clearRect(0, 0, viewport.width, viewport.height);
    ctx.save(); ctx.translate(-viewport.left, -viewport.top);
    ctx.beginPath(); ctx.rect(viewport.left, viewport.clipTop, viewport.width, Math.max(0, viewport.clipBottom - viewport.clipTop)); ctx.clip();
    if (mode === 'liquid' && !reducedMotion) {
      const entries = this.appearances.flatMap(appearance => {
        const pool = this.pools.get(appearance.id);
        if (!pool) return [];
        return [{ id: appearance.id, liquid: pool.liquid, colour: appearance.colour, sleeping: this.settling.sleeping(pool.liquid) }];
      });
      const outlines = drawLiquids(ctx, entries, viewport);
      for (const [liquid, outline] of outlines) this.settling.observe(liquid, outline, liquid.steps * DT);
      ctx.restore();
      return;
    }
    for (const appearance of this.appearances) {
      const pool = this.pools.get(appearance.id);
      if (!pool) continue;
      const liquid = pool.liquid;
      if (mode === 'smooth' || reducedMotion) {
        const rects = liquid.cards.filter(card => card.x + card.width / 2 >= viewport.left - 168 &&
          card.x - card.width / 2 <= viewport.left + viewport.width + 168 &&
          card.y + card.height / 2 >= viewport.clipTop - 168 && card.y - card.height / 2 <= viewport.clipBottom + 168)
          .map(card => ({ x: card.x - card.width / 2 - 14, y: card.y - card.height / 2 - 14,
            width: card.width + 28, height: card.height + 28, column: card.column }));
        if (rects.length) {
          const signature = JSON.stringify([rects, viewport]);
          let cached = this.smoothPaths.get(appearance.id);
          if (cached?.signature !== signature) {
            cached = { signature, path: buildOrganicPath(rects, viewport) };
            this.smoothPaths.set(appearance.id, cached);
          }
          ctx.fillStyle = appearance.colour;
          ctx.fill(cached.path, 'evenodd');
        }
      }
    }
    ctx.restore();
  }
}

function transportParticles(liquid: Liquid, previous: Map<number, GooCard>) {
  const movements = liquid.cards.map(card => {
    const old = previous.get(card.id)!;
    return { old, dx: card.x - old.x, dy: card.y - old.y };
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
