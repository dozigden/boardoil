<template>
  <section v-if="isLoadingBoard" class="board-loading" aria-live="polite">
    <span class="board-loading-indicator" aria-hidden="true" />
    <p class="board-loading-label">Loading board...</p>
  </section>

  <section v-else-if="board" class="board-view">
    <BoardCardFilters
      :search-text="cardSearchText"
      :available-tag-names="availableTagNames"
      :filter-states="tagFilterStates"
      :picker-open="isTagFilterMenuOpen"
      :has-active-filters="hasActiveCardFilters"
      @update:search-text="cardSearchText = $event"
      @update:filter-states="tagFilterStates = $event"
      @update:picker-open="isTagFilterMenuOpen = $event"
      @clear="clearCardFilters"
    />

    <section class="board">
      <article
        v-for="column in filteredColumns"
        :key="column.id"
        class="column"
        @dragover.prevent="handleColumnDragOver(column.id, $event)"
        @drop.prevent="handleColumnDrop(column.id)"
      >
        <header class="column-header">
          <div class="column-heading">
            <h2 class="column-name">{{ column.title }}</h2>
            <span class="column-card-count">{{ formatColumnCardCount(column.cards.length) }}</span>
          </div>
          <div class="column-add-card-wrap" :ref="element => setCreateCardTypeMenuRoot(column.id, element)">
            <button
              type="button"
              class="btn btn--secondary column-add-card column-add-card-main"
              aria-label="Add default card"
              title="Add default card"
              @click="openDefaultCardDraft(column.id)"
            >
              <Plus :size="16" aria-hidden="true" />
            </button>
            <button
              v-if="cardTypes.length > 1"
              type="button"
              class="btn btn--secondary column-add-card column-add-card-toggle"
              aria-label="Choose card type"
              title="Choose card type"
              :aria-expanded="createCardTypeMenuColumnId === column.id"
              @click="toggleCreateCardTypeMenu(column.id)"
            >
              <ChevronDown :size="14" aria-hidden="true" />
            </button>

            <section
              v-if="cardTypes.length > 1 && createCardTypeMenuColumnId === column.id"
              class="panel panel--compact column-add-card-menu"
              aria-label="New card type"
            >
              <button
                v-for="cardType in cardTypes"
                :key="cardType.id"
                type="button"
                class="btn btn--menu-item column-add-card-menu-item"
                :title="`New ${cardType.name}`"
                @click="openNewCardDraft(column.id, cardType.id)"
              >
                <span class="column-add-card-menu-item-title">
                  <span v-if="cardType.emoji" aria-hidden="true">{{ cardType.emoji }}</span>
                  <span>{{ cardType.name }}</span>
                </span>
              </button>
            </section>
          </div>
        </header>

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
            @start-drag="onCardDragStart"
            @end-drag="onCardDragEnd"
            @dragover.prevent.stop="onCardDragOver(column.id, card.id, $event)"
            @drop.prevent.stop="onCardDrop(column.id, card.id, $event)"
            @edit-card="openCardEditor"
          />

          <p v-if="hasActiveCardFilters && column.cards.length === 0 && newCardDraftTitles[column.id] === undefined" class="column-filter-empty">
            No matching cards.
          </p>
        </div>
      </article>
    </section>
  </section>
</template>

<script setup lang="ts">
import { ChevronDown, Plus } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoardCardFilters from '../components/BoardCardFilters.vue';
import Card from '../components/Card.vue';
import CreateCardInline from '../components/CreateCardInline.vue';
import { useClickOutside } from '../composables/useClickOutside';
import { useBoardStore } from '../stores/boardStore';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useTagStore } from '../stores/tagStore';
import type { TagFilterState, TagFilterStateMap } from '../types/tagFilterTypes';
import { formatColumnCardCount } from '../utils/columnCardCount';
import { createCardSearchAndTagMatcher } from '../utils/cardFilters';
import type { CardSearchAndTagFilter } from '../utils/cardFilters';

const newCardDraftTitles = ref<Record<number, string>>({});
const newCardDraftCardTypeIds = ref<Record<number, number | null>>({});
const newCardDraftInputs = ref<Record<number, HTMLInputElement | HTMLTextAreaElement | null>>({});
const createCardTypeMenuColumnId = ref<number | null>(null);
const createCardTypeMenuRoots = ref<Record<number, HTMLElement | null>>({});
const cardSearchText = ref('');
const tagFilterStates = ref<TagFilterStateMap>({});
const isTagFilterMenuOpen = ref(false);
const draggingCardId = ref<number | null>(null);
const activeDropPoint = ref<{ columnId: number; targetCardId: number | null } | null>(null);

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const tagStore = useTagStore();
const { board, isLoadingBoard } = storeToRefs(boardStore);
const { cardTypes, systemCardType } = storeToRefs(cardTypeStore);
const { tags } = storeToRefs(tagStore);
const { createCard, startDrag, dropCard } = cardStore;
const activeCreateCardTypeMenuRoot = computed(() => {
  const activeColumnId = createCardTypeMenuColumnId.value;
  if (activeColumnId === null) {
    return null;
  }

  return createCardTypeMenuRoots.value[activeColumnId] ?? null;
});
const defaultCreateCardTypeId = computed(() => systemCardType.value?.id ?? cardTypes.value[0]?.id ?? null);
const availableTagNames = computed(() => tags.value.map(tag => tag.name).sort((left, right) => left.localeCompare(right)));
const includedTagNames = computed(() => availableTagNames.value.filter(tagName => resolveTagFilterState(tagName) === 'include'));
const excludedTagNames = computed(() => availableTagNames.value.filter(tagName => resolveTagFilterState(tagName) === 'exclude'));
const cardFilters = computed<CardSearchAndTagFilter>(() => ({
  searchText: cardSearchText.value,
  includedTagNames: [...includedTagNames.value],
  excludedTagNames: [...excludedTagNames.value]
}));
const filteredColumns = computed(() => {
  if (!board.value) {
    return [];
  }

  const matcher = createCardSearchAndTagMatcher(cardFilters.value);
  return board.value.columns.map(column => ({
    ...column,
    cards: column.cards.filter(card => matcher(card))
  }));
});
const hasActiveCardFilters = computed(() =>
  cardSearchText.value.trim().length > 0
  || includedTagNames.value.length > 0
  || excludedTagNames.value.length > 0
);

useClickOutside(
  activeCreateCardTypeMenuRoot,
  () => {
    createCardTypeMenuColumnId.value = null;
  },
  () => createCardTypeMenuColumnId.value !== null
);

async function openNewCardDraft(columnId: number, cardTypeId: number | null = defaultCreateCardTypeId.value) {
  createCardTypeMenuColumnId.value = null;

  if (newCardDraftTitles.value[columnId] !== undefined) {
    newCardDraftCardTypeIds.value[columnId] = cardTypeId;
    newCardDraftInputs.value[columnId]?.focus();
    return;
  }

  newCardDraftTitles.value[columnId] = '';
  newCardDraftCardTypeIds.value[columnId] = cardTypeId;
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
}

function closeNewCardDraft(columnId: number) {
  delete newCardDraftTitles.value[columnId];
  delete newCardDraftCardTypeIds.value[columnId];
  delete newCardDraftInputs.value[columnId];
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
  await createCard(columnId, title, cardTypeId);
  closeNewCardDraft(columnId);
}

function setCreateCardTypeMenuRoot(columnId: number, element: unknown) {
  createCardTypeMenuRoots.value[columnId] = element instanceof HTMLElement ? element : null;
}

function toggleCreateCardTypeMenu(columnId: number) {
  if (cardTypes.value.length <= 1) {
    return;
  }

  createCardTypeMenuColumnId.value = createCardTypeMenuColumnId.value === columnId ? null : columnId;
}

async function openCardEditor(cardId: number) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await router.push({ name: 'board-card', params: { boardId, cardId } });
}

function onCardDragStart(cardId: number, fromColumnId: number) {
  draggingCardId.value = cardId;
  activeDropPoint.value = null;
  startDrag(cardId, fromColumnId);
}

function onCardDragEnd() {
  clearDragInteraction();
}

function onCardDragOver(columnId: number, cardId: number, event: DragEvent) {
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
  onCardDragOver(columnId, cardId, event);
  const targetCardId = activeDropPoint.value?.columnId === columnId
    ? activeDropPoint.value.targetCardId
    : cardId;
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

function resolveCardDropIndicator(columnId: number, cardId: number): 'none' | 'before' | 'after' {
  if (draggingCardId.value === null || cardId === draggingCardId.value) {
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

function handleColumnDragOver(columnId: number, event: DragEvent) {
  const targetCardId = resolveColumnDropTargetCardId(columnId, event);
  setDropPoint(columnId, targetCardId);
}

async function handleColumnDrop(columnId: number) {
  const targetCardId = activeDropPoint.value?.columnId === columnId
    ? activeDropPoint.value.targetCardId
    : null;
  await dropAt(columnId, targetCardId);
}

async function dropAt(columnId: number, targetCardId: number | null) {
  try {
    await dropCard(columnId, targetCardId);
  } finally {
    clearDragInteraction();
  }
}

function clearCardFilters() {
  cardSearchText.value = '';
  tagFilterStates.value = {};
  isTagFilterMenuOpen.value = false;
}

function resolveTagFilterState(tagName: string): TagFilterState {
  const normalisedTagName = normaliseTagName(tagName);
  if (!normalisedTagName) {
    return 'none';
  }

  return tagFilterStates.value[normalisedTagName] ?? 'none';
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

function normaliseTagName(tagName: string) {
  return tagName.trim().toLocaleLowerCase();
}

function clearDragInteraction() {
  draggingCardId.value = null;
  activeDropPoint.value = null;
  createCardTypeMenuColumnId.value = null;
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

.column-header {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
  align-items: center;
}

.column-heading {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.45rem;
  min-width: 0;
}

.column-name {
  margin: 0;
  font-size: 1rem;
}

.column-card-count {
  flex: 0 0 auto;
  font-size: 0.88rem;
  font-weight: 600;
  color: var(--bo-ink-muted);
  line-height: 1.2;
}

.column-add-card {
  height: 2rem;
  min-height: 2rem;
  padding: 0;
  line-height: 1;
}

.column-add-card-main {
  min-width: 2rem;
}

.column-add-card-main:not(:only-child) {
  border-top-right-radius: 0;
  border-bottom-right-radius: 0;
}

.column-add-card-toggle {
  margin-left: -1px;
  min-width: 1.75rem;
  border-top-left-radius: 0;
  border-bottom-left-radius: 0;
}

.column-add-card-wrap {
  position: relative;
  display: inline-flex;
  align-items: center;
  margin-right: 0.5rem;
}

.column-add-card-menu {
  position: absolute;
  right: 0;
  top: calc(100% + 0.35rem);
  z-index: 5;
  min-width: 12.5rem;
  max-width: min(18rem, 72vw);
  display: grid;
  gap: 0.25rem;
}

.column-add-card-menu-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.column-add-card-menu-item-title {
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
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
  .board {
    padding-inline: 0.75rem;
  }
}
</style>
