import { describe, expect, it } from 'vitest';
import { buildSelectionBridgeCards, SELECTION_GROUP_ID } from './selectionGooAdapter';
import type { GooCard } from '../goo/types';

const cards: GooCard[] = [
  { id: 1, group: 11, column: 0, centreX: 150, centreY: 100, width: 280, height: 100, radius: 12 },
  { id: 2, group: 22, column: 0, centreX: 150, centreY: 210, width: 280, height: 100, radius: 12 },
  { id: 3, group: null, column: 1, centreX: 510, centreY: 100, width: 280, height: 100, radius: 12 }
];

describe('selection bridge cards', () => {
  it('groups selected cards across slicks and columns, including cards without a slick', () => {
    const selected = buildSelectionBridgeCards(cards, new Set([1, 2, 3]));
    expect(selected.map(card => card.group)).toEqual([SELECTION_GROUP_ID, SELECTION_GROUP_ID, SELECTION_GROUP_ID]);
    expect(selected.map(card => card.column)).toEqual([0, 0, 1]);
  });

  it('excludes unselected slick members and IDs outside the visible geometry', () => {
    expect(buildSelectionBridgeCards(cards, new Set([1, 99])).map(card => card.group)).toEqual([SELECTION_GROUP_ID, null, null]);
    expect(buildSelectionBridgeCards(cards, new Set()).every(card => card.group === null)).toBe(true);
  });

  it('preserves geometry and leaves slick membership in the source untouched', () => {
    const before = structuredClone(cards);
    const selected = buildSelectionBridgeCards(cards, new Set([1, 2, 3]));
    expect(cards).toEqual(before);
    selected.forEach((card, index) => expect({ ...card, group: cards[index].group }).toEqual(cards[index]));
  });
});
