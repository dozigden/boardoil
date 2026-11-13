import type { Bounds, GooCard, Particle } from './types';

export const liquidSeedConfig = {
  coating: 28,
  bridgeHalfWidth: 14,
  bridgeMaxOffset: 150,
  columnSpacing: 10,
  rowSpacing: 8.5,
  jitter: .35,
  retainedSeparation: 6
} as const;

export function seedBridgePeers(a: GooCard, b: GooCard) {
  return Math.abs(a.column - b.column) === 1 && Math.abs(a.centreY - b.centreY) <= liquidSeedConfig.bridgeMaxOffset;
}

export function seedReach(card: GooCard, cards: GooCard[]): Bounds {
  // Coverage includes displaced seeds, rounded outwards to a whole pixel.
  const padding = Math.ceil(liquidSeedConfig.jitter);
  const coating = liquidSeedConfig.coating + padding;
  const bridge = liquidSeedConfig.bridgeHalfWidth + padding;
  const reach = { left: card.centreX - card.width / 2 - coating, right: card.centreX + card.width / 2 + coating,
    top: card.centreY - card.height / 2 - coating, bottom: card.centreY + card.height / 2 + coating };
  for (const other of cards) {
    if (!seedBridgePeers(card, other)) continue;
    reach.left = Math.min(reach.left, other.centreX - bridge); reach.right = Math.max(reach.right, other.centreX + bridge);
    reach.top = Math.min(reach.top, other.centreY - bridge); reach.bottom = Math.max(reach.bottom, other.centreY + bridge);
  }
  return reach;
}

export function seedSpacing(particles: Particle[]) {
  const separation = liquidSeedConfig.retainedSeparation;
  const buckets = new Map<string, Particle[]>();
  for (const p of particles) {
    const key = `${Math.floor(p.x / separation)},${Math.floor(p.y / separation)}`;
    if (!buckets.has(key)) buckets.set(key, []);
    buckets.get(key)!.push(p);
  }
  return (x: number, y: number) => {
    const gx = Math.floor(x / separation), gy = Math.floor(y / separation);
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      for (const p of buckets.get(`${gx + dx},${gy + dy}`) ?? []) {
        if ((p.x - x) ** 2 + (p.y - y) ** 2 < separation ** 2) return false;
      }
    }
    return true;
  };
}
