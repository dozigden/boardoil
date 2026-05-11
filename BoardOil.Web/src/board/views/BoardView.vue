<template>
  <section v-if="isLoadingBoard" class="board-loading" aria-live="polite">
    <span class="board-loading-indicator" aria-hidden="true" />
    <p class="board-loading-label">Loading board...</p>
  </section>

  <section v-else-if="board" class="board-view">
    <BoardConveyor
      :highlighted="isCardSelectionMode"
      :right-label="archiveConveyorLabel"
      :right-aria-label="archiveConveyorAriaLabel"
      :right-disabled="archiveConveyorDisabled"
      @right-click="handleArchiveConveyorClick"
    >
      <BoardCardFilters
        embedded
        :search-text="cardSearchText"
        :available-tag-names="availableTagNames"
        :filter-states="tagFilterStates"
        :picker-open="isTagFilterMenuOpen"
        :has-active-filters="hasActiveCardFilters"
        :selection-mode="isCardSelectionMode"
        :selected-count="selectedCardCount"
        :disable-bulk-edit-action="isApplyingBulkEdit || selectedCardCount === 0"
        :disable-selection-menu-action="isApplyingBulkEdit"
        @update:search-text="cardSearchText = $event"
        @update:filter-states="tagFilterStates = $event"
        @update:picker-open="isTagFilterMenuOpen = $event"
        @clear="clearCardFilters"
        @toggle-selection-mode="toggleCardSelectionMode"
        @open-bulk-edit="openBulkEditDialog"
        @open-bulk-delete="confirmDeleteSelectedCards"
        @invert-selection="invertVisibleSelection"
      />
    </BoardConveyor>

    <section class="board">
      <div v-for="column in filteredColumns" :key="column.id" class="column-stack">
        <article
          class="column"
          @dragover.prevent="handleColumnDragOver(column.id, $event)"
          @drop.prevent="handleColumnDrop(column.id)"
        >
          <BoardColumnHeader
            :column-id="column.id"
            :title="column.title"
            :count-label="formatColumnCardCount(column.cards.length)"
            :card-types="cardTypes"
            :selection-mode="isCardSelectionMode"
            :disable-select-all="!canSelectAllVisibleInColumn(column.id)"
            :disable-clear-visible="!canClearVisibleInColumn(column.id)"
            @open-default-card-draft="openDefaultCardDraft"
            @open-card-draft-for-type="openNewCardDraft"
            @select-all-visible="selectAllVisibleInColumn"
            @clear-visible="clearVisibleInColumn"
          />

          <div
            class="column-content"
            :class="{
              'column-content--drop-tail': isDropPoint(column.id, null),
              'column-content--drop-head': isDropAtColumnStart(column.id)
            }"
          >
            <CreateCardInline
              v-if="newCardDraftTitles[column.id] !== undefined"
              :title="newCardDraftTitles[column.id] ?? ''"
              :card-type-id="newCardDraftCardTypeIds[column.id] ?? defaultCreateCardTypeId"
              :error-message="newCardDraftErrors[column.id] ?? ''"
              :input-ref="element => setNewCardDraftInput(column.id, element)"
              @update:title="updateNewCardDraftTitle(column.id, $event)"
              @save="saveNewCardDraft(column.id)"
              @cancel="closeNewCardDraft(column.id)"
            />

            <Card
              v-for="card in column.cards"
              :key="card.id"
              :card="card"
              :column-id="column.id"
              :data-card-id="card.id"
              :drop-indicator="resolveCardDropIndicator(column.id, card.id)"
              :selection-mode="isCardSelectionMode"
              :selected="isCardSelected(card.id)"
              :selected-count="selectedCardCount"
              @start-drag="onCardDragStart"
              @end-drag="onCardDragEnd"
              @dragover.prevent.stop="onCardDragOver(column.id, card.id, $event)"
              @drop.prevent.stop="onCardDrop(column.id, card.id, $event)"
              @edit-card="openCardEditor"
              @toggle-select="toggleCardSelection"
            />

            <p v-if="hasActiveCardFilters && column.cards.length === 0 && newCardDraftTitles[column.id] === undefined" class="column-filter-empty">
              No matching cards.
            </p>
          </div>
        </article>

        <div
          class="column-tail-drop-zone"
          :class="{ 'column-tail-drop-zone--active': isDropPoint(column.id, null) }"
          aria-hidden="true"
          @dragover.prevent.stop="onColumnTailDragOver(column.id)"
          @drop.prevent.stop="onColumnTailDrop(column.id)"
        />
      </div>
    </section>

    <BoardArchiveSelectedCardsDialog
      :open="isArchiveConfirmOpen"
      :selected-cards="selectedCards"
      :selected-count="selectedCardCount"
      :is-archiving="isArchivingSelectedCards"
      @close="closeArchiveConfirm"
      @confirm="confirmArchiveSelectedCards"
    />

    <BoardBulkEditSelectedCardsDialog
      :open="isBulkEditDialogOpen"
      :selected-count="selectedCardCount"
      :is-saving="isApplyingBulkEdit"
      :available-tag-names="availableTagNames"
      :columns="filteredColumns.map(column => ({ id: column.id, title: column.title }))"
      :filter-states="bulkEditTagStates"
      :target-column-id="bulkEditTargetColumnId"
      :has-changes="hasBulkEditChanges"
      @update:filter-states="bulkEditTagStates = $event"
      @update:target-column-id="bulkEditTargetColumnId = $event"
      @close="closeBulkEditDialog"
      @confirm="confirmBulkEdit"
    />
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoardArchiveSelectedCardsDialog from '../components/BoardArchiveSelectedCardsDialog.vue';
import BoardBulkEditSelectedCardsDialog from '../components/BoardBulkEditSelectedCardsDialog.vue';
import BoardCardFilters from '../components/BoardCardFilters.vue';
import BoardColumnHeader from '../components/BoardColumnHeader.vue';
import BoardConveyor from '../components/BoardConveyor.vue';
import Card from '../components/Card.vue';
import CreateCardInline from '../components/CreateCardInline.vue';
import { useBoardCardDragDrop } from '../composables/useBoardCardDragDrop';
import { useBoardCardFilters } from '../composables/useBoardCardFilters';
import { useBoardCardSelection } from '../composables/useBoardCardSelection';
import { useBoardStore } from '../stores/boardStore';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useTagStore } from '../stores/tagStore';
import type { AppError } from '../../shared/types/appError';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import { formatColumnCardCount } from '../utils/columnCardCount';
import { useConfirm } from '../../shared/composables/useConfirm';

const newCardDraftTitles = ref<Record<number, string>>({});
const newCardDraftCardTypeIds = ref<Record<number, number | null>>({});
const newCardDraftInputs = ref<Record<number, HTMLInputElement | HTMLTextAreaElement | null>>({});
const newCardDraftErrors = ref<Record<number, string>>({});
const isBulkEditDialogOpen = ref(false);
const isApplyingBulkEdit = ref(false);
const bulkEditTagStates = ref<TagFilterStateMap>({});
const bulkEditTargetColumnId = ref<number | null>(null);

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const tagStore = useTagStore();

const { board, isLoadingBoard } = storeToRefs(boardStore);
const { cardTypes, systemCardType } = storeToRefs(cardTypeStore);
const { tags } = storeToRefs(tagStore);
const { createCard, startDrag, dropCard, archiveCards, bulkMoveCards, bulkEditCards, deleteCards } = cardStore;
const { confirm } = useConfirm();

const defaultCreateCardTypeId = computed(() => systemCardType.value?.id ?? cardTypes.value[0]?.id ?? null);

const {
  cardSearchText,
  tagFilterStates,
  isTagFilterMenuOpen,
  availableTagNames,
  filteredColumns,
  hasActiveCardFilters,
  clearCardFilters
} = useBoardCardFilters(board, tags);

async function openArchivedCards() {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

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
  isCardSelected,
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
} = useBoardCardSelection(board, archiveCards, bulkMoveCards, resolveBoardId, openArchivedCards);

const {
  onCardDragStart,
  onCardDragEnd,
  onCardDragOver,
  onCardDrop,
  onColumnTailDragOver,
  onColumnTailDrop,
  handleColumnDragOver,
  handleColumnDrop,
  isDropPoint,
  isDropAtColumnStart,
  resolveCardDropIndicator,
  clearDragInteraction
} = useBoardCardDragDrop(
  filteredColumns,
  isCardSelectionMode,
  selectedCardIds,
  startDrag,
  dropCard,
  moveSelectedCardsByDropTarget
);

function toggleCardSelectionMode() {
  clearDragInteraction();
  toggleCardSelectionModeInternal();
  if (!isCardSelectionMode.value) {
    closeBulkEditDialog();
  }
}

const hasBulkEditChanges = computed(() =>
  bulkEditTargetColumnId.value !== null
  || Object.keys(bulkEditTagStates.value).length > 0
);

function selectAllVisibleInColumn(columnId: number) {
  const column = filteredColumns.value.find(x => x.id === columnId);
  if (!column) {
    return;
  }

  selectCardIds(column.cards.map(card => card.id));
}

function clearVisibleInColumn(columnId: number) {
  const column = filteredColumns.value.find(x => x.id === columnId);
  if (!column) {
    return;
  }

  unselectCardIds(column.cards.map(card => card.id));
}

function invertVisibleSelection() {
  if (!isCardSelectionMode.value || isApplyingBulkEdit.value) {
    return;
  }

  const visibleCardIds = filteredColumns.value
    .flatMap(column => column.cards.map(card => card.id));
  if (visibleCardIds.length === 0) {
    return;
  }

  invertCardIds(visibleCardIds);
}

function canSelectAllVisibleInColumn(columnId: number) {
  const column = filteredColumns.value.find(x => x.id === columnId);
  if (!column || column.cards.length === 0) {
    return false;
  }

  return column.cards.some(card => !isCardSelected(card.id));
}

function canClearVisibleInColumn(columnId: number) {
  const column = filteredColumns.value.find(x => x.id === columnId);
  if (!column || column.cards.length === 0) {
    return false;
  }

  return column.cards.some(card => isCardSelected(card.id));
}

function openBulkEditDialog() {
  if (!isCardSelectionMode.value || selectedCardCount.value === 0 || isApplyingBulkEdit.value) {
    return;
  }

  isBulkEditDialogOpen.value = true;
}

function closeBulkEditDialog(force = false) {
  if (isApplyingBulkEdit.value && !force) {
    return;
  }

  isBulkEditDialogOpen.value = false;
  bulkEditTagStates.value = {};
  bulkEditTargetColumnId.value = null;
}

async function confirmBulkEdit() {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  const cardIds = selectedCards.value.map(card => card.id);
  if (cardIds.length === 0) {
    closeBulkEditDialog();
    return;
  }

  const addTagNames = resolveTagNamesForState('include');
  const removeTagNames = resolveTagNamesForState('exclude');
  isApplyingBulkEdit.value = true;
  try {
    const edited = await bulkEditCards(
      cardIds,
      {
        moveTargetColumnId: bulkEditTargetColumnId.value,
        moveTargetCardId: null,
        addTagNames,
        removeTagNames
      },
      boardId
    );
    if (!edited) {
      return;
    }

    closeBulkEditDialog(true);
    clearDragInteraction();
    toggleCardSelectionModeInternal();
  } finally {
    isApplyingBulkEdit.value = false;
  }
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

  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  const cardIds = selectedCards.value.map(card => card.id);
  if (cardIds.length === 0) {
    return;
  }

  isApplyingBulkEdit.value = true;
  try {
    const deleted = await deleteCards(cardIds, boardId);
    if (!deleted) {
      return;
    }

    clearDragInteraction();
    resetSelectionState();
    closeBulkEditDialog();
  } finally {
    isApplyingBulkEdit.value = false;
  }
}

function resolveTagNamesForState(state: 'exclude' | 'include') {
  const namesByNormalised = new Map(
    availableTagNames.value.map(tagName => [tagName.trim().toLocaleLowerCase(), tagName] as const)
  );
  const tagNames: string[] = [];
  for (const [normalisedName, tagState] of Object.entries(bulkEditTagStates.value)) {
    if (tagState !== state) {
      continue;
    }

    const tagName = namesByNormalised.get(normalisedName);
    if (tagName) {
      tagNames.push(tagName);
    }
  }

  return tagNames;
}

async function openNewCardDraft(columnId: number, cardTypeId: number | null = defaultCreateCardTypeId.value) {
  if (newCardDraftTitles.value[columnId] !== undefined) {
    newCardDraftCardTypeIds.value[columnId] = cardTypeId;
    delete newCardDraftErrors.value[columnId];
    newCardDraftInputs.value[columnId]?.focus();
    return;
  }

  newCardDraftTitles.value[columnId] = '';
  newCardDraftCardTypeIds.value[columnId] = cardTypeId;
  delete newCardDraftErrors.value[columnId];
  await nextTick();
  newCardDraftInputs.value[columnId]?.focus();
}

function openDefaultCardDraft(columnId: number) {
  void openNewCardDraft(columnId, defaultCreateCardTypeId.value);
}

function updateNewCardDraftTitle(columnId: number, value: string) {
  if (newCardDraftTitles.value[columnId] === undefined) {
    return;
  }

  newCardDraftTitles.value[columnId] = value;
  if (newCardDraftErrors.value[columnId]) {
    delete newCardDraftErrors.value[columnId];
  }
}

function closeNewCardDraft(columnId: number) {
  delete newCardDraftTitles.value[columnId];
  delete newCardDraftCardTypeIds.value[columnId];
  delete newCardDraftInputs.value[columnId];
  delete newCardDraftErrors.value[columnId];
}

function setNewCardDraftInput(columnId: number, element: unknown) {
  newCardDraftInputs.value[columnId] = element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement
    ? element
    : null;
}

async function saveNewCardDraft(columnId: number) {
  const title = newCardDraftTitles.value[columnId] ?? '';
  if (!title.trim()) {
    return;
  }

  const cardTypeId = newCardDraftCardTypeIds.value[columnId] ?? defaultCreateCardTypeId.value;
  const result = await createCard(columnId, title, cardTypeId);
  if (!result || result.ok) {
    closeNewCardDraft(columnId);
    return;
  }

  newCardDraftErrors.value[columnId] = resolveCreateCardErrorMessage(result.error);
  newCardDraftInputs.value[columnId]?.focus();
}

async function openCardEditor(cardId: number) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await router.push({ name: 'board-card', params: { boardId, cardId } });
}

function resolveCreateCardErrorMessage(error: AppError) {
  const validationErrors = error.validationErrors ?? {};
  const titleErrors = validationErrors.title ?? validationErrors[''] ?? [];
  if (titleErrors.length > 0) {
    return titleErrors[0]!;
  }

  return error.message;
}

function resolveBoardId() {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
}

watch(
  () => route.params.boardId,
  async () => {
    clearDragInteraction();
    clearCardFilters();
    resetSelectionState();
    closeBulkEditDialog();

    const boardId = resolveBoardId();
    if (boardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    const loaded = await boardStore.initialize(boardId);
    if (!loaded && resolveBoardId() === boardId) {
      await router.replace({ name: 'boards' });
      return;
    }

    await tagStore.loadTags(boardId);
    await cardTypeStore.loadCardTypes(boardId);
  },
  { immediate: true }
);
</script>

<style scoped>
@keyframes bo-spin {
  to {
    transform: rotate(360deg);
  }
}

.board-view {
  flex: 1;
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  gap: var(--bo-standard-gap);
}

.board {
  --column-min-width: 280px;
  --column-max-width: 360px;
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(var(--column-min-width), var(--column-max-width));
  grid-template-rows: 1fr;
  gap: var(--bo-standard-gap);
  margin-top: 0;
  align-items: stretch;
  min-height: 0;
  height: 100%;
  overflow-x: auto;
  overflow-y: hidden;
  overscroll-behavior-x: contain;
  padding-inline: 0;
  padding-bottom: 0;
  flex: 1;
}

.board-loading {
  flex: 1;
  min-height: 0;
  display: grid;
  place-items: center;
  align-content: center;
  justify-items: center;
  gap: 0.75rem;
  padding: 1.5rem;
}

.board-loading-indicator {
  width: 2rem;
  height: 2rem;
  border-radius: 50%;
  border: 3px solid color-mix(in srgb, var(--bo-border-default) 55%, transparent);
  border-top-color: var(--bo-colour-brand);
  animation: bo-spin 0.85s linear infinite;
}

.board-loading-label {
  margin: 0;
  color: var(--bo-ink-muted);
}

.column {
  background: var(--bo-surface-panel);
  border: 1px solid var(--bo-border-soft);
  border-radius: 14px;
  padding: 0.75rem 0.25rem 0.75rem 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-height: 0;
  height: auto;
  max-height: 100%;
  overflow: hidden;
}

.column-stack {
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.column-content {
  display: flex;
  flex-direction: column;
  flex: 1 1 auto;
  gap: 0.5rem;
  min-height: 0;
  overflow-y: auto;
  padding-right: 0.5rem;
  overscroll-behavior-y: contain;
  scrollbar-width: none;
  position: relative;
}

.column-content--drop-tail {
  box-shadow: inset 0 0 0 2px color-mix(in srgb, var(--bo-focus-ring) 45%, transparent);
  border-radius: 10px;
}

.column-content--drop-head::before {
  content: '';
  position: absolute;
  left: 0.25rem;
  right: 0.75rem;
  top: 0.1rem;
  height: 4px;
  border-radius: 999px;
  background: var(--bo-focus-ring);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--bo-focus-ring) 30%, transparent);
  pointer-events: none;
  z-index: 2;
}

.column-content > .card {
  margin-bottom: 0;
}

.column-content:hover,
.column-content:focus-within {
  scrollbar-width: thin;
  scrollbar-color: var(--bo-border-default) transparent;
}

.column-content::-webkit-scrollbar {
  width: 0;
}

.column-content::-webkit-scrollbar-track {
  background: transparent;
}

.column-content:hover::-webkit-scrollbar,
.column-content:focus-within::-webkit-scrollbar {
  width: 0.55rem;
}

.column-content::-webkit-scrollbar-thumb {
  background: transparent;
  border: 2px solid transparent;
  background-clip: content-box;
  border-radius: 999px;
}

.column-content:hover::-webkit-scrollbar-thumb,
.column-content:focus-within::-webkit-scrollbar-thumb {
  background: color-mix(in srgb, var(--bo-border-default) 78%, transparent);
}

.column-filter-empty {
  margin: 0;
  color: var(--bo-ink-subtle);
  font-size: 0.85rem;
  text-align: center;
  padding: 0.55rem;
}

.column-tail-drop-zone {
  flex: 1 1 auto;
  min-height: 0.25rem;
  border-radius: 10px;
  border: 1px dashed transparent;
  transition: background-color 120ms ease, border-color 120ms ease;
}

.column-tail-drop-zone--active {
  background: color-mix(in srgb, var(--bo-focus-ring) 14%, transparent);
  border-color: color-mix(in srgb, var(--bo-focus-ring) 60%, transparent);
}

@media (max-width: 720px) {
  .board {
    padding-inline: 0;
  }
}
</style>
