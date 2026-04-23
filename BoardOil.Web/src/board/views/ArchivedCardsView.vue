<template>
  <section class="archived-cards-page">
    <BoardConveyor left-label="Board" left-aria-label="Back to board" @left-click="goToBoard">
      <BoardCardFilters
        embedded
        :search-text="searchDraft"
        :available-tag-names="[]"
        :filter-states="filterStates"
        :picker-open="isTagFilterMenuOpen"
        :has-active-filters="hasActiveFilters"
        :show-selection-toggle="false"
        @update:search-text="handleSearchTextChanged"
        @update:filter-states="filterStates = $event"
        @update:picker-open="isTagFilterMenuOpen = $event"
        @clear="clearSearch"
      />
    </BoardConveyor>

    <section class="archived-cards-grid-region">
      <p v-if="listErrorMessage" class="archived-cards-empty">{{ listErrorMessage }}</p>
      <BoGrid
        v-else
        class="archived-cards-grid"
        :columns="gridFields"
        :items="listItems"
        :is-loading="isLoadingList"
        :empty-text="emptyGridText"
        sticky-header="100%"
        :total-count="totalCount"
        :offset="offsetQuery"
        :limit="PageLimit"
        row-clickable
        @row-clicked="openArchivedCardFromRow"
        @previous-page="goToPreviousPage"
        @next-page="goToNextPage"
      >
        <template #cell(id)="{ row }">
          <span class="archived-card-id">#{{ row.originalCardId }}</span>
        </template>
        <template #cell(title)="{ row }">
          <span class="archived-card-title">{{ row.title }}</span>
        </template>
        <template #cell(tagNames)="{ row }">
          <span v-if="Array.isArray(row.tagNames) && row.tagNames.length > 0" class="archived-card-tags">
            <Tag
              v-for="tagName in row.tagNames"
              :key="`${row.id}-${tagName}`"
              class="archived-card-tag"
              :tagName="tagName"
              enable-fallback
            />
          </span>
          <span v-else class="archived-card-tags-empty">-</span>
        </template>
        <template #cell(archivedAtUtc)="{ row }">
          <span class="archived-card-date">{{ formatDateTime(String(row.archivedAtUtc ?? '')) }}</span>
        </template>
      </BoGrid>
    </section>
  </section>

  <ModalDialog
    :open="isDetailModalOpen"
    :title="detailModalTitle"
    size="fill"
    close-label="Close archived card"
    @close="closeDetailModal"
  >
    <p v-if="isLoadingDetail" class="archived-detail-loading">Loading archived card...</p>
    <p v-else-if="detailErrorMessage" class="archived-detail-error">{{ detailErrorMessage }}</p>
    <ArchivedCardDetailContent
      v-else-if="selectedArchivedCard"
      :archived-card="selectedArchivedCard"
      :column-title="resolveColumnTitle(selectedArchivedCard.card.boardColumnId)"
    />
  </ModalDialog>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoardCardFilters from '../components/BoardCardFilters.vue';
import BoardConveyor from '../components/BoardConveyor.vue';
import BoGrid from '../../shared/components/BoGrid.vue';
import ArchivedCardDetailContent from '../components/ArchivedCardDetailContent.vue';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { createBoardApi } from '../../shared/api/boardApi';
import { useBoardStore } from '../stores/boardStore';
import type { ArchivedCard, ArchivedCardList, ArchivedCardListItem } from '../../shared/types/boardTypes';
import type { TagFilterStateMap } from '../../shared/types/tagFilterTypes';
import Tag from '../components/Tag.vue';
import { useTagStore } from '../stores/tagStore';

const PageLimit = 25;

const api = createBoardApi();
const boardStore = useBoardStore();
const route = useRoute();
const router = useRouter();
const { currentBoardId, board } = storeToRefs(boardStore);
const tagStore = useTagStore();

const searchDraft = ref('');
const filterStates = ref<TagFilterStateMap>({});
const isTagFilterMenuOpen = ref(false);
const archivedCardList = ref<ArchivedCardList | null>(null);
const isLoadingList = ref(true);
const listErrorMessage = ref('');
const isDetailModalOpen = ref(false);
const isLoadingDetail = ref(false);
const detailErrorMessage = ref('');
const selectedArchivedCard = ref<ArchivedCard | null>(null);
const selectedArchivedCardListItem = ref<ArchivedCardListItem | null>(null);
let detailRequestVersion = 0;

const routeBoardId = computed(() => {
  const parsed = Number.parseInt(String(route.params.boardId ?? ''), 10);
  return Number.isFinite(parsed) ? parsed : null;
});
const searchQuery = computed(() => {
  const value = route.query.search;
  return typeof value === 'string' ? value.trim() : '';
});
const offsetQuery = computed(() => {
  const value = Number.parseInt(String(route.query.offset ?? '0'), 10);
  return Number.isFinite(value) && value > 0 ? value : 0;
});
const listItems = computed(() => archivedCardList.value?.items ?? []);
const totalCount = computed(() => archivedCardList.value?.totalCount ?? 0);
const hasActiveFilters = computed(() => searchDraft.value.trim().length > 0);
const emptyGridText = computed(() => hasActiveFilters.value ? 'No archived cards match your filters.' : 'No archived cards found.');
const detailModalTitle = computed(() => selectedArchivedCard.value?.title ?? selectedArchivedCardListItem.value?.title ?? 'Archived Card');

watch(
  routeBoardId,
  async nextBoardId => {
    if (nextBoardId === null) {
      await router.replace({ name: 'boards' });
      return;
    }

    const loaded = await boardStore.initialize(nextBoardId);
    if (!loaded && routeBoardId.value === nextBoardId) {
      await router.replace({ name: 'boards' });
      return;
    }

    searchDraft.value = searchQuery.value;
    await loadArchivedCards();
    await tagStore.loadTags(routeBoardId.value);

    closeDetailModal();
  },
  { immediate: true }
);

watch(
  () => [route.query.search, route.query.offset],
  async () => {
    searchDraft.value = searchQuery.value;
    if (routeBoardId.value === null || currentBoardId.value !== routeBoardId.value) {
      return;
    }

    await loadArchivedCards();
  }
);

let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null;

onBeforeUnmount(() => {
  if (searchDebounceTimer !== null) {
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = null;
  }
});

async function goToBoard() {
  if (routeBoardId.value === null) {
    await router.push({ name: 'boards' });
    return;
  }

  await router.push({ name: 'board', params: { boardId: routeBoardId.value } });
}

function handleSearchTextChanged(value: string) {
  searchDraft.value = value;
  if (searchDebounceTimer !== null) {
    clearTimeout(searchDebounceTimer);
  }

  searchDebounceTimer = setTimeout(() => {
    void updateQuery({ search: value.trim() || null, offset: 0 });
  }, 220);
}

async function clearSearch() {
  if (searchDebounceTimer !== null) {
    clearTimeout(searchDebounceTimer);
    searchDebounceTimer = null;
  }

  searchDraft.value = '';
  await updateQuery({ search: null, offset: 0 });
}

async function goToPreviousPage() {
  const nextOffset = Math.max(0, offsetQuery.value - PageLimit);
  await updateQuery({ offset: nextOffset });
}

async function goToNextPage() {
  await updateQuery({ offset: offsetQuery.value + PageLimit });
}

const gridFields: Array<{
  key: string;
  label: string;
  rowKeyColumn?: boolean;
  width?: string;
  align?: 'end';
}> = [
  { key: 'id', label: 'Id', rowKeyColumn: true, width: '6.5rem' },
  { key: 'title', label: 'Title' },
  { key: 'tagNames', label: 'Tags' },
  { key: 'archivedAtUtc', label: 'Archived', width: '14rem', align: 'end' }
];

function openArchivedCardFromRow(row: Record<string, unknown>) {
  if (typeof row.id !== 'number') {
    return;
  }

  void openArchivedCard(row as ArchivedCardListItem);
}

async function openArchivedCard(item: ArchivedCardListItem) {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    return;
  }

  detailRequestVersion += 1;
  const requestVersion = detailRequestVersion;
  selectedArchivedCardListItem.value = item;
  selectedArchivedCard.value = null;
  detailErrorMessage.value = '';
  isDetailModalOpen.value = true;
  isLoadingDetail.value = true;
  try {
    const result = await api.getArchivedCard(boardId, item.id);
    if (requestVersion !== detailRequestVersion) {
      return;
    }

    if (!result.ok) {
      detailErrorMessage.value = result.error.message;
      return;
    }

    selectedArchivedCard.value = result.data;
  } finally {
    if (requestVersion === detailRequestVersion) {
      isLoadingDetail.value = false;
    }
  }
}

function closeDetailModal() {
  detailRequestVersion += 1;
  isDetailModalOpen.value = false;
  isLoadingDetail.value = false;
  detailErrorMessage.value = '';
  selectedArchivedCard.value = null;
  selectedArchivedCardListItem.value = null;
}

async function loadArchivedCards() {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    return;
  }

  isLoadingList.value = true;
  listErrorMessage.value = '';
  try {
    const result = await api.getArchivedCards(boardId, {
      searchText: searchQuery.value,
      offset: offsetQuery.value,
      limit: PageLimit
    });
    if (!result.ok) {
      listErrorMessage.value = result.error.message;
      archivedCardList.value = null;
      return;
    }

    archivedCardList.value = result.data;
  } finally {
    isLoadingList.value = false;
  }
}

async function updateQuery(changes: { search?: string | null; offset?: number }) {
  const boardId = routeBoardId.value;
  if (boardId === null) {
    return;
  }

  const query: Record<string, string> = {};
  const nextSearch = changes.search === undefined ? searchQuery.value : changes.search ?? '';
  const nextOffset = changes.offset === undefined ? offsetQuery.value : Math.max(0, changes.offset);

  if (nextSearch.length > 0) {
    query.search = nextSearch;
  }
  if (nextOffset > 0) {
    query.offset = String(nextOffset);
  }

  await router.replace({ name: 'board-archived', params: { boardId }, query });
}

function formatDateTime(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(date);
}

function resolveColumnTitle(boardColumnId: number) {
  return board.value?.columns.find(column => column.id === boardColumnId)?.title ?? null;
}
</script>

<style scoped>
.archived-cards-page {
  display: grid;
  max-width: none;
  margin-top: 0.45rem;
  margin-bottom: 1rem;
  margin-inline-end: 0.75rem;
  gap: 0.5rem;
  min-height: 0;
  height: 100%;
  grid-template-rows: auto minmax(0, 1fr);
}

.archived-cards-page :deep(.board-filters) {
  width: 100%;
}

.archived-cards-grid {
  margin-top: 0;
  min-height: 0;
  height: 100%;
}

.archived-cards-grid-region {
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.archived-card-title {
  display: block;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

.archived-card-date {
  display: block;
  color: var(--bo-ink-muted);
  font-size: 0.88rem;
  text-align: right;
  white-space: nowrap;
}

.archived-card-tags {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}

.archived-card-tag {
  border: 1px solid var(--bo-border-soft);
  border-radius: 999px;
  padding: 0.15rem 0.5rem;
  font-size: 0.8rem;
  color: var(--bo-ink-muted);
  background: var(--bo-surface-energy);
}

.archived-card-tags-empty {
  color: var(--bo-ink-subtle);
}

.archived-cards-empty {
  margin: 0;
  color: var(--bo-ink-muted);
}

.archived-detail-loading,
.archived-detail-error {
  margin: 0;
  color: var(--bo-ink-muted);
}

@media (max-width: 720px) {
  .archived-cards-page {
    margin-inline-end: 0.5rem;
  }
}

@media (max-width: 767px) {
  .archived-card-date {
    text-align: left;
  }
}
</style>
