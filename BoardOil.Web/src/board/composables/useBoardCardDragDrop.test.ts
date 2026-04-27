import { computed, ref } from 'vue';
import { describe, expect, it, vi } from 'vitest';
import type { BoardColumn, Card } from '../../shared/types/boardTypes';
import { useBoardCardDragDrop } from './useBoardCardDragDrop';

describe('useBoardCardDragDrop', () => {
  it('sets tail drop point to append when dragging over the column tail zone', () => {
    const startDrag = vi.fn();
    const dropCard = vi.fn(async () => undefined);
    const selectionMode = ref(false);
    const model = useBoardCardDragDrop(computed(() => makeColumns()), selectionMode, startDrag, dropCard);

    model.onCardDragStart(101, 1);
    model.onColumnTailDragOver(2);

    expect(model.activeDropPoint.value).toEqual({ columnId: 2, targetCardId: null });
  });

  it('drops to column tail using null target card id', async () => {
    const startDrag = vi.fn();
    const dropCard = vi.fn(async () => undefined);
    const selectionMode = ref(false);
    const model = useBoardCardDragDrop(computed(() => makeColumns()), selectionMode, startDrag, dropCard);

    model.onCardDragStart(101, 1);
    model.onColumnTailDragOver(2);
    await model.onColumnTailDrop(2);

    expect(dropCard).toHaveBeenCalledWith(2, null);
    expect(model.draggingCardId.value).toBeNull();
    expect(model.activeDropPoint.value).toBeNull();
  });

  it('ignores tail drag/drop while card selection mode is enabled', async () => {
    const startDrag = vi.fn();
    const dropCard = vi.fn(async () => undefined);
    const selectionMode = ref(true);
    const model = useBoardCardDragDrop(computed(() => makeColumns()), selectionMode, startDrag, dropCard);

    model.onCardDragStart(101, 1);
    model.onColumnTailDragOver(2);
    await model.onColumnTailDrop(2);

    expect(model.draggingCardId.value).toBeNull();
    expect(model.activeDropPoint.value).toBeNull();
    expect(dropCard).not.toHaveBeenCalled();
  });

  it('allows dropping on own column tail and still appends with null target', async () => {
    const startDrag = vi.fn();
    const dropCard = vi.fn(async () => undefined);
    const selectionMode = ref(false);
    const model = useBoardCardDragDrop(computed(() => makeColumns()), selectionMode, startDrag, dropCard);

    model.onCardDragStart(101, 1);
    model.onColumnTailDragOver(1);
    expect(model.activeDropPoint.value).toEqual({ columnId: 1, targetCardId: null });
    await model.onColumnTailDrop(1);

    expect(dropCard).toHaveBeenCalledWith(1, null);
  });
});

function makeColumns(): BoardColumn[] {
  return [
    {
      id: 1,
      title: 'Todo',
      sortKey: 'A',
      createdAtUtc: '2026-04-01T00:00:00Z',
      updatedAtUtc: '2026-04-01T00:00:00Z',
      cards: [makeCard(101, 1, 'Task A'), makeCard(102, 1, 'Task B')]
    },
    {
      id: 2,
      title: 'Doing',
      sortKey: 'B',
      createdAtUtc: '2026-04-01T00:00:00Z',
      updatedAtUtc: '2026-04-01T00:00:00Z',
      cards: [makeCard(201, 2, 'Task C')]
    }
  ];
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
    sortKey: `${id}`,
    tags: [],
    tagNames: [],
    createdAtUtc: '2026-04-01T00:00:00Z',
    updatedAtUtc: '2026-04-01T00:00:00Z'
  };
}
