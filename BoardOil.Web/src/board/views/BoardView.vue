<template>
  <section class="board-view-route-shell">
    <section v-if="board" class="board-view">
      <Teleport defer :to="`#${boardLayoutRegistry.conveyorContentTargetId}`">
        <BoardCardFilters
          embedded
          :search-text="cardSearchText"
          :available-tag-names="availableTagNames"
          :available-slicks="slicks"
          :available-card-types="availableCardTypes"
          :tag-filter-states="tagFilterStates"
          :slick-filter-states="slickFilterStates"
          :card-type-filter-states="cardTypeFilterStates"
          :picker-open="isTagFilterMenuOpen"
          :has-active-filters="hasActiveCardFilters"
          :selection-mode="isCardSelectionMode"
          :selected-count="selectedCardCount"
          :disable-bulk-edit-action="isApplyingBulkEdit || selectedCardCount === 0"
          :disable-selection-menu-action="isApplyingBulkEdit"
          @update:search-text="cardSearchText = $event"
          @update:tag-filter-states="tagFilterStates = $event"
          @update:slick-filter-states="slickFilterStates = $event"
          @update:card-type-filter-states="cardTypeFilterStates = $event"
          @update:picker-open="isTagFilterMenuOpen = $event"
          @clear="clearCardFilters"
          @toggle-selection-mode="toggleCardSelectionMode"
          @open-bulk-edit="openBulkEditDialog"
          @open-bulk-delete="confirmDeleteSelectedCards"
          @invert-selection="invertVisibleSelection"
        />
      </Teleport>

      <BoardSurface
        ref="boardSurface"
        :board-id="board.id"
        :columns="filteredColumns"
        :card-filters="cardFilters"
        :has-active-card-filters="hasActiveCardFilters"
        :selection-mode="isCardSelectionMode"
        :selected-card-ids="selectedCardIds"
        :slick-cohesion-mode-enabled="board.slickCohesionModeEnabled"
        :drop-selected-cards="moveSelectedCardsByDropTarget"
        @edit-card="openCardEditor"
        @toggle-select="toggleCardSelection"
        @select-all-visible="selectAllVisibleInColumn"
        @clear-visible="clearVisibleInColumn"
      />

      <BoardArchiveSelectedCardsDialog
        :open="isArchiveConfirmOpen"
        :selected-cards="selectedCards"
        :selected-count="selectedCardCount"
        :is-archiving="isArchivingSelectedCards"
        @close="closeArchiveConfirm"
        @confirm="confirmArchiveSelectedCards"
      />

      <BoardBulkEditSelectedCardsDialog
        v-if="isBulkEditDialogOpen"
        :key="board.id"
        :board-id="board.id"
        :selected-card-ids="selectedCards.map(card => card.id)"
        :columns="filteredColumns.map(column => ({ id: column.id, title: column.title }))"
        :slicks="slicks"
        @update:busy="onBulkEditBusyChange"
        @close="closeBulkEditDialog"
        @saved="onBulkEditSaved"
      />
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { onBeforeUnmount, onMounted, ref, useTemplateRef, watch } from 'vue';
import { useRouter } from 'vue-router';
import BoardArchiveSelectedCardsDialog from '../components/BoardArchiveSelectedCardsDialog.vue';
import BoardBulkEditSelectedCardsDialog from '../components/BoardBulkEditSelectedCardsDialog.vue';
import BoardCardFilters from '../components/BoardCardFilters.vue';
import BoardSurface from '../components/BoardSurface.vue';
import { useBoardCardFilters } from '../composables/useBoardCardFilters';
import { useBoardCardSelection } from '../composables/useBoardCardSelection';
import { useBoardStore } from '../stores/boardStore';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useSlickStore } from '../stores/slickStore';
import { useTagStore } from '../stores/tagStore';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useBoardLayoutRegistry } from '../../site/layouts/boardLayoutRegistry';

const isBulkEditDialogOpen = ref(false);
const isApplyingBulkEdit = ref(false);

const router = useRouter();
const boardStore = useBoardStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const slickStore = useSlickStore();
const tagStore = useTagStore();
const boardLayoutRegistry = useBoardLayoutRegistry();
const boardSurface = useTemplateRef<InstanceType<typeof BoardSurface>>('boardSurface');

const { board, currentBoardId } = storeToRefs(boardStore);
const { cardTypes } = storeToRefs(cardTypeStore);
const { slicks } = storeToRefs(slickStore);
const { tags } = storeToRefs(tagStore);
const { archiveCards, bulkMoveCards, deleteCards } = cardStore;
const { confirm } = useConfirm();
const {
  cardSearchText,
  tagFilterStates,
  slickFilterStates,
  cardTypeFilterStates,
  isTagFilterMenuOpen,
  availableTagNames,
  availableCardTypes,
  cardFilters,
  filteredColumns,
  hasActiveCardFilters,
  clearCardFilters
} = useBoardCardFilters(board, tags, slicks, cardTypes);

async function openArchivedCards() {
  const boardId = currentBoardId.value!;
  await router.push({ name: 'board-archived', params: { boardId } });
}

const {
  isCardSelectionMode,
  selectedCardIds,
  selectedCards,
  selectedCardCount,
  isArchiveConfirmOpen,
  isArchivingSelectedCards,
  archiveConveyorLabel,
  archiveConveyorAriaLabel,
  archiveConveyorDisabled,
  toggleCardSelectionMode: toggleCardSelectionModeInternal,
  toggleCardSelection,
  selectCardIds,
  unselectCardIds,
  invertCardIds,
  handleArchiveConveyorClick,
  closeArchiveConfirm,
  confirmArchiveSelectedCards,
  moveSelectedCardsByDropTarget,
  resetSelectionState
} = useBoardCardSelection(board, archiveCards, bulkMoveCards, openArchivedCards);
const conveyorRegistration = boardLayoutRegistry.registerConveyor({
  highlighted: false,
  leftLabel: null,
  leftAriaLabel: null,
  leftDisabled: false,
  rightLabel: archiveConveyorLabel.value,
  rightAriaLabel: archiveConveyorAriaLabel.value,
  rightDisabled: archiveConveyorDisabled.value,
  onLeftClick: null,
  onRightClick: handleArchiveConveyorClick
});

watch(
  [isCardSelectionMode, archiveConveyorLabel, archiveConveyorAriaLabel, archiveConveyorDisabled],
  ([highlighted, rightLabel, rightAriaLabel, rightDisabled]) => {
    conveyorRegistration.update({
      highlighted,
      leftLabel: null,
      leftAriaLabel: null,
      leftDisabled: false,
      rightLabel,
      rightAriaLabel,
      rightDisabled,
      onLeftClick: null,
      onRightClick: handleArchiveConveyorClick
    });
  },
  { immediate: true }
);

onBeforeUnmount(() => {
  conveyorRegistration.dispose();
});

function toggleCardSelectionMode() {
  boardSurface.value?.resetInteraction();
  toggleCardSelectionModeInternal();
  if (!isCardSelectionMode.value) {
    closeBulkEditDialog();
  }
}

function selectAllVisibleInColumn(columnId: number) {
  selectCardIds(boardSurface.value?.getVisibleCardIds(columnId) ?? []);
}

function clearVisibleInColumn(columnId: number) {
  unselectCardIds(boardSurface.value?.getVisibleCardIds(columnId) ?? []);
}

function invertVisibleSelection() {
  if (!isCardSelectionMode.value || isApplyingBulkEdit.value) {
    return;
  }

  const visibleCardIds = boardSurface.value?.getVisibleCardIds() ?? [];
  if (visibleCardIds.length === 0) {
    return;
  }

  invertCardIds(visibleCardIds);
}

function openBulkEditDialog() {
  if (!isCardSelectionMode.value || selectedCardCount.value === 0 || isApplyingBulkEdit.value) {
    return;
  }

  isBulkEditDialogOpen.value = true;
}

function closeBulkEditDialog() {
  if (!isApplyingBulkEdit.value) isBulkEditDialogOpen.value = false;
}

function onBulkEditBusyChange(value: boolean) {
  isApplyingBulkEdit.value = value;
}

function onBulkEditSaved() {
  closeBulkEditDialog();
  boardSurface.value?.resetInteraction();
  resetSelectionState();
}

async function confirmDeleteSelectedCards() {
  if (!isCardSelectionMode.value || selectedCardCount.value === 0 || isApplyingBulkEdit.value) {
    return;
  }

  const shouldDelete = await confirm({
    title: 'Delete selected cards',
    message: `Delete ${selectedCardCount.value} selected card${selectedCardCount.value === 1 ? '' : 's'}? This cannot be undone.`,
    confirmLabel: 'Delete',
    danger: true
  });
  if (!shouldDelete) {
    return;
  }

  const cardIds = selectedCards.value.map(card => card.id);
  if (cardIds.length === 0) {
    return;
  }

  isApplyingBulkEdit.value = true;
  try {
    const deleted = await deleteCards(cardIds);
    if (!deleted) {
      return;
    }

    boardSurface.value?.resetInteraction();
    resetSelectionState();
    closeBulkEditDialog();
  } finally {
    isApplyingBulkEdit.value = false;
  }
}

async function openCardEditor(cardId: number) {
  const boardId = currentBoardId.value!;
  await router.push({ name: 'board-card', params: { boardId, cardId } });
}

onMounted(() => {
  initializeView();
});

function initializeView() {
  boardSurface.value?.resetInteraction();
  clearCardFilters();
  resetSelectionState();
  closeBulkEditDialog();
}
</script>

<style scoped>
.board-view-route-shell {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.board-view {
  flex: 1;
  height: 0;
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
</style>
