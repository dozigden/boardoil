<template>
  <section class="entity-rows-page attachments-page" aria-label="Board attachments">
    <header class="entity-rows-header">
      <h2>Attachments</h2>
      <div v-if="isCurrentUserOwner" class="attachment-controls">
        <label>State
          <select v-model="cardState">
            <option value="both">(all)</option>
            <option value="live">Live</option>
            <option value="archived">Archived</option>
          </select>
        </label>
        <label>Sort
          <select v-model="sortOrder">
            <option value="date:desc">Newest first</option>
            <option value="date:asc">Oldest first</option>
            <option value="name:asc">Name (A–Z)</option>
            <option value="name:desc">Name (Z–A)</option>
            <option value="size:desc">Largest first</option>
            <option value="size:asc">Smallest first</option>
          </select>
        </label>
      </div>
    </header>
    <p v-if="!isCurrentUserOwner" class="entity-rows-empty">Owner permission required to view board attachments.</p>
    <template v-else>
      <p v-if="store.error" role="alert">{{ store.error }}</p>
      <template v-if="store.inventory">
        <p class="attachment-totals" aria-label="Attachment totals">
          <strong>{{ store.inventory.totalCount }} {{ store.inventory.totalCount === 1 ? 'attachment' : 'attachments' }}</strong>
          <span :title="`${store.inventory.totalByteLength.toLocaleString()} bytes`">{{ formatAttachmentSize(store.inventory.totalByteLength) }} uploaded files</span>
          <span class="attachment-total-scope">Board total</span>
        </p>
      </template>
      <section v-if="!store.error" class="attachments-grid-wrap">
        <BoGrid
          class="attachments-grid"
          :columns="gridFields"
          :items="store.inventory?.items ?? []"
          :is-loading="store.loading || recoveryOffset !== null"
          :empty-text="emptyText"
          sticky-header="100%"
          :total-count="store.inventory?.matchingCount ?? 0"
          :offset="store.inventory?.offset ?? query.offset"
          :limit="query.limit"
          @previous-page="previousPage"
          @next-page="nextPage"
        >
          <template #cell(id)="{ row }">
            <a class="attachment-cell" :title="String(row.originalFileName)"
              :href="buildApiUrl(`/api/boards/${currentBoardId}/attachments/${row.id}/download`)"
              :download="String(row.originalFileName)" :aria-label="`Download ${row.originalFileName}`"
              @click.left.exact.prevent="store.download(Number(row.id))">{{ row.originalFileName }}</a>
          </template>
          <template #cell(contentType)="{ row }">
            <span class="attachment-cell" :title="String(row.contentType)">{{ row.contentType }}</span>
          </template>
          <template #cell(byteLength)="{ row }">
            <span class="attachment-cell" :title="`${Number(row.byteLength).toLocaleString()} bytes`">{{ formatAttachmentSize(Number(row.byteLength)) }}</span>
          </template>
          <template #cell(createdAtUtc)="{ row }">
            <time class="attachment-cell" :datetime="String(row.createdAtUtc)" :title="formatDate(String(row.createdAtUtc))">{{ formatDate(String(row.createdAtUtc)) }}</time>
          </template>
          <template #cell(cardTitle)="{ row }">
            <RouterLink class="attachment-cell" :title="`#${row.cardId}: ${row.cardTitle}`" :to="cardLocation(asAttachment(row))">#{{ row.cardId }}: {{ row.cardTitle }}</RouterLink>
          </template>
          <template #cell(archived)="{ row }">
            <span class="attachment-cell">{{ row.archived ? 'Archived' : 'Live' }}</span>
          </template>
        </BoGrid>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, watch } from 'vue';
import { storeToRefs } from 'pinia';
import { RouterLink, useRoute, useRouter } from 'vue-router';
import BoGrid from '../../shared/components/BoGrid.vue';
import { useBoardStore } from '../stores/boardStore';
import { useBoardAttachmentInventoryStore } from '../stores/boardAttachmentInventoryStore';
import { formatAttachmentSize } from '../utils/formatAttachmentSize';
import { buildApiUrl } from '../../shared/api/config';
import { defaultBoardAttachmentInventoryQuery, type BoardAttachmentInventoryItem, type BoardAttachmentInventoryQuery } from '../../shared/types/attachmentTypes';

const { currentBoardId, isCurrentUserOwner } = storeToRefs(useBoardStore());
const store = useBoardAttachmentInventoryStore();
const route = useRoute();
const router = useRouter();
const query = computed<BoardAttachmentInventoryQuery>(() => {
  const offset = Number(route.query.offset ?? 0);
  const sort = route.query.sort;
  const direction = route.query.direction;
  const state = route.query.state;
  return {
    ...defaultBoardAttachmentInventoryQuery,
    offset: Number.isSafeInteger(offset) && offset >= 0 ? offset : 0,
    sort: sort === 'name' || sort === 'size' ? sort : 'date',
    direction: direction === 'asc' ? 'asc' : 'desc',
    state: state === 'live' || state === 'archived' ? state : 'both'
  };
});
const cardState = computed({
  get: () => query.value.state,
  set: (state: BoardAttachmentInventoryQuery['state']) => changeQuery({ state, offset: 0 })
});
const sortOrder = computed({
  get: () => `${query.value.sort}:${query.value.direction}`,
  set: (value: string) => {
    const [sort, direction] = value.split(':') as [BoardAttachmentInventoryQuery['sort'], BoardAttachmentInventoryQuery['direction']];
    changeQuery({ sort, direction, offset: 0 });
  }
});
const emptyText = computed(() => {
  if (query.value.state === 'live') { return 'No attachments on live cards.'; }
  if (query.value.state === 'archived') { return 'No attachments on archived cards.'; }
  return 'No attachments on live or archived cards.';
});
const recoveryOffset = computed(() => {
  const inventory = store.inventory;
  if (!inventory || inventory.offset === 0 || inventory.offset < inventory.matchingCount) { return null; }
  // A card can be archived while its owner follows a link from the last page.
  return Math.max(0, Math.ceil(inventory.matchingCount / inventory.limit) - 1) * inventory.limit;
});
watch(recoveryOffset, offset => {
  if (offset !== null) { changeQuery({ offset }); }
});
function changeQuery(changes: Partial<BoardAttachmentInventoryQuery>) {
  const next = { ...query.value, ...changes };
  void router.replace({ query: { offset: String(next.offset), sort: next.sort, direction: next.direction, state: next.state } });
}
function previousPage() {
  if (!store.loading) { changeQuery({ offset: Math.max(0, query.value.offset - query.value.limit) }); }
}
function nextPage() {
  if (!store.loading) { changeQuery({ offset: query.value.offset + query.value.limit }); }
}
const gridFields = [
  { key: 'id', label: 'File', rowKeyColumn: true, width: 'minmax(10rem, 1.4fr)' },
  { key: 'contentType', label: 'Type', width: 'minmax(7rem, 1fr)' },
  { key: 'byteLength', label: 'Size', width: '6rem' },
  { key: 'createdAtUtc', label: 'Uploaded', width: '12rem' },
  { key: 'cardTitle', label: 'Card', width: 'minmax(10rem, 1.4fr)' },
  { key: 'archived', label: 'State', width: '6rem' }
];
function asAttachment(row: Record<string, unknown>) { return row as BoardAttachmentInventoryItem; }
const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
function formatDate(value: string) { return dateFormat.format(new Date(value)); }
function cardLocation(item: BoardAttachmentInventoryItem) {
  return { name: item.archived ? 'board-archived-card' : 'board-card', params: { boardId: currentBoardId.value!, cardId: item.cardId } };
}
watch([currentBoardId, isCurrentUserOwner, query], () => {
  if (currentBoardId.value !== null && isCurrentUserOwner.value) { void store.load(currentBoardId.value, query.value); }
  else { store.clear(); }
}, { immediate: true });
onBeforeUnmount(store.clear);
</script>

<style scoped>
.attachments-page { max-width: none; height: 100%; flex: 1; display: flex; flex-direction: column; min-height: 0; overflow: hidden; }
.attachment-totals { display: flex; flex-wrap: wrap; align-items: center; gap: 0.25rem 1rem; margin: 0; font-size: 0.85rem; }
.attachments-grid-wrap { display: flex; flex-direction: column; flex: 1; min-height: 0; overflow: hidden; }
.attachments-grid { height: 100%; min-height: 0; --bo-grid-cell-padding: 0.35rem 0.6rem; font-size: 0.85rem; }
.attachment-cell { display: block; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.entity-rows-header, .attachment-controls { flex-wrap: wrap; }
.attachment-controls { display: flex; gap: 0.5rem 1rem; font-size: 0.85rem; }
.attachment-controls label { display: flex; align-items: center; gap: 0.4rem; }
.attachment-controls select { padding: 0.25rem 0.4rem; }
.attachment-total-scope { color: var(--bo-ink-muted); }
</style>
