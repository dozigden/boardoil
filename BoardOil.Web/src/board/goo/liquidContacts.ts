export const liquidGridStep = 3;
export type LiquidField = {
  originX: number;
  originY: number;
  nx: number;
  ny: number;
  values: Float32Array;
  strength: Float32Array;
};
export type CompetingField = { id: number; field: LiquidField };

export function fieldsOverlap(a: LiquidField, b: LiquidField) {
  return a.nx > 0 && b.nx > 0 &&
    a.originX < b.originX + (b.nx - 1) * liquidGridStep && b.originX < a.originX + (a.nx - 1) * liquidGridStep &&
    a.originY < b.originY + (b.ny - 1) * liquidGridStep && b.originY < a.originY + (a.ny - 1) * liquidGridStep;
}

type Contact = {
  dx: number;
  dy: number;
  x0: number;
  y0: number;
  width: number;
  height: number;
  advantage: Float32Array;
};
const contacts = new WeakMap<LiquidField, WeakMap<LiquidField, Contact>>();

function tensionContact(a: LiquidField, b: LiquidField): Contact {
  let neighbours = contacts.get(a);
  if (!neighbours) {
    neighbours = new WeakMap();
    contacts.set(a, neighbours);
  }
  const cached = neighbours.get(b);
  if (cached) return cached;
  const dx = Math.round((b.originX - a.originX) / liquidGridStep);
  const dy = Math.round((b.originY - a.originY) / liquidGridStep);
  const left = Math.max(0, dx), right = Math.min(a.nx, dx + b.nx);
  const top = Math.max(0, dy), bottom = Math.min(a.ny, dy + b.ny);
  let x0 = right, x1 = left, y0 = bottom, y1 = top;
  for (let y = top; y < bottom; y++) for (let x = left; x < right; x++) {
    if (a.values[y * a.nx + x] < .5 || b.values[(y - dy) * b.nx + x - dx] < .5) continue;
    x0 = Math.min(x0, x); x1 = Math.max(x1, x + 1);
    y0 = Math.min(y0, y); y1 = Math.max(y1, y + 1);
  }
  const width = Math.max(0, x1 - x0), height = Math.max(0, y1 - y0);
  let advantage = new Float32Array(width * height), next = new Float32Array(width * height);
  const mask = new Uint8Array(width * height);
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const i = (y + y0) * a.nx + x + x0, j = (y + y0 - dy) * b.nx + x + x0 - dx;
    const k = y * width + x;
    if (a.values[i] < .5 || b.values[j] < .5) continue;
    // Dense particles cannot pin a boundary: only the stronger card coating can.
    mask[k] = Math.max(a.strength[i], b.strength[j]) > 1.25 ? 2 : 1;
    advantage[k] = Math.max(-.25, Math.min(.25, a.strength[i] - b.strength[j]));
  }
  // Precompute a sparse stencil: most of the intersecting rectangle can be
  // empty or covered by cards, and needs no work during relaxation.
  const stencil: number[] = [];
  for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
    const i = y * width + x;
    if (mask[i] !== 1) continue;
    stencil.push(i,
      x > 0 && mask[i - 1] ? i - 1 : i,
      x + 1 < width && mask[i + 1] ? i + 1 : i,
      y > 0 && mask[i - width] ? i - width : i,
      y + 1 < height && mask[i + width] ? i + width : i);
  }
  next.set(advantage);
  // Relax only the shared interface. Empty space is excluded, so this cannot
  // close the larger pockets or expand a slick's original outer silhouette.
  for (let pass = 0; pass < 8; pass++) {
    for (let k = 0; k < stencil.length; k += 5) {
      const i = stencil[k];
      next[i] = (advantage[i] * 4 + advantage[stencil[k + 1]] + advantage[stencil[k + 2]] +
        advantage[stencil[k + 3]] + advantage[stencil[k + 4]]) / 8;
    }
    const swap = advantage; advantage = next; next = swap;
  }
  const contact = { dx, dy, x0, y0, width, height, advantage };
  neighbours.set(b, contact);
  return contact;
}

export function resolveLiquidContacts(own: CompetingField, rivals: CompetingField[]): Float32Array {
  if (!rivals.length) return own.field.values;
  const field = own.field, result = field.values.slice();
  for (const rival of rivals) {
    // Canonical order gives both colours exactly the same relaxed boundary,
    // computed once and reused while both fields are asleep.
    const first = own.id < rival.id;
    const a = first ? field : rival.field, b = first ? rival.field : field;
    const contact = tensionContact(a, b);
    for (let y = 0; y < contact.height; y++) for (let x = 0; x < contact.width; x++) {
      const ai = (y + contact.y0) * a.nx + x + contact.x0;
      const bi = (y + contact.y0 - contact.dy) * b.nx + x + contact.x0 - contact.dx;
      if (a.values[ai] < .5 || b.values[bi] < .5) continue;
      const i = first ? ai : bi;
      let advantage = contact.advantage[y * contact.width + x];
      if (!first) advantage = -advantage;
      if (advantage === 0) advantage = first ? 1e-6 : -1e-6;
      result[i] = Math.min(result[i], .5 + advantage);
    }
  }
  return result;
}
