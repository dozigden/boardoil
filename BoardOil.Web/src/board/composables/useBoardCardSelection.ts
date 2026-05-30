import { computed, ref, watch, type Ref } from 'vue';
import type { Board, Card as BoardCard } from '../../shared/types/boardTypes';

type ArchiveCardsOperation = (cardIds: number[]) => Promise<boolean>;
type BulkMoveCardsOperation = (
  cardIds: number[],
  targetColumnId: number,
  targetCardId: number | null
) => Promise<boolean>;

type OpenArchivedCardsAction = () => Promise<void>;

export function useBoardCardSelection(
  board: Ref<Board | null>,
  archiveCards: ArchiveCardsOperation,
  bulkMoveCards: BulkMoveCardsOperation,
  openArchivedCards: OpenArchivedCardsAction
) {
  const isCardSelectionMode = ref(false);
  const selectedCardIds = ref<number[]>([]);
  const isArchiveConfirmOpen = ref(false);
  const isArchivingSelectedCards = ref(false);
  const isMovingSelectedCards = ref(false);

  const cardsById = computed(() => {
    if (!board.value) {
      return new Map<number, BoardCard>();
    }

    return board.value.columns.reduce((map, column) => {
      for (const card of column.cards) {
        map.set(card.id, card);
      }

      return map;
    }, new Map<number, BoardCard>());
  });

  const selectedCardIdSet = computed(() => new Set(selectedCardIds.value));

  const selectedCards = computed<BoardCard[]>(() =>
    selectedCardIds.value
      .map(cardId => cardsById.value.get(cardId))
      .filter((card): card is BoardCard => card !== undefined)
  );

  const selectedCardCount = computed(() => selectedCards.value.length);

  const archiveConveyorLabel = computed(() => {
    if (isCardSelectionMode.value && selectedCardCount.value > 0) {
      return `Archive ${selectedCardCount.value}`;
    }

    return 'Archive';
  });

  const archiveConveyorAriaLabel = computed(() => {
    if (isCardSelectionMode.value) {
      return selectedCardCount.value > 0
        ? `Archive ${selectedCardCount.value} selected cards`
        : 'Archive selected cards';
    }

    return 'View archived cards';
  });

  const archiveConveyorDisabled = computed(() =>
    isArchivingSelectedCards.value
    || isMovingSelectedCards.value
    || (isCardSelectionMode.value && selectedCardCount.value === 0)
  );

  function isCardSelected(cardId: number) {
    return selectedCardIdSet.value.has(cardId);
  }

  function clearCardSelection() {
    selectedCardIds.value = [];
  }

  function toggleCardSelectionMode() {
    isCardSelectionMode.value = !isCardSelectionMode.value;

    if (!isCardSelectionMode.value) {
      clearCardSelection();
      isArchiveConfirmOpen.value = false;
    }
  }

  function toggleCardSelection(cardId: number) {
    if (!isCardSelectionMode.value) {
      return;
    }

    if (selectedCardIdSet.value.has(cardId)) {
      selectedCardIds.value = selectedCardIds.value.filter(x => x !== cardId);
      return;
    }

    selectedCardIds.value = [...selectedCardIds.value, cardId];
  }

  function selectCardIds(cardIds: number[]) {
    if (!isCardSelectionMode.value || cardIds.length === 0) {
      return;
    }

    const nextSelectedCardIds = new Set(selectedCardIds.value);
    for (const cardId of cardIds) {
      nextSelectedCardIds.add(cardId);
    }

    selectedCardIds.value = [...nextSelectedCardIds];
  }

  function unselectCardIds(cardIds: number[]) {
    if (!isCardSelectionMode.value || cardIds.length === 0) {
      return;
    }

    const cardIdSet = new Set(cardIds);
    selectedCardIds.value = selectedCardIds.value.filter(cardId => !cardIdSet.has(cardId));
  }

  function invertCardIds(cardIds: number[]) {
    if (!isCardSelectionMode.value || cardIds.length === 0) {
      return;
    }

    const nextSelectedCardIds = new Set(selectedCardIds.value);
    for (const cardId of cardIds) {
      if (nextSelectedCardIds.has(cardId)) {
        nextSelectedCardIds.delete(cardId);
      } else {
        nextSelectedCardIds.add(cardId);
      }
    }

    selectedCardIds.value = [...nextSelectedCardIds];
  }

  function handleArchiveConveyorClick() {
    if (!isCardSelectionMode.value) {
      void openArchivedCards();
      return;
    }

    isArchiveConfirmOpen.value = true;
  }

  function closeArchiveConfirm() {
    if (isArchivingSelectedCards.value || isMovingSelectedCards.value) {
      return;
    }

    isArchiveConfirmOpen.value = false;
  }

  async function confirmArchiveSelectedCards() {
    const cardIds = selectedCards.value.map(card => card.id);
    if (cardIds.length === 0) {
      isArchiveConfirmOpen.value = false;
      return;
    }

    isArchivingSelectedCards.value = true;
    try {
      const archived = await archiveCards(cardIds);
      if (!archived) {
        return;
      }

      isArchiveConfirmOpen.value = false;
      isCardSelectionMode.value = false;
      clearCardSelection();
    } finally {
      isArchivingSelectedCards.value = false;
    }
  }

  async function moveSelectedCardsByDropTarget(targetColumnId: number, targetCardId: number | null) {
    if (!isCardSelectionMode.value || isArchivingSelectedCards.value || isMovingSelectedCards.value) {
      return false;
    }

    const cardIds = selectedCards.value.map(card => card.id);
    if (cardIds.length === 0) {
      return true;
    }

    isMovingSelectedCards.value = true;
    try {
      const moved = await bulkMoveCards(cardIds, targetColumnId, targetCardId);
      if (!moved) {
        return false;
      }

      isCardSelectionMode.value = false;
      clearCardSelection();
      return true;
    } finally {
      isMovingSelectedCards.value = false;
    }
  }

  function resetSelectionState() {
    isCardSelectionMode.value = false;
    clearCardSelection();
    isArchiveConfirmOpen.value = false;
    isArchivingSelectedCards.value = false;
    isMovingSelectedCards.value = false;
  }

  watch(cardsById, currentCardsById => {
    selectedCardIds.value = selectedCardIds.value.filter(cardId => currentCardsById.has(cardId));
  });

  return {
    isCardSelectionMode,
    selectedCardIds,
    selectedCards,
    selectedCardCount,
    isArchiveConfirmOpen,
    isArchivingSelectedCards,
    isMovingSelectedCards,
    archiveConveyorLabel,
    archiveConveyorAriaLabel,
    archiveConveyorDisabled,
    isCardSelected,
    clearCardSelection,
    toggleCardSelectionMode,
    toggleCardSelection,
    selectCardIds,
    unselectCardIds,
    invertCardIds,
    handleArchiveConveyorClick,
    closeArchiveConfirm,
    confirmArchiveSelectedCards,
    moveSelectedCardsByDropTarget,
    resetSelectionState
  };
}
