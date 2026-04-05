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
        @dragover.prevent
        @drop="dropCard(column.id, null)"
      >
        <header class="column-header">
          <div class="column-heading">
            <h2 class="column-name">{{ column.title }}</h2>
            <span class="badge column-card-count">{{ formatColumnCardCount(column.cards.length) }}</span>
          </div>
          <button
            type="button"
            class="btn btn--secondary column-add-card"
            aria-label="Add card"
            title="Add card"
            @click="openNewCardDraft(column.id)"
          >
            <Plus :size="16" aria-hidden="true" />
          </button>
        </header>

        <div class="column-content">
          <article v-if="newCardDraftTitles[column.id] !== undefined" class="create-card-inline">
            <label class="create-card-inline-label">
              Title
              <input
                :ref="element => setNewCardDraftInput(column.id, element)"
                :value="newCardDraftTitles[column.id]"
                type="text"
                maxlength="200"
                placeholder="New card title"
                @input="updateNewCardDraftTitle(column.id, ($event.target as HTMLInputElement).value)"
                @keydown.enter.prevent="saveNewCardDraft(column.id)"
                @keydown.esc.prevent="closeNewCardDraft(column.id)"
              />
            </label>
            <div class="editor-actions create-card-inline-actions">
              <button type="button" class="btn create-card-save" aria-label="Save new card" title="Save new card" @click="saveNewCardDraft(column.id)">
                <Check :size="16" aria-hidden="true" />
              </button>
              <button
                type="button"
                class="btn btn--secondary create-card-cancel"
                aria-label="Cancel new card"
                title="Cancel new card"
                @click="closeNewCardDraft(column.id)"
              >
                <X :size="16" aria-hidden="true" />
              </button>
            </div>
          </article>

          <Card
            v-for="card in column.cards"
            :key="card.id"
            :card="card"
            :column-id="column.id"
            @start-drag="startDrag"
            @drop-card="dropCard"
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
import { Check, Plus, X } from 'lucide-vue-next';
import { storeToRefs } from 'pinia';
import { computed, nextTick, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoardCardFilters from '../components/BoardCardFilters.vue';
import Card from '../components/Card.vue';
import { useBoardStore } from '../stores/boardStore';
import { useCardStore } from '../stores/cardStore';
import { useCardTypeStore } from '../stores/cardTypeStore';
import { useTagStore } from '../stores/tagStore';
import type { TagFilterState, TagFilterStateMap } from '../types/tagFilterTypes';
import { formatColumnCardCount } from '../utils/columnCardCount';
import { createCardSearchAndTagMatcher } from '../utils/cardFilters';
import type { CardSearchAndTagFilter } from '../utils/cardFilters';

const newCardDraftTitles = ref<Record<number, string>>({});
const newCardDraftInputs = ref<Record<number, HTMLInputElement | null>>({});
const cardSearchText = ref('');
const tagFilterStates = ref<TagFilterStateMap>({});
const isTagFilterMenuOpen = ref(false);

const route = useRoute();
const router = useRouter();
const boardStore = useBoardStore();
const cardStore = useCardStore();
const cardTypeStore = useCardTypeStore();
const tagStore = useTagStore();
const { board, isLoadingBoard } = storeToRefs(boardStore);
const { tags } = storeToRefs(tagStore);
const { createCard, startDrag, dropCard } = cardStore;
const availableTagNames = computed(() => {
  const mergedTagNames = tags.value.map(tag => tag.name);
  for (const column of board.value?.columns ?? []) {
    for (const card of column.cards) {
      mergedTagNames.push(...card.tagNames);
    }
  }

  return dedupeTagNames(mergedTagNames).sort((left, right) => left.localeCompare(right));
});
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

async function openNewCardDraft(columnId: number) {
  if (newCardDraftTitles.value[columnId] !== undefined) {
    newCardDraftInputs.value[columnId]?.focus();
    return;
  }

  newCardDraftTitles.value[columnId] = '';
  await nextTick();
  newCardDraftInputs.value[columnId]?.focus();
}

function updateNewCardDraftTitle(columnId: number, value: string) {
  if (newCardDraftTitles.value[columnId] === undefined) {
    return;
  }

  newCardDraftTitles.value[columnId] = value;
}

function closeNewCardDraft(columnId: number) {
  delete newCardDraftTitles.value[columnId];
  delete newCardDraftInputs.value[columnId];
}

function setNewCardDraftInput(columnId: number, element: unknown) {
  newCardDraftInputs.value[columnId] = element instanceof HTMLInputElement ? element : null;
}

async function saveNewCardDraft(columnId: number) {
  const title = newCardDraftTitles.value[columnId] ?? '';
  if (!title.trim()) {
    return;
  }

  await createCard(columnId, title);
  closeNewCardDraft(columnId);
}

async function openCardEditor(cardId: number) {
  const boardId = resolveBoardId();
  if (boardId === null) {
    return;
  }

  await router.push({ name: 'board-card', params: { boardId, cardId } });
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

function dedupeTagNames(tagNames: string[]) {
  const deduped: string[] = [];
  const seen = new Set<string>();
  for (const tagName of tagNames) {
    const normalisedTagName = normaliseTagName(tagName);
    if (!normalisedTagName || seen.has(normalisedTagName)) {
      continue;
    }

    seen.add(normalisedTagName);
    deduped.push(tagName.trim());
  }

  return deduped;
}

function normaliseTagName(tagName: string) {
  return tagName.trim().toLocaleLowerCase();
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
}

.column-add-card {
  padding: 0.3rem;
  margin-right: 0.5rem;
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

.create-card-inline {
  border: 1px solid var(--bo-border-soft);
  border-radius: 12px;
  padding: 0.6rem;
  background: var(--bo-surface-base);
  margin-bottom: 0.5rem;
  display: grid;
  gap: 0.45rem;
}

.create-card-inline-label {
  display: grid;
  gap: 0.25rem;
  font-size: 0.85rem;
}

.create-card-inline-actions {
  justify-content: flex-end;
}

.create-card-save,
.create-card-cancel {
  padding: 0.4rem;
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
