import { buildNecks, cardSurface, outlinePath, outlineSvg } from './bridges';
import { blendedOutline } from './surface';
import type { SlickAppearance } from './scene';
import type { GooCard, Neck, SurfaceRect, Viewport } from './types';

export type ColumnOutline = { id: number; colour: string; d: string };
type Patch = { left: number; top: number; right: number; bottom: number };

function touches(a: Patch, b: Patch) {
  return a.left <= b.right && a.right >= b.left && a.top <= b.bottom && a.bottom >= b.top;
}

export function bridgePatches(necks: Neck[]): Patch[] {
  const patches: Patch[] = [];
  for (const neck of necks) {
    const pad = neck.radius + 27;
    const patch = { left: Math.min(neck.x0, neck.x1) - pad, right: Math.max(neck.x0, neck.x1) + pad,
      top: Math.min(neck.y0, neck.y1) - pad, bottom: Math.max(neck.y0, neck.y1) + pad };
    // Intersecting shoulders must share one field, including three-way joins.
    for (let i = 0; i < patches.length;) {
      const other = patches[i];
      if (!touches(patch, other)) { i++; continue; }
      patch.left = Math.min(patch.left, other.left); patch.right = Math.max(patch.right, other.right);
      patch.top = Math.min(patch.top, other.top); patch.bottom = Math.max(patch.bottom, other.bottom);
      patches.splice(i, 1);
      i = 0;
    }
    patches.push(patch);
  }
  return patches;
}

function bounds(cards: SurfaceRect[]): Viewport {
  const left = Math.min(...cards.map(c => c.left)) - 30, top = Math.min(...cards.map(c => c.top)) - 30;
  const right = Math.max(...cards.map(c => c.left + c.width)) + 30;
  const bottom = Math.max(...cards.map(c => c.top + c.height)) + 30;
  return { left, top, width: right - left, height: bottom - top, clipTop: top, clipBottom: bottom };
}

export class BridgeLayers {
  private columns = new Map<number, { signature: string; outlines: ColumnOutline[] }>();
  private bridges = new Map<number, { signature: string; paths: { path: Path2D; patch: Patch }[] }>();
  outlineBuilds = 0;

  columnOutlines(column: number, cards: GooCard[], appearances: SlickAppearance[]): ColumnOutline[] {
    const signature = JSON.stringify([cards, appearances]);
    const cached = this.columns.get(column);
    if (cached?.signature === signature) return cached.outlines;
    const outlines = appearances.flatMap(appearance => {
      const members = cards.filter(c => c.group === appearance.id).map(cardSurface).sort((a, b) => a.top - b.top);
      if (!members.length) return [];
      // Keep disconnected parts separate so long columns don't allocate empty grids.
      const segments: SurfaceRect[][] = [];
      let bottom = -Infinity;
      for (const member of members) {
        if (member.top > bottom + 27) segments.push([]);
        segments[segments.length - 1].push(member);
        bottom = Math.max(bottom, member.top + member.height);
      }
      const d = segments.map(segment => outlineSvg(blendedOutline(segment, [], bounds(segment)))).join('');
      return [{ id: appearance.id, colour: appearance.colour, d }];
    });
    this.columns.set(column, { signature, outlines });
    this.outlineBuilds++;
    return outlines;
  }

  retainColumns(columns: Set<number>) {
    for (const column of this.columns.keys()) if (!columns.has(column)) this.columns.delete(column);
  }

  draw(ctx: CanvasRenderingContext2D, cards: GooCard[], appearances: SlickAppearance[], viewport: Viewport,
    floating: ReadonlyMap<number, HTMLElement>) {
    ctx.clearRect(0, 0, viewport.width, viewport.height);
    ctx.save();
    ctx.translate(-viewport.left, -viewport.top);
    ctx.beginPath(); ctx.rect(viewport.left, viewport.clipTop, viewport.width, Math.max(0, viewport.clipBottom - viewport.clipTop)); ctx.clip();
    const visible = cards.filter(c => c.centreX + c.width / 2 >= viewport.left - 168 && c.centreX - c.width / 2 <= viewport.left + viewport.width + 168 &&
      c.centreY + c.height / 2 >= viewport.clipTop - 168 && c.centreY - c.height / 2 <= viewport.clipBottom + 168);
    const groups = new Set(appearances.map(a => a.id));
    for (const id of this.bridges.keys()) if (!groups.has(id)) this.bridges.delete(id);
    for (const appearance of appearances) {
      const members = visible.filter(c => c.group === appearance.id);
      const surfaces = members.map(cardSurface);
      const necks = buildNecks(surfaces);
      const signature = JSON.stringify([surfaces, necks, viewport, [...floating.keys()]]);
      let cached = this.bridges.get(appearance.id);
      if (cached?.signature !== signature) {
        const patches = bridgePatches(necks);
        // Travelling cards have no native column outline until they land.
        for (const card of members.filter(c => floating.has(c.id))) {
          const b = bounds([cardSurface(card)]);
          patches.push({ left: b.left, top: b.top, right: b.left + b.width, bottom: b.top + b.height });
        }
        const paths = patches.map(patch => {
          const region = { ...viewport, left: patch.left, top: patch.top, width: patch.right - patch.left, height: patch.bottom - patch.top };
          const nearby = surfaces.filter(c => c.left <= patch.right + 27 && c.left + c.width >= patch.left - 27 &&
            c.top <= patch.bottom + 27 && c.top + c.height >= patch.top - 27);
          return { patch, path: outlinePath(blendedOutline(nearby, necks, region)) };
        });
        cached = { signature, paths };
        this.bridges.set(appearance.id, cached);
      }
      ctx.fillStyle = appearance.colour;
      for (const { path, patch } of cached.paths) {
        ctx.save(); ctx.beginPath(); ctx.rect(patch.left, patch.top, patch.right - patch.left, patch.bottom - patch.top); ctx.clip();
        ctx.fill(path, 'evenodd'); ctx.restore();
      }
    }
    ctx.restore();
  }
}
