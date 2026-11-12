export function traceContours(field, nx, ny, originX, originY, step) {
  const adjacency = new Map();
  const points = new Map();
  function connect(a, b) {
    if (!adjacency.has(a)) adjacency.set(a, []);
    if (!adjacency.has(b)) adjacency.set(b, []);
    adjacency.get(a).push(b);
    adjacency.get(b).push(a);
  }
  // Each grid edge has a unique ID, shared by its two neighbouring cells.
  function edge(id, x0, y0, x1, y1, v0, v1) {
    if (!points.has(id)) {
      const t = (0.5 - v0) / (v1 - v0);
      points.set(id, [(x0 + t * (x1 - x0)) * step + originX, (y0 + t * (y1 - y0)) * step + originY]);
    }
    return id;
  }
  for (let y = 0; y < ny - 1; y++) {
    for (let x = 0; x < nx - 1; x++) {
      const i = y * nx + x;
      const a = field[i], b = field[i + 1], c = field[i + nx + 1], d = field[i + nx];
      const mask = Number(a >= .5) | (Number(b >= .5) << 1) | (Number(c >= .5) << 2) | (Number(d >= .5) << 3);
      if (mask === 0 || mask === 15) continue;
      const crossings = [];
      if ((a >= .5) !== (b >= .5)) crossings.push(edge(i * 2, x, y, x + 1, y, a, b));
      if ((b >= .5) !== (c >= .5)) crossings.push(edge((i + 1) * 2 + 1, x + 1, y, x + 1, y + 1, b, c));
      if ((d >= .5) !== (c >= .5)) crossings.push(edge((i + nx) * 2, x, y + 1, x + 1, y + 1, d, c));
      if ((a >= .5) !== (d >= .5)) crossings.push(edge(i * 2 + 1, x, y, x, y + 1, a, d));
      if (crossings.length === 2) {
        connect(crossings[0], crossings[1]);
      } else {
        // Asymptotic decider for ambiguous saddles: consistent merge topology.
        const determinant = (a - .5) * (c - .5) - (b - .5) * (d - .5);
        if (determinant >= 0) {
          connect(crossings[0], crossings[1]);
          connect(crossings[2], crossings[3]);
        } else {
          connect(crossings[0], crossings[3]);
          connect(crossings[1], crossings[2]);
        }
      }
    }
  }
  const loops = [];
  const visited = new Set();
  for (const start of adjacency.keys()) {
    if (visited.has(start)) continue;
    const loop = [];
    let current = start, previous = -1;
    do {
      visited.add(current);
      loop.push(points.get(current));
      const neighbours = adjacency.get(current);
      const next = neighbours.find(id => id !== previous);
      previous = current;
      current = next;
    } while (current !== start && current !== undefined && !visited.has(current));
    if (current === start && loop.length >= 3) loops.push(loop);
  }
  return loops;
}
