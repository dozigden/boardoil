import { describe, expect, it, vi } from 'vitest';
import { Liquid, liquidSettings } from './physics';
import { LiquidSimulation, packParticles, type SimulationRequest } from './liquidSimulation';
import { LiquidWorker, type SimulationWorker } from './liquidWorker';
import { GooScene } from './scene';
import type { GooCard } from './types';

const cards: GooCard[] = [{ id: 1, group: 1, column: 0, centreX: 150, centreY: 150, width: 160, height: 70, radius: 12 }];
const bounds = { left: 20, right: 300, top: 20, bottom: 300 };
const viewport = { left: 0, top: 0, width: 600, height: 400, clipTop: 0, clipBottom: 400 };
const appearances = [{ id: 1, name: 'One', colour: '#95d4aa' }];
function fakeWorker() {
  const requests: SimulationRequest[] = [];
  const simulation = new LiquidSimulation();
  const worker = {
    onmessage: null as SimulationWorker['onmessage'],
    onerror: null as SimulationWorker['onerror'],
    onmessageerror: null as SimulationWorker['onmessageerror'],
    terminate: vi.fn(),
    postMessage(message: unknown, options: Transferable[] | StructuredSerializeOptions = []) {
      const transfer = Array.isArray(options) ? options : options.transfer;
      requests.push(structuredClone(message, { transfer }) as SimulationRequest);
    }
  };
  function reply() {
    const result = simulation.run(requests.shift()!);
    worker.onmessage?.call(worker as unknown as Worker, { data: result } as MessageEvent);
    return result;
  }
  return { worker, requests, reply };
}

function setup() {
  const fake = fakeWorker(), ready = vi.fn();
  const worker = new LiquidWorker(() => fake.worker, ready);
  const liquid = new Liquid(structuredClone(cards), structuredClone(cards), bounds);
  liquid.otherObstacles = [];
  return { ...fake, client: worker, liquid, pools: new Map([[1, liquid]]), ready };
}

describe('Liquid worker simulation', () => {
  it('matches the existing fixed-step solver across successive transferred batches', () => {
    const { client, liquid, pools, reply } = setup();
    const original = new Liquid(cards, cards, bounds);
    for (let batch = 0; batch < 60; batch++) {
      const steps = batch % 4 + 1;
      client.submit(pools, cards, steps, () => true);
      for (let i = 0; i < steps; i++) original.step(liquidSettings);
      reply();
      expect(packParticles(liquid.particles)).toEqual(packParticles(original.particles));
      expect(liquid.steps).toBe(original.steps);
    }
  });

  it('limits the queue to one batch and applies displacement on top of scrolling', () => {
    const { client, liquid, pools, reply, requests, ready } = setup();
    const existing = [...liquid.particles];
    client.submit(pools, cards, 2, () => true);
    client.submit(pools, cards, 2, () => true);
    expect(requests).toHaveLength(1);
    for (const p of existing) { p.x += 40; p.y -= 80; }
    liquid.particles.shift();
    const fresh = { x: 50, y: 50, vx: 0, vy: 0 };
    liquid.particles.push(fresh);
    const removed = { ...existing[0] };
    const result = reply();
    for (let i = 1; i < existing.length; i++) {
      expect(existing[i].x).toBeCloseTo(result.pools[0].particles[i * 4] + 40, 10);
      expect(existing[i].y).toBeCloseTo(result.pools[0].particles[i * 4 + 1] - 80, 10);
    }
    expect(fresh).toEqual({ x: 50, y: 50, vx: 0, vy: 0 });
    expect(existing[0]).toEqual(removed);
    expect(client.busy).toBe(false);
    expect(ready).toHaveBeenCalledOnce();
  });

  it('ignores paused and invalidated batches, then resumes without a stale reply', () => {
    const { client, liquid, pools, reply } = setup();
    const before = packParticles(liquid.particles);
    client.submit(pools, cards, 2, () => true);
    client.cancel(); reply();
    expect(packParticles(liquid.particles)).toEqual(before);
    client.submit(pools, cards, 2, () => false); reply();
    expect(packParticles(liquid.particles)).toEqual(before);
    client.submit(pools, cards, 2, () => true); reply();
    expect(liquid.steps).toBe(2);
    expect(packParticles(liquid.particles)).not.toEqual(before);
  });

  it('rejects old layout and membership results but accepts results during continuous scrolling', () => {
    const fake = fakeWorker();
    const scene = new GooScene(() => fake.worker);
    let current = structuredClone(cards);
    scene.update(current, appearances, viewport);
    for (let i = 0; i < 6; i++) {
      scene.step(.02);
      const before = scene.diagnostics[0].steps;
      current = current.map(c => ({ ...c, centreY: c.centreY - 8 }));
      scene.update(current, appearances, viewport, 'scroll');
      fake.reply();
      expect(scene.diagnostics[0].steps).toBeGreaterThan(before);
    }
    scene.step(.02);
    const steps = scene.diagnostics[0].steps;
    current = current.map(c => ({ ...c, height: c.height + 30 }));
    scene.update(current, appearances, viewport);
    fake.reply();
    expect(scene.diagnostics[0].steps).toBe(steps);
    scene.step(.02);
    scene.update([{ ...current[0], id: 2 }], appearances, viewport);
    fake.reply();
    expect(scene.diagnostics[0].steps).toBe(0);
    scene.dispose();
    expect(fake.worker.terminate).toHaveBeenCalledOnce();
  });

  it('falls back to the UI solver after a worker error and releases the pending batch', () => {
    const fake = fakeWorker(), warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    try {
      const scene = new GooScene(() => fake.worker);
      scene.update(cards, appearances, viewport);
      scene.step(.02);
      expect(scene.simulationBackend).toBe('worker');
      fake.worker.onerror?.call(fake.worker as unknown as Worker, { preventDefault() {} } as ErrorEvent);
      scene.step(.02);
      expect(scene.simulationBackend).toBe('main');
      expect(scene.diagnostics[0].steps).toBeGreaterThan(0);
      expect(fake.worker.terminate).toHaveBeenCalledOnce();
      expect(warn).toHaveBeenCalledOnce();
    } finally { warn.mockRestore(); }
  });

  it.each(['unavailable', 'send failure'])('preserves the current simulation time on %s', failure => {
    const fake = fakeWorker(), warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    fake.worker.postMessage = () => { throw new Error('Cannot send'); };
    try {
      const scene = new GooScene(() => failure === 'unavailable' ? null : fake.worker);
      scene.update(cards, appearances, viewport);
      scene.step(.02);
      expect(scene.simulationBackend).toBe('main');
      expect(scene.diagnostics[0].steps).toBe(2);
    } finally { warn.mockRestore(); }
  });
});
