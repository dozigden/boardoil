import { Liquid, liquidSettings } from './physics';
import type { Bounds, GooCard, Particle } from './types';

export type SimulationPool = {
  id: number;
  steps: number;
  bounds: Bounds;
  activeBand: Bounds;
  particles: Float64Array;
};
export type SimulationRequest = { id: number; steps: number; cards: GooCard[]; pools: SimulationPool[] };
export type SimulationResult = { id: number; pools: { id: number; steps: number; particles: Float64Array }[] };

export function packParticles(particles: Particle[]): Float64Array {
  const packed = new Float64Array(particles.length * 4);
  for (let i = 0; i < particles.length; i++) {
    const p = particles[i], offset = i * 4;
    packed[offset] = p.x; packed[offset + 1] = p.y;
    packed[offset + 2] = p.vx; packed[offset + 3] = p.vy;
  }
  return packed;
}

export class LiquidSimulation {
  private pools = new Map<number, Liquid>();

  run(request: SimulationRequest): SimulationResult {
    const ids = new Set(request.pools.map(pool => pool.id));
    for (const id of this.pools.keys()) if (!ids.has(id)) this.pools.delete(id);
    const pools = request.pools.map(input => {
      const cards = request.cards.filter(card => card.group === input.id);
      let liquid = this.pools.get(input.id);
      if (!liquid) {
        liquid = new Liquid(cards, request.cards, input.bounds, false);
        this.pools.set(input.id, liquid);
      }
      liquid.cards = cards;
      liquid.obstacles = request.cards;
      liquid.otherObstacles = request.cards.filter(card => card.group !== input.id);
      liquid.bounds = input.bounds;
      liquid.activeBand = input.activeBand;
      liquid.steps = input.steps;
      const packed = input.particles;
      const count = packed.length / 4;
      liquid.particles.length = count;
      for (let i = 0; i < count; i++) {
        const offset = i * 4;
        const p = liquid.particles[i] ?? { x: 0, y: 0, vx: 0, vy: 0 };
        p.x = packed[offset]; p.y = packed[offset + 1];
        p.vx = packed[offset + 2]; p.vy = packed[offset + 3];
        liquid.particles[i] = p;
      }
      for (let n = 0; n < request.steps; n++) liquid.step(liquidSettings);
      for (let i = 0; i < count; i++) {
        const p = liquid.particles[i], offset = i * 4;
        packed[offset] = p.x; packed[offset + 1] = p.y;
        packed[offset + 2] = p.vx; packed[offset + 3] = p.vy;
      }
      return { id: input.id, steps: liquid.steps, particles: packed };
    });
    return { id: request.id, pools };
  }
}
