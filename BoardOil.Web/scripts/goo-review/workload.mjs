import { scenes, stages } from './scenes.mjs';

// Numerical review only: no browser rasterisation or GPU work is simulated.
export class NumericalPath {
  moveTo() {}
  lineTo() {}
  closePath() {}
}
const context = { fill() {}, stroke() {} };

export function runWorkload(api, record = () => {}, measure = (_name, work) => work()) {
  for (const scene of scenes) {
    const cards = structuredClone(scene.cards);
    const groups = [...new Set(cards.map(card => card.group).filter(group => group !== null))];
    const appearances = groups.map(id => ({ id, name: `Pool ${id}`, colour: '#95d4aa' }));
    const bridges = new api.BridgeLayers();
    const pools = groups.map(id => {
      const members = cards.filter(card => card.group === id);
      const bounds = {
        left: Math.min(...members.map(c => c.centreX - c.width / 2)) - 40,
        right: Math.max(...members.map(c => c.centreX + c.width / 2)) + 40,
        top: Math.min(...members.map(c => c.centreY - c.height / 2)) - 120,
        bottom: Math.max(...members.map(c => c.centreY + c.height / 2)) + 120
      };
      const initial = measure('initial seed', () => new api.Liquid(members, cards, bounds));
      record(`${scene.name}/initial-fill/${id}`, initial.particles);
      return { id, liquid: new api.Liquid(members, cards, bounds, false), residency: new api.LiquidResidency() };
    });
    for (const stage of stages) {
      const key = `${scene.name}/${stage.name}`;
      const viewport = { left: stage.left, top: stage.top, width: stage.width, height: stage.height,
        clipTop: stage.top + 56, clipBottom: stage.top + stage.height };
      const active = { left: stage.left - 120, right: stage.left + stage.width + 120,
        top: stage.top - 120, bottom: stage.top + stage.height + 120 };
      for (const { id, liquid, residency } of pools) {
        measure('residency', () => residency.update(liquid, active));
        measure('physics', () => {
          for (let i = 0; i < stage.steps; i++) liquid.step(api.liquidSettings);
        });
        record(`${key}/particles/${id}`, liquid.particles);
        const field = measure('raw field', () => api.buildField(liquid, viewport, new api.DensitySmoother(), new api.LiquidCardSamples()));
        record(`${key}/field/${id}`, field);
      }
      const entries = pools.map(({ id, liquid }) => ({ id, liquid, colour: '#95d4aa', sleeping: false, revision: stages.indexOf(stage) }));
      const outlines = measure('liquid outlines', () => api.drawLiquids(context, entries, viewport));
      record(`${key}/liquid`, [...outlines.values()]);
      const cached = measure('cached liquid outlines', () => api.drawLiquids(context, entries, viewport));
      record(`${key}/liquid-cached`, [...cached.values()]);
      for (const id of groups) {
        const surfaces = cards.filter(card => card.group === id).map(api.cardSurface);
        const necks = api.buildNecks(surfaces);
        record(`${key}/bridges/${id}`, necks);
        const outline = measure('bridge outlines', () => api.blendedOutline(surfaces, necks, viewport));
        record(`${key}/bridge-outlines/${id}`, outline);
      }
      for (const column of new Set(cards.map(card => card.column))) {
        const local = cards.filter(card => card.column === column);
        const outlines = measure('column SVG', () => bridges.columnOutlines(column, local, appearances));
        record(`${key}/column/${column}`, outlines);
      }
    }
  }
}
