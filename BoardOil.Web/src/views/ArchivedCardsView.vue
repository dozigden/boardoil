<template>
  <section class="entity-rows-page archived-cards-page">
    <header class="entity-rows-header archived-cards-header">
      <div class="archived-cards-heading">
        <h2>Archived Cards</h2>
      </div>
      <button type="button" class="btn btn--secondary" @click="goToBoard">Back To Board</button>
    </header>

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

    <div class="archived-cards-meta">
      <p class="archived-cards-range">
        <template v-if="totalCount > 0">
          Showing {{ rangeStart }}-{{ rangeEnd }} of {{ totalCount }}
        </template>
        <template v-else>
          No archived cards found.
        </template>
      </p>
    </div>

    <p v-if="isLoadingList" class="entity-rows-empty">Loading archived cards...</p>
    <p v-else-if="listErrorMessage" class="entity-rows-empty">{{ listErrorMessage }}</p>

    <section v-else-if="listItems.length > 0" class="entity-rows-list archived-cards-list">
      <article v-for="item in listItems" :key="item.id" class="entity-row">
        <button
          type="button"
          class="entity-row-main-button archived-card-row-main"
          :aria-label="`Open archived card ${item.title}`"
          @click="openArchivedCard(item)"
        >
          <span class="archived-card-row-header">
            <span class="entity-row-title">#{{ item.originalCardId }} {{ item.title }}</span>
            <span class="archived-card-row-meta">Archived {{ formatDateTime(item.archivedAtUtc) }}</span>
          </span>
          <span v-if="item.tagNames.length > 0" class="archived-card-tags">
            <span v-for="tagName in item.tagNames" :key="`${item.id}-${tagName}`" class="archived-card-tag">{{ tagName }}</span>
          </span>
        </button>
      </article>
    </section>

    <p v-else class="entity-rows-empty">
      No archived cards match your filters.
    </p>

    <footer class="archived-cards-pagination">
      <button type="button" class="btn btn--secondary" :disabled="!canGoPrevious" @click="goToPreviousPage">Previous</button>
      <button type="button" class="btn btn--secondary" :disabled="!canGoNext" @click="goToNextPage">Next</button>
    </footer>
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
import ModalDialog from '../components/ModalDialog.vue';
import { createBoardApi } from '../api/boardApi';
import { useBoardStore } from '../stores/boardStore';
import type { ArchivedCard, ArchivedCardList, ArchivedCardListItem } from '../types/boardTypes';
import type { TagFilterStateMap } from '../types/tagFilterTypes';

const PageLimit = 25;

const api = createBoardApi();
const boardStore = useBoardStore();
const route = useRoute();
const router = useRouter();
const { currentBoardId } = storeToRefs(boardStore);

const searchDraft = ref('');
const filterStates = ref<TagFilterStateMap>({});
const isTagFilterMenuOpen = ref(false);
const archivedCardList = ref<ArchivedCardList | null>(null);
const isLoadingList = ref(false);
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
const rangeStart = computed(() => (totalCount.value === 0 ? 0 : offsetQuery.value + 1));
const rangeEnd = computed(() => {
  if (totalCount.value === 0) {
    return 0;
  }

  return Math.min(offsetQuery.value + listItems.value.length, totalCount.value);
});
const canGoPrevious = computed(() => offsetQuery.value > 0);
const canGoNext = computed(() => offsetQuery.value + listItems.value.length < totalCount.value);
const hasActiveFilters = computed(() => searchDraft.value.trim().length > 0);
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
  max-width: none;
  margin-top: 0.45rem;
  margin-inline-end: 2rem;
}

.archived-cards-header {
  align-items: flex-end;
}

.archived-cards-heading h2 {
  margin: 0;
}

.archived-cards-page :deep(.board-filters) {
  margin-inline: 0;
  margin-top: 0;
}

.archived-cards-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.archived-cards-range {
  margin: 0;
  color: var(--bo-ink-muted);
}

.archived-cards-list {
  margin-top: 0;
}

.archived-card-row-main {
  display: grid;
  gap: 0.35rem;
  width: 100%;
}

.archived-card-row-header {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  align-items: baseline;
  gap: 0.75rem;
  min-width: 0;
  width: 100%;
}

.archived-card-row-header .entity-row-title {
  display: block;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.archived-card-row-meta {
  color: var(--bo-ink-muted);
  font-size: 0.88rem;
  justify-self: end;
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

.archived-cards-pagination {
  margin-top: 0.85rem;
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
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

  .archived-cards-header {
    align-items: stretch;
  }

  .archived-cards-meta {
    flex-direction: column;
    align-items: flex-start;
  }

}
</style>
