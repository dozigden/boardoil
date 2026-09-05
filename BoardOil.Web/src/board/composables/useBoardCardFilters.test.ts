import { describe, expect, it } from 'vitest';
import { ref } from 'vue';
import type { Board, Card, CardType, Slick, Tag } from '../../shared/types/boardTypes';
import { useBoardCardFilters } from './useBoardCardFilters';

describe('useBoardCardFilters', () => {
  it('filters visible cards by card type state keyed by id', () => {
    const board = ref<Board>(makeBoard([
      makeCard(1, 'Story card', 10),
      makeCard(2, 'Bug card', 20)
    ]));
    const model = useBoardCardFilters(
      board,
      ref<Tag[]>([]),
      ref<Slick[]>([]),
      ref<CardType[]>([
        makeCardType(10, 'Story'),
        makeCardType(20, 'Bug')
      ]));

    model.cardTypeFilterStates.value = { '20': 'include' };

    expect(model.filteredColumns.value[0]?.cards.map(card => card.title)).toEqual(['Bug card']);
    expect(model.includedCardTypeIds.value).toEqual([20]);
    expect(model.hasActiveCardFilters.value).toBe(true);

    model.clearCardFilters();

    expect(model.cardTypeFilterStates.value).toEqual({});
    expect(model.filteredColumns.value[0]?.cards.map(card => card.title)).toEqual(['Story card', 'Bug card']);
  });
});

function makeBoard(cards: Card[]): Board {
  return {
    id: 1,
    name: 'BoardOil',
    description: '',
    slickCohesionModeEnabled: true,
    createdAtUtc: '2026-07-13T00:00:00Z',
    updatedAtUtc: '2026-07-13T00:00:00Z',
    columns: [
      {
        id: 10,
        title: 'Todo',
        sortKey: '100',
        createdAtUtc: '2026-07-13T00:00:00Z',
        updatedAtUtc: '2026-07-13T00:00:00Z',
        cards
      }
    ]
  };
}

function makeCard(id: number, title: string, cardTypeId: number): Card {
  return {
    id,
    slick: null,
    boardColumnId: 10,
    cardTypeId,
    cardTypeName: 'Story',
    cardTypeEmoji: null,
    title,
    description: '',
    externalUrl: null,
    sortKey: String(id).padStart(3, '0'),
    tags: [],
    tagNames: [],
    cardCreatedUtc: '2026-07-13T00:00:00Z',
    cardUpdatedUtc: '2026-07-13T00:00:00Z'
  };
}

function makeCardType(id: number, name: string): CardType {
  return {
    id,
    name,
    styleName: 'auto',
    stylePropertiesJson: '{}',
    emoji: null,
    isSystem: false,
    createdAtUtc: '2026-07-13T00:00:00Z',
    updatedAtUtc: '2026-07-13T00:00:00Z'
  };
}
