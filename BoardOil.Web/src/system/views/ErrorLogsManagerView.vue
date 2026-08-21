<template>
  <section class="error-logs-view">
    <SystemLogsTabs />

    <header class="error-logs-header">
      <div>
        <p>Application failures retained for 14 days.</p>
      </div>
      <div class="error-logs-toolbar">
        <label class="error-logs-page-size">
          <span>Page size</span>
          <select :value="limit" :disabled="listLoading" @change="onPageSizeChanged">
            <option v-for="option in pageSizeOptions" :key="option" :value="option">
              {{ option }}
            </option>
          </select>
        </label>
        <button type="button" class="btn btn--secondary" :disabled="listLoading" @click="refresh">
          Refresh
        </button>
        <button type="button" class="btn btn--danger" :disabled="listLoading || purging" @click="purgeExpired">
          {{ purging ? 'Purging...' : 'Purge old logs' }}
        </button>
      </div>
    </header>

    <p v-if="listErrorMessage" class="error">{{ listErrorMessage }}</p>

    <section class="error-logs-grid-region">
      <BoGrid
        :columns="gridFields"
        :items="rows"
        :is-loading="listLoading"
        empty-text="No error logs found."
        sticky-header="100%"
        :total-count="totalCount"
        :offset="offset"
        :limit="limit"
        row-clickable
        @row-clicked="openErrorLogFromRow"
        @previous-page="errorLogsStore.goPreviousPage"
        @next-page="errorLogsStore.goNextPage"
      >
        <template #cell(id)="{ row }">
          <span class="error-log-id">#{{ row.id }}</span>
        </template>
        <template #cell(occurredAtUtc)="{ row }">
          <span>{{ formatDate(String(row.occurredAtUtc ?? '')) }}</span>
        </template>
        <template #cell(source)="{ row }">
          <span class="error-log-area">{{ row.source }}</span>
        </template>
        <template #cell(area)="{ row }">
          <span class="error-log-area">{{ row.area }}</span>
        </template>
        <template #cell(message)="{ row }">
          <span class="error-log-message" :title="String(row.message ?? '')">
            {{ row.message }}
          </span>
        </template>
      </BoGrid>
    </section>
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import BoGrid from '../../shared/components/BoGrid.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import SystemLogsTabs from '../components/SystemLogsTabs.vue';
import {
  ERROR_LOG_PAGE_SIZE_OPTIONS,
  useSystemErrorLogsStore
} from '../stores/systemErrorLogsStore';

const router = useRouter();
const { confirm } = useConfirm();
const feedback = useUiFeedbackStore();
const errorLogsStore = useSystemErrorLogsStore();
const {
  listLoading,
  listErrorMessage,
  errorLogs,
  offset,
  limit,
  totalCount
} = storeToRefs(errorLogsStore);
const rows = computed(() => errorLogs.value as unknown as Record<string, unknown>[]);
const purging = ref(false);
const pageSizeOptions = ERROR_LOG_PAGE_SIZE_OPTIONS;

const gridFields = [
  { key: 'id', label: 'Id', rowKeyColumn: true, width: '5.5rem' },
  { key: 'occurredAtUtc', label: 'Occurred', width: '13rem' },
  { key: 'source', label: 'Source', width: '7rem' },
  { key: 'area', label: 'Area', width: '10rem' },
  { key: 'message', label: 'Message', width: 'minmax(18rem, 1fr)' }
];

function openErrorLogFromRow(row: Record<string, unknown>) {
  if (typeof row.id !== 'number') {
    return;
  }

  void router.push({ name: 'system-error-log-details', params: { errorLogId: row.id } });
}

function onPageSizeChanged(event: Event) {
  const target = event.target;
  if (!(target instanceof HTMLSelectElement)) {
    return;
  }

  void errorLogsStore.setPageSize(Number(target.value));
}

function refresh() {
  void errorLogsStore.loadErrorLogs();
}

async function purgeExpired() {
  const accepted = await confirm({
    title: 'Purge old error logs?',
    message: 'Delete error logs older than 14 days? Newer error logs will be kept.',
    confirmLabel: 'Purge old logs',
    danger: true
  });
  if (!accepted) {
    return;
  }

  purging.value = true;
  try {
    const result = await errorLogsStore.purgeExpiredErrorLogs();
    if (result) {
      feedback.showToast('Purged successfully.');
    }
  } finally {
    purging.value = false;
  }
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

onMounted(async () => {
  await errorLogsStore.loadErrorLogs();
});
</script>

<style scoped>
.error-logs-view {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-height: 100%;
}

.error-logs-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
}

.error-logs-header p {
  margin: 0;
}

.error-logs-header p {
  color: var(--bo-ink-muted);
}

.error-logs-toolbar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.55rem;
}

.error-logs-page-size {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: var(--bo-ink-muted);
  font-size: 0.86rem;
  font-weight: 700;
}

.error-logs-page-size select {
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.45rem 0.55rem;
  background: var(--bo-surface-panel);
  color: var(--bo-ink-default);
}

.error-logs-grid-region {
  flex: 1 1 auto;
  min-height: 22rem;
}

.error-log-id,
.error-log-area {
  font-weight: 700;
}

.error-log-message {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 900px) {
  .error-logs-header {
    align-items: stretch;
    flex-direction: column;
  }

  .error-logs-toolbar {
    justify-content: flex-start;
    flex-wrap: wrap;
  }
}

@media (max-width: 560px) {
  .error-logs-toolbar,
  .error-logs-page-size {
    align-items: stretch;
    flex-direction: column;
  }

  .error-logs-toolbar .btn {
    width: 100%;
  }
}
</style>
