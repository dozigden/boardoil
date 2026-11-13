const tileSize = 8, kernelRadius = 3;

// A separable kernel softens particle-scale ripples before the card coating,
// clearance and shared contact boundaries are applied (sigma about 3.7px).
export class DensitySmoother {
  private horizontal = new Float32Array(0);
  private tiles = new Uint8Array(0);
  private nx = 0;
  private ny = 0;
  private columns = 0;
  private rows = 0;

  prepare(nx: number, ny: number) {
    this.nx = nx; this.ny = ny;
    this.columns = Math.ceil(nx / tileSize); this.rows = Math.ceil(ny / tileSize);
    if (this.horizontal.length < nx * ny) this.horizontal = new Float32Array(nx * ny);
    else this.horizontal.fill(0, 0, nx * ny);
    const count = this.columns * this.rows;
    if (this.tiles.length < count) this.tiles = new Uint8Array(count);
    else this.tiles.fill(0, 0, count);
  }

  include(x0: number, y0: number, x1: number, y1: number) {
    // Both passes need the full kernel halo, including across tile boundaries.
    const left = Math.max(0, Math.floor((x0 - kernelRadius) / tileSize));
    const right = Math.min(this.columns - 1, Math.floor((x1 + kernelRadius) / tileSize));
    const top = Math.max(0, Math.floor((y0 - kernelRadius) / tileSize));
    const bottom = Math.min(this.rows - 1, Math.floor((y1 + kernelRadius) / tileSize));
    for (let y = top; y <= bottom; y++) this.tiles.fill(1, y * this.columns + left, y * this.columns + right + 1);
  }

  smooth(field: Float32Array) {
    const { nx, ny, columns, rows, tiles, horizontal } = this;
    for (let ty = 0; ty < rows; ty++) for (let tx = 0; tx < columns; tx++) {
      if (!tiles[ty * columns + tx]) continue;
      const x0 = Math.max(3, tx * tileSize), x1 = Math.min(nx - 3, (tx + 1) * tileSize);
      const y1 = Math.min(ny, (ty + 1) * tileSize);
      for (let y = ty * tileSize; y < y1; y++) for (let x = x0; x < x1; x++) {
        const i = y * nx + x;
        horizontal[i] = (field[i - 3] + 6 * field[i - 2] + 15 * field[i - 1] + 20 * field[i] +
          15 * field[i + 1] + 6 * field[i + 2] + field[i + 3]) / 64;
      }
    }
    field.fill(0);
    for (let ty = 0; ty < rows; ty++) for (let tx = 0; tx < columns; tx++) {
      if (!tiles[ty * columns + tx]) continue;
      const x0 = Math.max(3, tx * tileSize), x1 = Math.min(nx - 3, (tx + 1) * tileSize);
      const y0 = Math.max(3, ty * tileSize), y1 = Math.min(ny - 3, (ty + 1) * tileSize);
      for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
        const i = y * nx + x;
        field[i] = (horizontal[i - 3 * nx] + 6 * horizontal[i - 2 * nx] + 15 * horizontal[i - nx] + 20 * horizontal[i] +
          15 * horizontal[i + nx] + 6 * horizontal[i + 2 * nx] + horizontal[i + 3 * nx]) / 64;
      }
    }
  }
}
