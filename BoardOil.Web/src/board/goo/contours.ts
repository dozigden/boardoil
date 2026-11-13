import type { Outline, Point } from './types';

class ContourTracer {
  private edgeNodes = new Uint32Array(0);
  private coordinates = new Float64Array(0);
  private neighbours = new Uint32Array(0);
  private visited = new Uint8Array(0);
  private starts: number[] = [];
  private crossings = new Uint32Array(4);
  private count = 0;

  private grow() {
    const capacity = Math.max(128, this.visited.length * 2);
    const coordinates = new Float64Array(capacity * 2);
    const neighbours = new Uint32Array(capacity * 2);
    const visited = new Uint8Array(capacity);
    coordinates.set(this.coordinates); neighbours.set(this.neighbours); visited.set(this.visited);
    this.coordinates = coordinates; this.neighbours = neighbours; this.visited = visited;
  }

  private edge(id: number, x0: number, y0: number, x1: number, y1: number,
    v0: number, v1: number, originX: number, originY: number, step: number) {
    const existing = this.edgeNodes[id];
    if (existing) return existing;
    const node = ++this.count;
    if (node >= this.visited.length) this.grow();
    this.edgeNodes[id] = node;
    const t = (.5 - v0) / (v1 - v0);
    this.coordinates[node * 2] = (x0 + t * (x1 - x0)) * step + originX;
    this.coordinates[node * 2 + 1] = (y0 + t * (y1 - y0)) * step + originY;
    this.neighbours[node * 2] = 0; this.neighbours[node * 2 + 1] = 0;
    this.visited[node] = 0;
    return node;
  }

  private connect(a: number, b: number) {
    // Marching-square edges have at most two neighbours. Preserve connection
    // insertion order, including saddles, so loop starts and direction match.
    if (this.neighbours[a * 2] === 0) { this.starts.push(a); this.neighbours[a * 2] = b; }
    else this.neighbours[a * 2 + 1] = b;
    if (this.neighbours[b * 2] === 0) { this.starts.push(b); this.neighbours[b * 2] = a; }
    else this.neighbours[b * 2 + 1] = a;
  }

  trace(field: Float32Array, nx: number, ny: number, originX: number, originY: number, step: number): Outline {
    const edgeCount = nx * ny * 2;
    if (this.edgeNodes.length < edgeCount) this.edgeNodes = new Uint32Array(Math.max(edgeCount, this.edgeNodes.length * 2));
    else this.edgeNodes.fill(0, 0, edgeCount);
    this.count = 0; this.starts.length = 0;
    const crossings = this.crossings;
    for (let y = 0; y < ny - 1; y++) {
      for (let x = 0; x < nx - 1; x++) {
        const i = y * nx + x;
        const a = field[i], b = field[i + 1], c = field[i + nx + 1], d = field[i + nx];
        const mask = Number(a >= .5) | (Number(b >= .5) << 1) | (Number(c >= .5) << 2) | (Number(d >= .5) << 3);
        if (mask === 0 || mask === 15) continue;
        let count = 0;
        if ((a >= .5) !== (b >= .5)) crossings[count++] = this.edge(i * 2, x, y, x + 1, y, a, b, originX, originY, step);
        if ((b >= .5) !== (c >= .5)) crossings[count++] = this.edge((i + 1) * 2 + 1, x + 1, y, x + 1, y + 1, b, c, originX, originY, step);
        if ((d >= .5) !== (c >= .5)) crossings[count++] = this.edge((i + nx) * 2, x, y + 1, x + 1, y + 1, d, c, originX, originY, step);
        if ((a >= .5) !== (d >= .5)) crossings[count++] = this.edge(i * 2 + 1, x, y, x, y + 1, a, d, originX, originY, step);
        if (count === 2) {
          this.connect(crossings[0], crossings[1]);
        } else {
          // Asymptotic decider for ambiguous saddles: consistent merge topology.
          const determinant = (a - .5) * (c - .5) - (b - .5) * (d - .5);
          if (determinant >= 0) {
            this.connect(crossings[0], crossings[1]);
            this.connect(crossings[2], crossings[3]);
          } else {
            this.connect(crossings[0], crossings[3]);
            this.connect(crossings[1], crossings[2]);
          }
        }
      }
    }
    const loops: Outline = [];
    for (const start of this.starts) {
      if (this.visited[start]) continue;
      const loop: Point[] = [];
      let current = start, previous = 0;
      do {
        this.visited[current] = 1;
        loop.push([this.coordinates[current * 2], this.coordinates[current * 2 + 1]]);
        let next = this.neighbours[current * 2];
        if (next === previous) next = this.neighbours[current * 2 + 1];
        previous = current;
        if (next === 0) break;
        current = next;
      } while (current !== start && !this.visited[current]);
      if (current === start && loop.length >= 3) loops.push(loop);
    }
    return loops;
  }
}

// Tracing is synchronous. Share scratch space across pools, but return fresh
// points so cached outlines never refer to buffers reused by the next trace.
const tracer = new ContourTracer();
export function traceContours(field: Float32Array, nx: number, ny: number, originX: number, originY: number, step: number): Outline {
  return tracer.trace(field, nx, ny, originX, originY, step);
}
