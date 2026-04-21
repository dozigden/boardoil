import { ref, type ComputedRef, type Ref } from 'vue';
import type { BoardColumn } from '../types/boardTypes';

type StartDragOperation = (cardId: number, fromColumnId: number) => void;

type DropCardOperation = (targetColumnId: number, targetCardId: number | null) => Promise<void>;

type DropIndicator = 'none' | 'before' | 'after';

export function useBoardCardDragDrop(
  filteredColumns: ComputedRef<BoardColumn[]>,
  isCardSelectionMode: Ref<boolean>,
  startDrag: StartDragOperation,
  dropCard: DropCardOperation
) {
  const draggingCardId = ref<number | null>(null);
  const activeDropPoint = ref<{ columnId: number; targetCardId: number | null } | null>(null);

  function onCardDragStart(cardId: number, fromColumnId: number) {
    if (isCardSelectionMode.value) {
      return;
    }

    draggingCardId.value = cardId;
    activeDropPoint.value = null;
    startDrag(cardId, fromColumnId);
  }

  function onCardDragEnd() {
    clearDragInteraction();
  }

  function onCardDragOver(columnId: number, cardId: number, event: DragEvent) {
    if (isCardSelectionMode.value) {
      return;
    }

    if (draggingCardId.value === null || cardId === draggingCardId.value) {
      return;
    }

    const targetCardId = resolveDropTargetCardId(columnId, cardId, event);
    if (targetCardId === undefined) {
      return;
    }

    setDropPoint(columnId, targetCardId);
  }

  async function onCardDrop(columnId: number, cardId: number, event: DragEvent) {
    if (isCardSelectionMode.value) {
      return;
    }

    onCardDragOver(columnId, cardId, event);
    const targetCardId = activeDropPoint.value?.columnId === columnId
      ? activeDropPoint.value.targetCardId
      : cardId;
    await dropAt(columnId, targetCardId);
  }

  function handleColumnDragOver(columnId: number, event: DragEvent) {
    if (isCardSelectionMode.value) {
      return;
    }

    const targetCardId = resolveColumnDropTargetCardId(columnId, event);
    setDropPoint(columnId, targetCardId);
  }

  async function handleColumnDrop(columnId: number) {
    if (isCardSelectionMode.value) {
      return;
    }

    const targetCardId = activeDropPoint.value?.columnId === columnId
      ? activeDropPoint.value.targetCardId
      : null;
    await dropAt(columnId, targetCardId);
  }

  function setDropPoint(columnId: number, targetCardId: number | null) {
    if (draggingCardId.value === null) {
      return;
    }

    if (targetCardId === draggingCardId.value) {
      return;
    }

    activeDropPoint.value = { columnId, targetCardId };
  }

  function isDropPoint(columnId: number, targetCardId: number | null) {
    return activeDropPoint.value?.columnId === columnId && activeDropPoint.value.targetCardId === targetCardId;
  }

  function isDropAtColumnStart(columnId: number) {
    const firstCardId = getFirstVisibleCardId(columnId);
    if (firstCardId === null) {
      return false;
    }

    return activeDropPoint.value?.columnId === columnId
      && activeDropPoint.value.targetCardId === firstCardId;
  }

  function resolveCardDropIndicator(columnId: number, cardId: number): DropIndicator {
    if (isCardSelectionMode.value || draggingCardId.value === null || cardId === draggingCardId.value) {
      return 'none';
    }

    const targetCardId = activeDropPoint.value?.columnId === columnId
      ? activeDropPoint.value.targetCardId
      : undefined;
    if (targetCardId === undefined) {
      return 'none';
    }

    if (targetCardId === cardId) {
      return 'before';
    }

    const nextCardId = getNextVisibleCardId(columnId, cardId);
    if (nextCardId === targetCardId || (targetCardId === null && nextCardId === null)) {
      return 'after';
    }

    return 'none';
  }

  function clearDragInteraction() {
    draggingCardId.value = null;
    activeDropPoint.value = null;
  }

  function resolveDropTargetCardId(columnId: number, cardId: number, event: DragEvent): number | null | undefined {
    const currentTarget = event.currentTarget;
    if (!(currentTarget instanceof HTMLElement)) {
      return cardId;
    }

    const rect = currentTarget.getBoundingClientRect();
    const dropBeforeCard = event.clientY <= rect.top + (rect.height / 2);
    const candidateTargetCardId = dropBeforeCard
      ? cardId
      : getNextVisibleCardId(columnId, cardId);

    return coerceTargetCardId(columnId, candidateTargetCardId);
  }

  function getNextVisibleCardId(columnId: number, cardId: number): number | null {
    const column = filteredColumns.value.find(x => x.id === columnId);
    if (!column) {
      return null;
    }

    const index = column.cards.findIndex(card => card.id === cardId);
    if (index < 0) {
      return null;
    }

    return column.cards[index + 1]?.id ?? null;
  }

  function getFirstVisibleCardId(columnId: number): number | null {
    const column = filteredColumns.value.find(x => x.id === columnId);
    if (!column) {
      return null;
    }

    const movingCardId = draggingCardId.value;
    for (const card of column.cards) {
      if (card.id !== movingCardId) {
        return card.id;
      }
    }

    return null;
  }

  function coerceTargetCardId(columnId: number, candidateTargetCardId: number | null) {
    const movingCardId = draggingCardId.value;
    if (candidateTargetCardId === null || movingCardId === null || candidateTargetCardId !== movingCardId) {
      return candidateTargetCardId;
    }

    return getNextVisibleCardId(columnId, candidateTargetCardId);
  }

  function resolveColumnDropTargetCardId(columnId: number, event: DragEvent): number | null {
    if (draggingCardId.value === null) {
      return null;
    }

    const currentTarget = event.currentTarget;
    if (!(currentTarget instanceof HTMLElement)) {
      return null;
    }

    const cardElements = Array.from(currentTarget.querySelectorAll<HTMLElement>('[data-card-id]'));
    if (cardElements.length === 0) {
      return null;
    }

    for (const element of cardElements) {
      const cardId = Number.parseInt(element.dataset.cardId ?? '', 10);
      if (!Number.isFinite(cardId) || cardId === draggingCardId.value) {
        continue;
      }

      const rect = element.getBoundingClientRect();
      const midpoint = rect.top + (rect.height / 2);
      if (event.clientY <= midpoint) {
        return cardId;
      }
    }

    return null;
  }

  async function dropAt(columnId: number, targetCardId: number | null) {
    try {
      await dropCard(columnId, targetCardId);
    } finally {
      clearDragInteraction();
    }
  }

  return {
    draggingCardId,
    activeDropPoint,
    onCardDragStart,
    onCardDragEnd,
    onCardDragOver,
    onCardDrop,
    handleColumnDragOver,
    handleColumnDrop,
    isDropPoint,
    isDropAtColumnStart,
    resolveCardDropIndicator,
    clearDragInteraction
  };
}
