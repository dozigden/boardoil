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

export function resolveLiquidContacts(own: CompetingField, rivals: CompetingField[]): Float32Array {
  if (!rivals.length) return own.field.values;
  const field = own.field, result = field.values.slice();
  for (const rival of rivals) {
    const other = rival.field;
    const dx = Math.round((other.originX - field.originX) / liquidGridStep);
    const dy = Math.round((other.originY - field.originY) / liquidGridStep);
    const x0 = Math.max(0, dx), x1 = Math.min(field.nx, dx + other.nx);
    const y0 = Math.max(0, dy), y1 = Math.min(field.ny, dy + other.ny);
    for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
      const i = y * field.nx + x, j = (y - dy) * other.nx + x - dx;
      if (other.values[j] < .5) continue;
      let advantage = field.strength[i] - other.strength[j];
      // Coincident equal fields need a stable owner, independent of paint order.
      if (advantage === 0) advantage = own.id < rival.id ? 1e-6 : -1e-6;
      // A point belongs here when this liquid exists AND either the rival is
      // absent or this liquid has the stronger claim. Opposite advantages give
      // both contours the same interpolated contact edge on the shared grid.
      result[i] = Math.min(result[i], .5 + Math.max(.5 - other.values[j], advantage));
    }
  }
  return result;
}
