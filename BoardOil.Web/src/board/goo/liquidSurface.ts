import { cardDistance, type Liquid } from './physics';
import { fieldsOverlap, liquidGridStep, resolveLiquidContacts, type LiquidField } from './liquidContacts';
import { DensitySmoother } from './liquidDensity';
import { LiquidCardSamples } from './liquidCardSamples';
import { traceContours } from './contours';
import type { Outline, Viewport } from './types';

const gridStep = liquidGridStep, renderRadius = 17, unassignedClearance = 6;
const densityPadding = renderRadius + 3 * gridStep;
type Surface = { loops: Outline; path: Path2D; field: LiquidField; rivals: LiquidField[] };
type CachedLiquid = {
  field: LiquidField; density: DensitySmoother; cards: LiquidCardSamples; surface?: Surface;
  steps: number; revision?: number;
};
// A revision tracks geometry changes outside the solver. Omit it when drawing
// manually edited particle snapshots that should always rebuild awake fields.
export type LiquidPaint = { id: number; liquid: Liquid; colour: string; sleeping: boolean; revision?: number };
const surfaces = new WeakMap<Liquid, CachedLiquid>();
function emptyField(): LiquidField {
  return { originX: 0, originY: 0, nx: 0, ny: 0, values: new Float32Array(), strength: new Float32Array() };
}
const clamp = (value: number, low: number, high: number) => Math.max(low, Math.min(high, value));

function buildField(liquid: Liquid, viewport: Viewport, density: DensitySmoother, samples: LiquidCardSamples): LiquidField {
  samples.begin();
  const viewLeft = Math.floor((viewport.left - 36) / gridStep) * gridStep;
  const viewTop = Math.floor((viewport.top - 36) / gridStep) * gridStep;
  const viewRight = viewLeft + (Math.ceil((viewport.width + 72) / gridStep)) * gridStep;
  const viewBottom = viewTop + (Math.ceil((viewport.height + 72) / gridStep)) * gridStep;
  const visible = (x: number, y: number, width: number, height: number) =>
    x + width / 2 >= viewLeft - renderRadius && x - width / 2 <= viewRight + renderRadius &&
    y + height / 2 >= viewTop - renderRadius && y - height / 2 <= viewBottom + renderRadius;
  const cards = liquid.cards.filter(c => visible(c.centreX, c.centreY, c.width, c.height));
  const particles = liquid.particles.filter(p => visible(p.x, p.y, 0, 0));
  if (!cards.length && !particles.length) { samples.end(); return emptyField(); }
  // Keep the same global grid phase and viewport boundary, but omit empty
  // space outside this slick. Padding includes every kernel and coating tail.
  const left = Math.min(...cards.map(c => c.centreX - c.width / 2 - 20), ...particles.map(p => p.x - densityPadding));
  const right = Math.max(...cards.map(c => c.centreX + c.width / 2 + 20), ...particles.map(p => p.x + densityPadding));
  const top = Math.min(...cards.map(c => c.centreY - c.height / 2 - 20), ...particles.map(p => p.y - densityPadding));
  const bottom = Math.max(...cards.map(c => c.centreY + c.height / 2 + 20), ...particles.map(p => p.y + densityPadding));
  const originX = Math.max(viewLeft, Math.floor(left / gridStep) * gridStep - gridStep);
  const originY = Math.max(viewTop, Math.floor(top / gridStep) * gridStep - gridStep);
  const nx = Math.round((Math.min(viewRight, Math.ceil(right / gridStep) * gridStep + gridStep) - originX) / gridStep) + 1;
  const ny = Math.round((Math.min(viewBottom, Math.ceil(bottom / gridStep) * gridStep + gridStep) - originY) / gridStep) + 1;
  if (nx < 3 || ny < 3) { samples.end(); return emptyField(); }
  const field = new Float32Array(nx * ny);
  const strength = new Float32Array(nx * ny);
  density.prepare(nx, ny);
  for (const p of particles) {
    if (p.y < originY - renderRadius || p.y > originY + ny * gridStep + renderRadius ||
        p.x < originX - renderRadius || p.x > originX + nx * gridStep + renderRadius) continue;
    const x0 = Math.max(1, Math.floor((p.x - renderRadius - originX) / gridStep));
    const x1 = Math.min(nx - 2, Math.ceil((p.x + renderRadius - originX) / gridStep));
    const y0 = Math.max(1, Math.floor((p.y - renderRadius - originY) / gridStep));
    const y1 = Math.min(ny - 2, Math.ceil((p.y + renderRadius - originY) / gridStep));
    density.include(x0, y0, x1, y1);
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const q = 1 - ((x * gridStep + originX - p.x) ** 2 + (y * gridStep + originY - p.y) ** 2) / renderRadius ** 2;
      if (q > 0) field[y * nx + x] += q * q * q * .7;
    }
  }
  density.smooth(field);
  // Keep individual card indents in the outline; only physics merges stacks.
  for (const card of cards) {
    const x0 = Math.max(1, Math.floor((card.centreX - card.width / 2 - 20 - originX) / gridStep));
    const x1 = Math.min(nx - 2, Math.ceil((card.centreX + card.width / 2 + 20 - originX) / gridStep));
    const y0 = Math.max(1, Math.floor((card.centreY - card.height / 2 - 20 - originY) / gridStep));
    const y1 = Math.min(ny - 2, Math.ceil((card.centreY + card.height / 2 + 20 - originY) / gridStep));
    if (x0 > x1 || y0 > y1) continue;
    const width = x1 - x0 + 1;
    const coatings = samples.get(card, x0 * gridStep + originX, y0 * gridStep + originY, width, y1 - y0 + 1, true);
    const radius = Math.min(card.radius, card.width / 2, card.height / 2);
    const innerLeft = Math.ceil((card.centreX - card.width / 2 + radius - originX) / gridStep);
    const innerRight = Math.floor((card.centreX + card.width / 2 - radius - originX) / gridStep);
    const innerTop = Math.ceil((card.centreY - card.height / 2 + radius - originY) / gridStep);
    const innerBottom = Math.floor((card.centreY + card.height / 2 - radius - originY) / gridStep);
    for (let y = y0; y <= y1; y++) {
      const interiorRow = y >= innerTop && y <= innerBottom;
      for (let x = x0; x <= x1; x++) {
        const i = y * nx + x;
        const sample = (y - y0) * width + x - x0;
        let coating = coatings[sample];
        if (Number.isNaN(coating)) {
          if (interiorRow && x >= innerLeft && x <= innerRight) coating = 1;
          else {
            const distance = cardDistance(x * gridStep + originX, y * gridStep + originY, card).distance;
            coating = clamp(.5 + (8 - distance) / 12, 0, 1);
          }
          coatings[sample] = coating;
        }
        if (!coating) continue;
        if (coating === 1 && field[i] === 0) {
          field[i] = 1; strength[i] = 1;
          continue;
        }
        const blend = Math.max(0, 1 - Math.abs(field[i] - coating) / .25);
        field[i] = Math.max(field[i], coating) + .25 * blend * blend / 4;
        strength[i] = Math.max(strength[i], coating);
      }
    }
  }
  // A card without a slick needs visible background around its edge; particle
  // collision alone leaves the wider rendered surface touching the card.
  const clearancePad = unassignedClearance + gridStep * 2;
  for (const card of liquid.otherObstacles) {
    if (card.group !== null) continue;
    const x0 = Math.max(1, Math.floor((card.centreX - card.width / 2 - clearancePad - originX) / gridStep));
    const x1 = Math.min(nx - 2, Math.ceil((card.centreX + card.width / 2 + clearancePad - originX) / gridStep));
    const y0 = Math.max(1, Math.floor((card.centreY - card.height / 2 - clearancePad - originY) / gridStep));
    const y1 = Math.min(ny - 2, Math.ceil((card.centreY + card.height / 2 + clearancePad - originY) / gridStep));
    if (x0 > x1 || y0 > y1) continue;
    const width = x1 - x0 + 1;
    const limits = samples.get(card, x0 * gridStep + originX, y0 * gridStep + originY, width, y1 - y0 + 1, false);
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const i = y * nx + x;
      if (field[i] <= 0) continue;
      const sample = (y - y0) * width + x - x0;
      let limit = limits[sample];
      if (Number.isNaN(limit)) {
        const distance = cardDistance(x * gridStep + originX, y * gridStep + originY, card).distance;
        limit = .5 + (distance - unassignedClearance) / 6;
        limits[sample] = limit;
      }
      const blend = Math.max(0, 1 - Math.abs(field[i] - limit) / .6);
      field[i] = Math.max(0, Math.min(field[i], limit) - .6 * blend * blend / 4);
    }
  }
  // Bound the particle contribution so a dense foreign pool cannot swallow a
  // card's dependable coating. Outside coatings, density decides the boundary.
  for (let i = 0; i < field.length; i++) strength[i] = field[i] / (1 + field[i]) + 2 * strength[i];
  samples.end();
  return { originX, originY, nx, ny, values: field, strength };
}

export function drawLiquids(ctx: CanvasRenderingContext2D, entries: LiquidPaint[], viewport: Viewport): Map<Liquid, Outline> {
  const fields = entries.map(entry => {
    let cached = surfaces.get(entry.liquid);
    if (!cached) {
      const density = new DensitySmoother();
      const cards = new LiquidCardSamples();
      cached = { field: buildField(entry.liquid, viewport, density, cards), density, cards,
        steps: entry.liquid.steps, revision: entry.revision };
      surfaces.set(entry.liquid, cached);
    } else if (!entry.sleeping && (entry.revision === undefined || entry.revision !== cached.revision || entry.liquid.steps !== cached.steps)) {
      cached.field = buildField(entry.liquid, viewport, cached.density, cached.cards);
      cached.steps = entry.liquid.steps; cached.revision = entry.revision;
    }
    return { ...entry, cached, field: cached.field };
  });
  const rendered = fields.map(own => {
    const rivals = fields.filter(other => other !== own && fieldsOverlap(own.field, other.field));
    const previous = own.cached.surface;
    let surface = previous;
    if (!surface || surface.field !== own.field || surface.rivals.length !== rivals.length ||
        rivals.some((rival, i) => surface!.rivals[i] !== rival.field)) {
      const field = own.field;
      const values = resolveLiquidContacts(own, rivals);
      const loops = traceContours(values, field.nx, field.ny, field.originX, field.originY, gridStep);
      const path = new Path2D();
      for (const loop of loops) {
        path.moveTo(...loop[0]);
        for (let i = 1; i < loop.length; i++) path.lineTo(...loop[i]);
        path.closePath();
      }
      surface = { loops, path, field, rivals: rivals.map(r => r.field) };
      own.cached.surface = surface;
    }
    return { ...own, surface };
  });
  // Paint seam underlays first, then the mutually exclusive fills. This avoids
  // pale antialiasing cracks without drawing a coloured rim over a neighbour.
  for (const { colour, surface } of rendered) {
    ctx.strokeStyle = colour; ctx.lineWidth = .65; ctx.stroke(surface.path);
  }
  for (const { colour, surface } of rendered) {
    ctx.fillStyle = colour; ctx.fill(surface.path, 'evenodd');
  }
  return new Map(rendered.map(({ liquid, surface }) => [liquid, surface.loops]));
}

export function drawLiquid(ctx: CanvasRenderingContext2D, liquid: Liquid, viewport: Viewport, colour: string, sleeping: boolean): Outline {
  return drawLiquids(ctx, [{ id: liquid.cards[0]?.group ?? 0, liquid, colour, sleeping }], viewport).get(liquid)!;
}
