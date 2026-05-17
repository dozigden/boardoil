import { describe, expect, it } from 'vitest';
import { resolveCardBoundaryClass, resolveSlickBoundaryType } from './slickCardBoundary';

describe('slickCardBoundary', () => {
  it('returns no boundary class for the first card', () => {
    const cards = [makeCard(1, 10), makeCard(2, 10)];
    expect(resolveCardBoundaryClass(cards, 0)).toBeNull();
  });

  it('returns no boundary class when adjacent cards share the same slick', () => {
    const cards = [makeCard(1, 10), makeCard(2, 10)];
    expect(resolveCardBoundaryClass(cards, 1)).toBeNull();
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('none');
  });

  it('returns no boundary class when adjacent cards are both unslicked', () => {
    const cards = [makeCard(1, null), makeCard(2, null)];
    expect(resolveCardBoundaryClass(cards, 1)).toBeNull();
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('none');
  });

  it('returns standard slick gap class for slick-to-none boundaries', () => {
    const cards = [makeCard(1, 10), makeCard(2, null)];
    expect(resolveCardBoundaryClass(cards, 1)).toBe('card--slick-gap');
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('with-none');
  });

  it('returns standard slick gap class for none-to-slick boundaries', () => {
    const cards = [makeCard(1, null), makeCard(2, 10)];
    expect(resolveCardBoundaryClass(cards, 1)).toBe('card--slick-gap');
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('with-none');
  });

  it('returns strong slick gap class for boundaries between different slicks', () => {
    const cards = [makeCard(1, 10), makeCard(2, 20)];
    expect(resolveCardBoundaryClass(cards, 1)).toBe('card--slick-gap-strong');
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('between-slicks');
  });
});

function makeCard(id: number, slickId: number | null) {
  return {
    id,
    boardColumnId: 1,
    cardTypeId: 1,
    slickId,
    cardTypeName: 'Story',
    cardTypeEmoji: null,
    title: `Card ${id}`,
    description: '',
    sortKey: `${id}`.padStart(20, '0'),
    tags: [],
    tagNames: [],
    createdAtUtc: '2026-05-10T00:00:00Z',
    updatedAtUtc: '2026-05-10T00:00:00Z'
  };
}
