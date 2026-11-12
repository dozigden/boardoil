import { cardDistance, type Liquid } from './physics';
import { traceContours } from './contours';
import type { Outline, Viewport } from './types';

const gridStep = 3, renderRadius = 17;
type Surface = { loops: Outline; path: Path2D };
const surfaces = new WeakMap<Liquid, Surface>();
const clamp = (value: number, low: number, high: number) => Math.max(low, Math.min(high, value));

function buildSurface(liquid: Liquid, viewport: Viewport): Surface {
  const viewLeft = Math.floor((viewport.left - 36) / gridStep) * gridStep;
  const viewTop = Math.floor((viewport.top - 36) / gridStep) * gridStep;
  const viewRight = viewLeft + (Math.ceil((viewport.width + 72) / gridStep)) * gridStep;
  const viewBottom = viewTop + (Math.ceil((viewport.height + 72) / gridStep)) * gridStep;
  const visible = (x: number, y: number, width: number, height: number) =>
    x + width / 2 >= viewLeft - renderRadius && x - width / 2 <= viewRight + renderRadius &&
    y + height / 2 >= viewTop - renderRadius && y - height / 2 <= viewBottom + renderRadius;
  const cards = liquid.cards.filter(c => visible(c.x, c.y, c.width, c.height));
  const particles = liquid.particles.filter(p => visible(p.x, p.y, 0, 0));
  if (!cards.length && !particles.length) return { loops: [], path: new Path2D() };
  // Keep the same global grid phase and viewport boundary, but omit empty
  // space outside this slick. Padding includes every kernel and coating tail.
  const left = Math.min(...cards.map(c => c.x - c.width / 2 - 20), ...particles.map(p => p.x - renderRadius));
  const right = Math.max(...cards.map(c => c.x + c.width / 2 + 20), ...particles.map(p => p.x + renderRadius));
  const top = Math.min(...cards.map(c => c.y - c.height / 2 - 20), ...particles.map(p => p.y - renderRadius));
  const bottom = Math.max(...cards.map(c => c.y + c.height / 2 + 20), ...particles.map(p => p.y + renderRadius));
  const originX = Math.max(viewLeft, Math.floor(left / gridStep) * gridStep - gridStep);
  const originY = Math.max(viewTop, Math.floor(top / gridStep) * gridStep - gridStep);
  const nx = Math.round((Math.min(viewRight, Math.ceil(right / gridStep) * gridStep + gridStep) - originX) / gridStep) + 1;
  const ny = Math.round((Math.min(viewBottom, Math.ceil(bottom / gridStep) * gridStep + gridStep) - originY) / gridStep) + 1;
  if (nx < 3 || ny < 3) return { loops: [], path: new Path2D() };
  const field = new Float32Array(nx * ny);
  for (const p of particles) {
    if (p.y < originY - renderRadius || p.y > originY + ny * gridStep + renderRadius ||
        p.x < originX - renderRadius || p.x > originX + nx * gridStep + renderRadius) continue;
    const x0 = Math.max(1, Math.floor((p.x - renderRadius - originX) / gridStep));
    const x1 = Math.min(nx - 2, Math.ceil((p.x + renderRadius - originX) / gridStep));
    const y0 = Math.max(1, Math.floor((p.y - renderRadius - originY) / gridStep));
    const y1 = Math.min(ny - 2, Math.ceil((p.y + renderRadius - originY) / gridStep));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const q = 1 - ((x * gridStep + originX - p.x) ** 2 + (y * gridStep + originY - p.y) ** 2) / renderRadius ** 2;
      if (q > 0) field[y * nx + x] += q * q * q * .7;
    }
  }
  // Keep individual card indents in the outline; only physics merges stacks.
  for (const card of cards) {
    const x0 = Math.max(1, Math.floor((card.x - card.width / 2 - 20 - originX) / gridStep));
    const x1 = Math.min(nx - 2, Math.ceil((card.x + card.width / 2 + 20 - originX) / gridStep));
    const y0 = Math.max(1, Math.floor((card.y - card.height / 2 - 20 - originY) / gridStep));
    const y1 = Math.min(ny - 2, Math.ceil((card.y + card.height / 2 + 20 - originY) / gridStep));
    for (let y = y0; y <= y1; y++) for (let x = x0; x <= x1; x++) {
      const distance = cardDistance(x * gridStep + originX, y * gridStep + originY, card).distance;
      const coating = clamp(.5 + (8 - distance) / 12, 0, 1);
      if (!coating) continue;
      const i = y * nx + x, blend = Math.max(0, 1 - Math.abs(field[i] - coating) / .25);
      field[i] = Math.max(field[i], coating) + .25 * blend * blend / 4;
    }
  }
  const loops = traceContours(field, nx, ny, originX, originY, gridStep);
  const path = new Path2D();
  for (const loop of loops) {
    path.moveTo(...loop[0]);
    for (let i = 1; i < loop.length; i++) path.lineTo(...loop[i]);
    path.closePath();
  }
  return { loops, path };
}

export function drawLiquid(ctx: CanvasRenderingContext2D, liquid: Liquid, viewport: Viewport, colour: string, sleeping: boolean): Outline {
  let surface = surfaces.get(liquid);
  if (!sleeping || !surface) {
    surface = buildSurface(liquid, viewport);
    surfaces.set(liquid, surface);
  }
  ctx.fillStyle = colour;
  ctx.fill(surface.path, 'evenodd');
  ctx.strokeStyle = colour;
  ctx.lineWidth = .65;
  ctx.stroke(surface.path);
  return surface.loops;
}
