import { describe, expect, it, vi } from 'vitest';
import type { BoardColumn, Card } from '../../shared/types/boardTypes';
import { useBoardCardRemovals } from './useBoardCardRemovals';

vi.mock('vue', async importOriginal => ({
  ...await importOriginal<typeof import('vue')>(),
  onBeforeUnmount: vi.fn()
}));

describe('board departure projection', () => {
  it('keeps departure spacing while live cards use their new neighbours', () => {
    const cards = [card(1, 10), card(2, 20), card(3, 10)];
    const removals = useBoardCardRemovals();
    removals.retain([column(cards)], new Set([2]));
    const projected = removals.project([column([cards[0]!, cards[2]!])])[0]!.cards;

    expect(projected.map(card => card.id)).toEqual([1, 2, 3]);
    expect(projected.map((_, index) => removals.boundaryClass(projected, index))).toEqual([
      'card--slick-gap-top', 'card--slick-gap-strong', 'card--slick-gap-bottom'
    ]);
  });

  it('rebuilds boundaries when live order or slick membership changes and releases departures', () => {
    const cards = [card(1, 10), card(2, 20), card(3, 10)];
    const removals = useBoardCardRemovals();
    removals.retain([column(cards)], new Set([2]));
    removals.project([column([cards[0]!, cards[2]!])]);
    const live = [card(3, null), cards[0]!];
    const projected = removals.project([column(live)])[0]!.cards;

    expect(projected.map(card => card.id)).toEqual([2, 3, 1]);
    expect(projected.map((_, index) => removals.boundaryClass(projected, index))).toEqual([
      'card--slick-gap-strong', null, 'card--slick-gap-bottom card--slick-gap'
    ]);
    removals.remove(2);
    const settled = removals.project([column(live)])[0]!.cards;
    expect(settled).toBe(live);
    expect(removals.boundaryClass(settled, 1)).toBe('card--slick-gap-bottom card--slick-gap');
  });

  it('reuses precomputed boundaries across repeated renders', () => {
    const readSlick = vi.fn(() => 10);
    const cards = Array.from({ length: 100 }, (_, index) => {
      const model = card(index + 1, 10);
      Object.defineProperty(model, 'slickId', { get: readSlick });
      return model;
    });
    const removals = useBoardCardRemovals();
    removals.retain([column(cards)], new Set([50]));
    const projected = removals.project([column(cards.filter(card => card.id !== 50))])[0]!.cards;
    readSlick.mockClear();

    for (let render = 0; render < 10; render++) {
      for (let index = 0; index < projected.length; index++) {
        removals.boundaryClass(projected, index);
      }
    }

    expect(readSlick).not.toHaveBeenCalled();
    expect(removals.boundaryClass(projected, 0)).toBe('card--slick-gap-top');
    expect(removals.boundaryClass(projected, 99)).toBe('card--slick-gap-bottom');
  });
});

function column(cards: Card[]): BoardColumn {
  return { id: 1, title: 'Todo', sortKey: 'A', createdAtUtc: '', updatedAtUtc: '', cards };
}

function card(id: number, slickId: number | null): Card {
  return {
    id, slickId, boardColumnId: 1, cardTypeId: 1, slick: null,
    cardTypeName: 'Story', cardTypeEmoji: null, title: `Card ${id}`, description: '',
    completedChecklistItemCount: 0, totalChecklistItemCount: 0,
    externalUrl: null, sortKey: String(id), tags: [], tagNames: [], cardCreatedUtc: '', cardUpdatedUtc: ''
  };
}
