<template>
  <section class="archived-cards-page">
    <section class="archived-cards-filter-row">
      <h2 class="archived-cards-title">Archived Cards</h2>
      <BoardCardFilters
        :search-text="searchDraft"
        :available-tag-names="[]"
        :filter-states="filterStates"
        :picker-open="isTagFilterMenuOpen"
        :has-active-filters="hasActiveFilters"
        @update:search-text="handleSearchTextChanged"
        @update:filter-states="filterStates = $event"
        @update:picker-open="isTagFilterMenuOpen = $event"
        @clear="clearSearch"
      />
      <button type="button" class="btn btn--secondary" @click="goToBoard">Back To Board</button>
    </section>

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
    <template v-else-if="selectedArchivedCard">
      <section class="archived-detail-meta">
        <p><strong>Archived:</strong> {{ formatDateTime(selectedArchivedCard.archivedAtUtc) }}</p>
        <p><strong>Original card ID:</strong> {{ selectedArchivedCard.originalCardId }}</p>
      </section>
      <section class="archived-detail-snapshot">
        <h4>Snapshot Payload</h4>
        <pre>{{ formattedSnapshotJson }}</pre>
      </section>
    </template>
  </ModalDialog>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onBeforeUnmount, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import BoardCardFilters from '../components/BoardCardFilters.vue';
import BoGrid from '../components/BoGrid.vue';
import ModalDialog from '../components/ModalDialog.vue';
import { createBoardApi } from '../api/boardApi';
import { useBoardStore } from '../stores/boardStore';
import type { ArchivedCard, ArchivedCardList, ArchivedCardListItem } from '../types/boardTypes';
import type { TagFilterStateMap } from '../types/tagFilterTypes';
import Tag from '../components/Tag.vue';
import { useTagStore } from '../stores/tagStore';

const PageLimit = 25;

const api = createBoardApi();
const boardStore = useBoardStore();
const route = useRoute();
const router = useRouter();
const { currentBoardId } = storeToRefs(boardStore);
const tagStore = useTagStore();
const { tags } = storeToRefs(tagStore);

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
const formattedSnapshotJson = computed(() => {
  const snapshotJson = selectedArchivedCard.value?.snapshotJson ?? '';
  if (!snapshotJson) {
    return '';
  }

  try {
    return JSON.stringify(JSON.parse(snapshotJson), null, 2);
  } catch {
    return snapshotJson;
  }
});

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
</script>

<style scoped>
.archived-cards-page {
  display: grid;
  max-width: none;
  margin-top: 0.45rem;
  margin-bottom: 1rem;
  margin-inline-end: 2rem;
  gap: 0.5rem;
  min-height: 0;
  height: 100%;
  grid-template-rows: auto minmax(0, 1fr);
}

.archived-cards-filter-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 0.4rem;
}

.archived-cards-title {
  margin: 0;
  white-space: nowrap;
}

.archived-cards-filter-row :deep(.board-filters) {
  width: 100%;
  margin-inline: 0;
  margin-top: 0;
  gap: 0.45rem 0.65rem;
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

.archived-detail-meta {
  display: grid;
  gap: 0.2rem;
  margin-bottom: 0.75rem;
}

.archived-detail-meta p {
  margin: 0;
}

.archived-detail-snapshot h4 {
  margin: 0 0 0.45rem;
}

.archived-detail-snapshot pre {
  margin: 0;
  padding: 0.75rem;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  background: var(--bo-surface-panel);
  font-size: 0.82rem;
  line-height: 1.45;
  overflow: auto;
}

@media (max-width: 720px) {
  .archived-cards-page {
    margin-inline-end: 0.75rem;
  }
}

@media (max-width: 767px) {
  .archived-card-date {
    text-align: left;
  }
}
</style>
