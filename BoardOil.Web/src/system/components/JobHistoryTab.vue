<template>
  <section class="job-history-tab" aria-label="Job history">
    <header class="jobs-header">
      <div class="jobs-toolbar">
        <label class="jobs-page-size"><span>Page size</span>
          <select :value="limit" :disabled="listLoading" @change="onPageSizeChanged">
            <option v-for="size in JOB_PAGE_SIZE_OPTIONS" :key="size" :value="size">{{ size }}</option>
          </select>
        </label>
        <button type="button" class="btn btn--secondary" :disabled="listLoading" @click="refresh">Refresh</button>
      </div>
    </header>
    <p v-if="jobWarning" class="jobs-warning" role="status">{{ jobWarning }}</p>
    <p v-if="listError" class="error" role="alert">{{ listError }}</p>
    <div class="jobs-grid-region">
      <BoGrid :columns="columns" :items="rows" :is-loading="listLoading"
        empty-text="No jobs found." sticky-header="100%" :total-count="totalCount"
        :offset="offset" :limit="limit" row-clickable
        @row-clicked="openRow" @previous-page="store.goPreviousPage" @next-page="store.goNextPage">
        <template #cell(id)="{ row }"><strong>#{{ row.id }}</strong></template>
        <template #cell(type)="{ row }"><span :title="String(row.type)">{{ jobTypeLabel(String(row.type)) }}</span></template>
        <template #cell(status)="{ row }"><span class="badge" :class="statusClass(String(row.status))">{{ row.status }}</span></template>
        <template #cell(startedAtUtc)="{ row }">{{ formatJobDate(row.startedAtUtc) }}</template>
        <template #cell(completedAtUtc)="{ row }">{{ formatJobDate(row.completedAtUtc) }}</template>
      </BoGrid>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue';
import { storeToRefs } from 'pinia';
import { useRouter } from 'vue-router';
import BoGrid from '../../shared/components/BoGrid.vue';
import { JOB_PAGE_SIZE_OPTIONS, useSystemJobsStore } from '../stores/systemJobsStore';
import { formatJobDate, jobTypeLabel, statusClass } from '../utils/jobPresentation';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

const router = useRouter();
const store = useSystemJobsStore();
const feedback = useUiFeedbackStore();
const { warningMessage } = storeToRefs(feedback);
const jobWarning = computed(() => warningMessage.value.startsWith('Job updates') ||
  warningMessage.value.startsWith('Live job updates') ? warningMessage.value : null);
const { jobs, offset, limit, totalCount, listLoading, listError } = storeToRefs(store);
const rows = computed(() => jobs.value as unknown as Record<string, unknown>[]);
const columns = [
  { key: 'id', label: 'Id', rowKeyColumn: true, width: '5.5rem' },
  { key: 'type', label: 'Type', width: 'minmax(14rem, 1fr)' },
  { key: 'status', label: 'Status', width: '9rem' },
  { key: 'startedAtUtc', label: 'Started', width: '13rem' },
  { key: 'completedAtUtc', label: 'Completed', width: '13rem' }
];
function openRow(row: Record<string, unknown>) {
  if (typeof row.id === 'number') void router.push({ name: 'system-job-details', params: { jobId: row.id } });
}
function onPageSizeChanged(event: Event) {
  const target = event.target;
  if (target instanceof HTMLSelectElement) void store.setPageSize(Number(target.value));
}
function refresh() { void store.loadJobs(); }
onMounted(() => { store.visible = true; void store.loadJobs(); });
onUnmounted(() => { store.visible = false; });
</script>

<style scoped>
.job-history-tab { display: flex; flex-direction: column; gap: 0.75rem; min-width: 0; }
.jobs-header { display: flex; align-items: end; justify-content: flex-end; gap: 1rem; }
.jobs-toolbar { display: flex; align-items: center; gap: 0.55rem; }
.jobs-page-size { display: inline-flex; align-items: center; flex: 0 0 auto; gap: 0.45rem; font-size: 0.86rem; font-weight: 700; color: var(--bo-ink-muted); white-space: nowrap; }
.jobs-page-size select { border: 1px solid var(--bo-border-default); border-radius: 8px; padding: 0.45rem 0.55rem; background: var(--bo-surface-panel); color: var(--bo-ink-default); }
.jobs-grid-region { flex: 1 1 auto; min-height: 22rem; }
.jobs-warning { margin: 0; color: var(--bo-ink-muted); }
.job-status--completed { color: var(--bo-colour-success-ink); }
.job-status--failed { color: var(--bo-colour-danger-ink); }
.job-status--running { color: var(--bo-ink-strong); border-color: var(--bo-colour-warning); }
@media (max-width: 700px) { .jobs-header { align-items: stretch; flex-direction: column; } }
</style>
