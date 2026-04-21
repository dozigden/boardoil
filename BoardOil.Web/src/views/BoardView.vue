<template>
  <section v-if="isLoadingBoard" class="board-loading" aria-live="polite">
    <span class="board-loading-indicator" aria-hidden="true" />
    <p class="board-loading-label">Loading board...</p>
  </section>

  <section v-else-if="board" class="board-view">
    <BoardConveyor
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
        @update:search-text="cardSearchText = $event"
        @update:filter-states="tagFilterStates = $event"
        @update:picker-open="isTagFilterMenuOpen = $event"
        @clear="clearCardFilters"
        @toggle-selection-mode="toggleCardSelectionMode"
      />
    </BoardConveyor>

    <section class="board">
      <article
        v-for="column in filteredColumns"
        :key="column.id"
        class="column"
        @dragover.prevent="handleColumnDragOver(column.id, $event)"
        @drop.prevent="handleColumnDrop(column.id)"
      >
        <BoardColumnHeader
          :column-id="column.id"
          :title="column.title"
          :count-label="formatColumnCardCount(column.cards.length)"
          :card-types="cardTypes"
          @open-default-card-draft="openDefaultCardDraft"
          @open-card-draft-for-type="openNewCardDraft"
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
    </section>

    <BoardArchiveSelectedCardsDialog
      :open="isArchiveConfirmOpen"
      :selected-cards="selectedCards"
      :selected-count="selectedCardCount"
      :is-archiving="isArchivingSelectedCards"
      @close="closeArchiveConfirm"
      @confirm="confirmArchiveSelectedCards"
    />
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoardArchiveSelectedCardsDialog from '../components/BoardArchiveSelectedCardsDialog.vue';
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
import type { AppError } from '../types/appError';
import { formatColumnCardCount } from '../utils/columnCardCount';

const newCardDraftTitles = ref<Record<number, string>>({});
const newCardDraftCardTypeIds = ref<Record<number, number | null>>({});
const newCardDraftInputs = ref<Record<number, HTMLInputElement | HTMLTextAreaElement | null>>({});
const newCardDraftErrors = ref<Record<number, string>>({});

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const tagStore = useTagStore();

const { board, isLoadingBoard } = storeToRefs(boardStore);
const { cardTypes, systemCardType } = storeToRefs(cardTypeStore);
const { tags } = storeToRefs(tagStore);
const { createCard, startDrag, dropCard, archiveCards } = cardStore;

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
  handleArchiveConveyorClick,
  closeArchiveConfirm,
  confirmArchiveSelectedCards,
  resetSelectionState
} = useBoardCardSelection(board, archiveCards, resolveBoardId, openArchivedCards);

const {
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
} = useBoardCardDragDrop(filteredColumns, isCardSelectionMode, startDrag, dropCard);

function toggleCardSelectionMode() {
  clearDragInteraction();
  toggleCardSelectionModeInternal();
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
  gap: 0.75rem;
}

.board-view :deep(.board-conveyor) {
  margin-inline: 1.5rem;
}

.board {
  --column-min-width: 280px;
  --column-max-width: 360px;
  display: grid;
  grid-auto-flow: column;
  grid-auto-columns: minmax(var(--column-min-width), var(--column-max-width));
  grid-template-rows: 1fr;
  gap: 1rem;
  margin-top: 0;
  align-items: start;
  min-height: 0;
  height: 100%;
  overflow-x: auto;
  overflow-y: hidden;
  overscroll-behavior-x: contain;
  padding-inline: 1.5rem;
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

@media (max-width: 720px) {
  .board-view :deep(.board-conveyor) {
    margin-inline: 0.75rem;
  }

  .board {
    padding-inline: 0.75rem;
  }
}
</style>
