import { ref } from 'vue';
import { describe, expect, it, vi } from 'vitest';
import type { Board, Card } from '../../shared/types/boardTypes';
import { useBoardCardSelection } from './useBoardCardSelection';

describe('useBoardCardSelection', () => {
  it('moves selected cards to target column and exits selection mode', async () => {
    const board = ref<Board | null>(makeBoard());
    const archiveCards = vi.fn(async () => true);
    const bulkMoveCards = vi.fn(async () => true);
    const openArchivedCards = vi.fn(async () => undefined);

    const model = useBoardCardSelection(board, archiveCards, bulkMoveCards, openArchivedCards);

    model.toggleCardSelectionMode();
    model.toggleCardSelection(101);
    model.toggleCardSelection(102);

    const moved = await model.moveSelectedCardsByDropTarget(2, 201);

    expect(moved).toBe(true);
    expect(bulkMoveCards).toHaveBeenCalledWith([101, 102], 2, 201);
    expect(model.isCardSelectionMode.value).toBe(false);
    expect(model.selectedCardCount.value).toBe(0);
  });

  it('inverts only the provided visible card ids while in selection mode', () => {
    const board = ref<Board | null>(makeBoard());
    const archiveCards = vi.fn(async () => true);
    const bulkMoveCards = vi.fn(async () => true);
    const openArchivedCards = vi.fn(async () => undefined);

    const model = useBoardCardSelection(board, archiveCards, bulkMoveCards, openArchivedCards);

    model.toggleCardSelectionMode();
    model.toggleCardSelection(101);
    model.invertCardIds([101, 102]);

    expect(model.selectedCardIds.value).toEqual([102]);
    expect(model.selectedCardCount.value).toBe(1);
  });
});

function makeBoard(): Board {
  return {
    id: 1,
    name: 'Demo',
    description: 'Demo board',
    slickCohesionModeEnabled: true,
    columns: [
      {
        id: 1,
        title: 'Todo',
        sortKey: 'A',
        cards: [
          makeCard(101, 1, 'Task A'),
          makeCard(102, 1, 'Task B')
        ],
        createdAtUtc: '2026-04-01T00:00:00Z',
        updatedAtUtc: '2026-04-01T00:00:00Z'
      },
      {
        id: 2,
        title: 'Done',
        sortKey: 'B',
        cards: [makeCard(201, 2, 'Task C')],
        createdAtUtc: '2026-04-01T00:00:00Z',
        updatedAtUtc: '2026-04-01T00:00:00Z'
      }
    ],
    createdAtUtc: '2026-04-01T00:00:00Z',
    updatedAtUtc: '2026-04-01T00:00:00Z'
  };
}

function makeCard(id: number, boardColumnId: number, title: string): Card {
  return {
    id,
    boardColumnId,
    cardTypeId: 1,
    cardTypeName: 'Story',
    cardTypeEmoji: null,
    title,
    description: '',
    externalUrl: null,
    sortKey: `${id}`,
    tags: [],
    tagNames: [],
    createdAtUtc: '2026-04-01T00:00:00Z',
    updatedAtUtc: '2026-04-01T00:00:00Z'
  };
}
