import type { Liquid } from './physics';
import { packParticles, type SimulationRequest, type SimulationResult } from './liquidSimulation';
import type { GooCard, Particle } from './types';

export type SimulationWorker = Pick<Worker, 'onmessage' | 'onerror' | 'onmessageerror' | 'postMessage' | 'terminate'>;
export function createLiquidWorker(): SimulationWorker | null {
  if (typeof Worker === 'undefined') return null;
  return new Worker(new URL('./liquid.worker.ts', import.meta.url), { type: 'module' });
}
type Snapshot = { liquid: Liquid; particles: Particle[]; positions: Float64Array };
type Pending = {
  id: number;
  generation: number;
  pools: Map<number, Snapshot>;
  accept: (id: number, liquid: Liquid) => boolean;
};

export class LiquidWorker {
  private worker: SimulationWorker | null = null;
  private pending: Pending | null = null;
  private generation = 0;
  private sequence = 0;

  constructor(factory: () => SimulationWorker | null, private ready: () => void) {
    try {
      this.worker = factory();
      if (this.worker) {
        this.worker.onmessage = event => this.receive(event.data as SimulationResult);
        this.worker.onerror = event => { event.preventDefault(); this.fail(); };
        this.worker.onmessageerror = () => this.fail();
      }
    } catch { this.fail(); }
  }
  get available() { return this.worker !== null; }
  get busy() { return this.pending !== null; }

  submit(pools: Map<number, Liquid>, cards: GooCard[], steps: number, accept: Pending['accept']) {
    if (!this.worker || this.pending) return false;
    const snapshots = new Map<number, Snapshot>();
    const request: SimulationRequest = { id: ++this.sequence, steps, cards, pools: [] };
    for (const [id, liquid] of pools) {
      const particles = [...liquid.particles];
      const positions = new Float64Array(particles.length * 2);
      particles.forEach((p, i) => { positions[i * 2] = p.x; positions[i * 2 + 1] = p.y; });
      snapshots.set(id, { liquid, particles, positions });
      request.pools.push({ id, steps: liquid.steps, bounds: liquid.bounds, activeBand: liquid.activeBand,
        particles: packParticles(particles) });
    }
    this.pending = { id: request.id, generation: this.generation, pools: snapshots, accept };
    try {
      this.worker.postMessage(request, request.pools.map(pool => pool.particles.buffer));
      return true;
    } catch {
      this.fail();
      return false;
    }
  }

  private receive(result: SimulationResult) {
    const pending = this.pending;
    if (!pending || pending.id !== result.id) return;
    this.pending = null;
    if (pending.generation === this.generation) for (const output of result.pools) {
      const snapshot = pending.pools.get(output.id);
      if (!snapshot || !pending.accept(output.id, snapshot.liquid)) continue;
      const { liquid, particles, positions } = snapshot;
      const retained = new Set(liquid.particles);
      for (let i = 0; i < particles.length; i++) {
        const p = particles[i];
        if (!retained.has(p)) continue;
        // Apply only simulated displacement: scrolling may already have moved
        // this particle since the snapshot, and must never be undone by a reply.
        p.x = output.particles[i * 4] + (p.x - positions[i * 2]);
        p.y = output.particles[i * 4 + 1] + (p.y - positions[i * 2 + 1]);
        p.vx = output.particles[i * 4 + 2]; p.vy = output.particles[i * 4 + 3];
      }
      liquid.steps = output.steps;
      liquid.cullParticles();
    }
    this.ready();
  }
  cancel() { this.generation++; }
  dispose() {
    this.cancel();
    if (this.worker) {
      this.worker.onmessage = null; this.worker.onerror = null; this.worker.onmessageerror = null;
      this.worker.terminate();
    }
    this.worker = null; this.pending = null;
  }
  private fail() {
    this.dispose();
    console.warn('Liquid physics worker unavailable; continuing on the UI thread.');
    this.ready();
  }
}
