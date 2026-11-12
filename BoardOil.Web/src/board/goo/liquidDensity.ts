// A separable kernel softens particle-scale ripples before the card coating,
// clearance and shared contact boundaries are applied (sigma about 3.7px).
export function smoothParticleDensity(field: Float32Array, nx: number, ny: number) {
  const horizontal = new Float32Array(field.length);
  for (let y = 0; y < ny; y++) for (let x = 3; x < nx - 3; x++) {
    const i = y * nx + x;
    horizontal[i] = (field[i - 3] + 6 * field[i - 2] + 15 * field[i - 1] + 20 * field[i] +
      15 * field[i + 1] + 6 * field[i + 2] + field[i + 3]) / 64;
  }
  field.fill(0);
  for (let y = 3; y < ny - 3; y++) for (let x = 3; x < nx - 3; x++) {
    const i = y * nx + x;
    field[i] = (horizontal[i - 3 * nx] + 6 * horizontal[i - 2 * nx] + 15 * horizontal[i - nx] + 20 * horizontal[i] +
      15 * horizontal[i + nx] + 6 * horizontal[i + 2 * nx] + horizontal[i + 3 * nx]) / 64;
  }
}
