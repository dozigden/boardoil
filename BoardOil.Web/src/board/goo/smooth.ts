import { blendedOutline } from './surface';
import type { SurfaceRect, Neck, Viewport, Outline, GooCard } from './types';
// Placement and thickness stay explicit. Surface blending controls the organic
// shoulders and multi-way intersections, without browser blur filters.
const clamp = (v: number, lo: number, hi: number) => Math.max(lo, Math.min(hi, v));
const smooth = (t: number) => t * t * (3 - 2 * t);

function connect(left: SurfaceRect, right: SurfaceRect, reach: number, thickness: number, vertical = false): Neck | false {
  const gap = right.x - left.x - left.width;
  if (gap <= 0) return false;
  const maxDistance = reach * 4;
  const overlapTop = Math.max(left.y, right.y);
  const overlapBottom = Math.min(left.y + left.height, right.y + right.height);
  const preferred = (overlapTop + overlapBottom) / 2;
  const normalShoulder = Math.min(thickness / 2 + 18, left.height / 2 - 6, right.height / 2 - 6);
  const overlap = overlapBottom - overlapTop;
  const attachmentHalf = Math.min(normalShoulder, Math.max(thickness / 2 + 2, overlap / 2 - 4));
  if (attachmentHalf <= 0) return false;
  // Put the throat in the overlap, including near card corners. Surface
  // blending forms the shoulders; reserving rectangular shoulder margins
  // forced premature diagonals and cut the reference's long pockets in two.
  const ly = clamp(preferred, left.y + 4, left.y + left.height - 4);
  const ry = clamp(preferred, right.y + 4, right.y + right.height - 4);
  const distance = Math.hypot(gap, ry - ly);
  if (distance >= maxDistance) return false;
  let strength = 1 - smooth(clamp((distance - maxDistance * .55) / (maxDistance * .45), 0, 1));
  if (!vertical) {
    const centerDelta = Math.abs((left.y + left.height / 2) - (right.y + right.height / 2));
    const verticalLimit = Math.min(left.height, right.height) + reach * .4;
    strength *= 1 - smooth(clamp((centerDelta - verticalLimit * .7) / (verticalLimit * .3), 0, 1));
  }
  const neckHalf = Math.min(thickness / 2, attachmentHalf * .8) * strength * strength;
  if (neckHalf < .6) return false;
  const lx = left.x + left.width - 6, rx = right.x + 6;
  if (vertical) return { x0:ly, y0:lx, x1:ry, y1:rx, radius:neckHalf };
  return { x0:lx, y0:ly, x1:rx, y1:ry, radius:neckHalf };
}

export function cardSurface(card: GooCard): SurfaceRect {
  return { x: card.x - card.width / 2 - 8, y: card.y - card.height / 2 - 8,
    width: card.width + 16, height: card.height + 16, column: card.column };
}

export function buildNecks(cards: SurfaceRect[]): Neck[] {
  const necks: Neck[] = [];
  for (const a of cards) for (const b of cards) {
    if (b.column !== a.column + 1) continue;
    const neck = connect(a, b, 36, 44);
    if (neck) necks.push(neck);
  }
  return necks;
}

export function outlinePath(loops: Outline): Path2D {
  const path = new Path2D();
  for (const loop of loops) {
    path.moveTo(...loop[0]);
    for (let i = 1; i < loop.length; i++) path.lineTo(...loop[i]);
    path.closePath();
  }
  return path;
}

export function outlineSvg(loops: Outline): string {
  return loops.map(loop => `M${loop.map(p => `${p[0].toFixed(2)},${p[1].toFixed(2)}`).join('L')}Z`).join('');
}

export function buildOrganicPath(rects: SurfaceRect[], viewport: Viewport) {
  const cards = rects.map(r => ({ x: r.x + 6, y: r.y + 6, width: r.width - 12, height: r.height - 12, column: r.column }));
  return outlinePath(blendedOutline(cards, buildNecks(cards), viewport));
}
