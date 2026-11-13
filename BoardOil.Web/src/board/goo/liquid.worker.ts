import { LiquidSimulation, type SimulationRequest } from './liquidSimulation';

const scope = self as unknown as Pick<Worker, 'onmessage' | 'postMessage'>;
const simulation = new LiquidSimulation();
scope.onmessage = (event: MessageEvent<SimulationRequest>) => {
  const result = simulation.run(event.data);
  scope.postMessage(result, result.pools.map(pool => pool.particles.buffer));
};
