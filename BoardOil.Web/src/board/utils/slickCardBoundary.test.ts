import { describe, expect, it } from 'vitest';
import { resolveCardBoundaryClass, resolveSlickBoundaryType } from './slickCardBoundary';

describe('slickCardBoundary', () => {
  it('returns top slick gap class for a slick first card', () => {
    const cards = [makeCard(1, 10), makeCard(2, null)];
    expect(resolveCardBoundaryClass(cards, 0)).toBe('card--slick-gap-top');
  });

  it('returns no boundary class for an unslicked first card', () => {
    const cards = [makeCard(1, null), makeCard(2, 10)];
    expect(resolveCardBoundaryClass(cards, 0)).toBeNull();
  });

  it('returns no boundary class when adjacent cards share the same slick in the middle', () => {
    const cards = [makeCard(1, 10), makeCard(2, 10), makeCard(3, null)];
    expect(resolveCardBoundaryClass(cards, 1)).toBeNull();
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('none');
  });

  it('returns no boundary class when adjacent cards are both unslicked in the middle', () => {
    const cards = [makeCard(1, null), makeCard(2, null), makeCard(3, 10)];
    expect(resolveCardBoundaryClass(cards, 1)).toBeNull();
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('none');
  });

  it('returns standard slick gap class for slick-to-none boundaries in the middle', () => {
    const cards = [makeCard(1, 10), makeCard(2, null), makeCard(3, 10)];
    expect(resolveCardBoundaryClass(cards, 1)).toBe('card--slick-gap');
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('with-none');
  });

  it('returns standard slick gap class for none-to-slick boundaries in the middle', () => {
    const cards = [makeCard(1, null), makeCard(2, 10), makeCard(3, null)];
    expect(resolveCardBoundaryClass(cards, 1)).toBe('card--slick-gap');
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('with-none');
  });

  it('returns strong slick gap class for boundaries between different slicks in the middle', () => {
    const cards = [makeCard(1, 10), makeCard(2, 20), makeCard(3, 10)];
    expect(resolveCardBoundaryClass(cards, 1)).toBe('card--slick-gap-strong');
    expect(resolveSlickBoundaryType(cards[0]!, cards[1]!)).toBe('between-slicks');
  });

  it('returns bottom slick gap class for a slick last card', () => {
    const cards = [makeCard(1, null), makeCard(2, 10)];
    expect(resolveCardBoundaryClass(cards, 1)).toBe('card--slick-gap-bottom card--slick-gap');
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
