import { cardDistance, type Liquid } from './physics';
import { seedReach, seedSpacing } from './liquidSeeding';
import type { Bounds, GooCard } from './types';

const retentionMargin = 40;
const contains = (b: Bounds, x: number, y: number) => x >= b.left && x < b.right && y >= b.top && y < b.bottom;
function intersect(a: Bounds, b: Bounds): Bounds | null {
  const result = { left: Math.max(a.left, b.left), right: Math.min(a.right, b.right),
    top: Math.max(a.top, b.top), bottom: Math.min(a.bottom, b.bottom) };
  return result.left < result.right && result.top < result.bottom ? result : null;
}
function translate(b: Bounds, x: number, y: number): Bounds {
  return { left: b.left + x, right: b.right + x, top: b.top + y, bottom: b.bottom + y };
}
function subtract(area: Bounds, covered: Bounds): Bounds[] {
  const overlap = intersect(area, covered);
  if (!overlap) return [area];
  return [
    { ...area, bottom: overlap.top }, { ...area, top: overlap.bottom },
    { left: area.left, right: overlap.left, top: overlap.top, bottom: overlap.bottom },
    { left: overlap.right, right: area.right, top: overlap.top, bottom: overlap.bottom }
  ].filter(b => b.left < b.right && b.top < b.bottom);
}
function merge(areas: Bounds[]): Bounds[] {
  for (let i = 0; i < areas.length; i++) for (let j = i + 1; j < areas.length; j++) {
    const a = areas[i], b = areas[j];
    const vertical = a.left === b.left && a.right === b.right && a.top <= b.bottom && b.top <= a.bottom;
    const horizontal = a.top === b.top && a.bottom === b.bottom && a.left <= b.right && b.left <= a.right;
    if (!vertical && !horizontal) continue;
    areas[i] = { left: Math.min(a.left, b.left), right: Math.max(a.right, b.right),
      top: Math.min(a.top, b.top), bottom: Math.max(a.bottom, b.bottom) };
    areas.splice(j, 1);
    return merge(areas);
  }
  return areas;
}

export class LiquidResidency {
  // Coverage follows each card, so independently scrolling columns do not reseed
  // liquid that is still present. Retain only nearby coverage, not board history.
  private coverage = new Map<number, Bounds[]>();

  update(liquid: Liquid, active: Bounds) {
    const retained = { left: active.left - retentionMargin, right: active.right + retentionMargin,
      top: active.top - retentionMargin, bottom: active.bottom + retentionMargin };
    liquid.activeBand = active;
    liquid.retentionBand = retained;
    liquid.cullParticles();
    const owners = liquid.cards.map(card => ({ card, reach: seedReach(card, liquid.cards) }));
    const requests = new Map<number, Bounds[]>();
    const nextCoverage = new Map<number, Bounds[]>();
    for (const { card, reach } of owners) {
      if (!intersect(retained, reach)) continue;
      const localKeep = translate(retained, -card.centreX, -card.centreY);
      const covered = (this.coverage.get(card.id) ?? []).flatMap(b => {
        const clipped = intersect(b, localKeep);
        return clipped ? [clipped] : [];
      });
      let uncovered = [translate(active, -card.centreX, -card.centreY)];
      for (const area of covered) uncovered = uncovered.flatMap(b => subtract(b, area));
      const entering = uncovered.flatMap(b => {
        const clipped = intersect(translate(b, card.centreX, card.centreY), reach);
        return clipped ? [clipped] : [];
      });
      if (entering.length) requests.set(card.id, entering);
      nextCoverage.set(card.id, merge([...covered, ...uncovered]));
    }
    this.coverage = nextCoverage;
    if (!requests.size) return;
    const areas = [...requests.values()].flat();
    const area = { left: Math.min(...areas.map(b => b.left)), right: Math.max(...areas.map(b => b.right)),
      top: Math.min(...areas.map(b => b.top)), bottom: Math.max(...areas.map(b => b.bottom)) };
    const spaced = seedSpacing(liquid.particles);
    liquid.seedBoard(area, (x, y) => {
      if (!areas.some(b => contains(b, x, y))) return false;
      let owner: GooCard | undefined, nearest = Infinity;
      for (const candidate of owners) {
        if (!contains(candidate.reach, x, y)) continue;
        const distance = cardDistance(x, y, candidate.card).distance;
        if (distance < nearest || (distance === nearest && candidate.card.id < owner!.id)) {
          owner = candidate.card; nearest = distance;
        }
      }
      return !!owner && !!requests.get(owner.id)?.some(b => contains(b, x, y)) && spaced(x, y);
    });
  }
}
