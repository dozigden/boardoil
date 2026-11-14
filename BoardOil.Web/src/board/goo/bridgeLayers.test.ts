import { describe, expect, it } from 'vitest';
import { bridgePatches, BridgeLayers } from './bridgeLayers';
import { buildNecks } from './bridges';
import { blendedOutline } from './surface';
import type { GooCard } from './types';

describe('Bridges column outlines', () => {
  it('retains inward side indents between vertically joined cards', () => {
    const cards = [100, 208].map(top => ({ left: 100, top, width: 300, height: 116, column: 0 }));
    const loops = blendedOutline(cards, [], { left: 0, top: 0, width: 500, height: 400, clipTop: 0, clipBottom: 400 });
    expect(loops).toHaveLength(1);
    const rightEdge = loops[0].filter(([x, y]) => x > 350 && Math.abs(y - 158) < 2);
    const indent = loops[0].filter(([x, y]) => x > 350 && Math.abs(y - 212) < 2);
    expect(rightEdge.length).toBeGreaterThan(0);
    expect(indent.length).toBeGreaterThan(0);
    expect(Math.max(...indent.map(([x]) => x))).toBeLessThan(Math.min(...rightEdge.map(([x]) => x)) - 3);
  });

  it('reuses column-local shapes and invalidates them when card geometry changes', () => {
    const renderer = new BridgeLayers();
    const appearance = [{ id: 1, name: 'Slick', colour: '#95d4aa' }];
    const cards: GooCard[] = [{ id: 1, group: 1, centreX: 160, centreY: 100, width: 280, height: 100, radius: 12, column: 0 }];
    const first = renderer.columnOutlines(0, cards, appearance);
    expect(first[0].d).toContain('M');
    expect(renderer.columnOutlines(0, structuredClone(cards), structuredClone(appearance))).toBe(first);
    expect(renderer.outlineBuilds).toBe(1);
    expect(renderer.columnOutlines(0, [{ ...cards[0], height: 140 }], appearance)).not.toBe(first);
    expect(renderer.outlineBuilds).toBe(2);
    expect(renderer.columnOutlines(0, [], appearance)).toEqual([]);
  });

  it('limits dynamic patches to the cross-column bridge and shoulders', () => {
    const cards = [{ left: 20, top: 100, width: 300, height: 116, column: 0 },
      { left: 380, top: 130, width: 300, height: 116, column: 1 }];
    const necks = buildNecks(cards);
    expect(necks).toHaveLength(1);
    const patches = bridgePatches(necks);
    expect(patches).toHaveLength(1);
    expect(patches[0].left).toBeGreaterThan(250);
    expect(patches[0].right).toBeLessThan(450);
    expect(bridgePatches([...necks, ...necks])).toEqual(patches);
    expect(buildNecks(cards.map(c => ({ ...c, column: 0 })))).toEqual([]);
  });
});
