<template>
  <FixedChromeDialog :open="true" title="Job Details" close-label="Close job details"
    size="fill" body-mode="managed" @close="close">
    <p v-if="loading" class="job-detail-empty">Loading job details...</p>
    <p v-else-if="error" class="error" role="alert">{{ error }}</p>
    <article v-else-if="job" class="job-detail-report">
      <h4>Job #{{ job.id }}</h4>
      <dl class="job-detail-facts">
        <div><dt>Type</dt><dd>{{ jobTypeLabel(job.type) }}</dd></div>
        <div><dt>Status</dt><dd><span class="badge" :class="statusClass(job.status)">{{ job.status }}</span></dd></div>
        <div><dt>Created</dt><dd>{{ formatJobDate(job.createdAtUtc) }}</dd></div>
        <div><dt>Run after</dt><dd>{{ formatJobDate(job.runAfterUtc) }}</dd></div>
        <div><dt>Started</dt><dd>{{ formatJobDate(job.startedAtUtc) }}</dd></div>
        <div><dt>Completed</dt><dd>{{ formatJobDate(job.completedAtUtc) }}</dd></div>
        <div><dt>Updated</dt><dd>{{ formatJobDate(job.updatedAtUtc) }}</dd></div>
        <div><dt>User</dt><dd>{{ job.userId === null ? '-' : `#${job.userId}` }}</dd></div>
        <div v-if="job.correlationId"><dt>Correlation</dt><dd>{{ job.correlationId }}</dd></div>
      </dl>
      <section v-if="job.errorMessage"><h5>Failure reason</h5><p class="error">{{ job.errorMessage }}</p></section>
      <section><h5>Logs ({{ job.logs?.length ?? 0 }})</h5>
        <div v-if="!job.logs"><p>Job logs are unavailable.</p>
          <button type="button" class="btn btn--secondary" @click="refreshDetails">Refresh details</button>
        </div>
        <p v-else-if="job.logs.length === 0">No logs found.</p>
        <div v-else class="job-logs-wrap"><table><thead><tr><th>Time</th><th>Level</th><th>Message</th><th>Data</th></tr></thead>
          <tbody><tr v-for="log in job.logs" :key="log.id"><td>{{ formatJobDate(log.loggedAtUtc) }}</td><td>{{ log.level }}</td><td>{{ log.message }}</td><td><pre>{{ formatDiagnosticJson(log.dataJson) }}</pre></td></tr></tbody>
        </table></div>
      </section>
      <section><h5>Payload JSON</h5><pre>{{ formatDiagnosticJson(job.payloadJson) }}</pre></section>
      <section><h5>Result JSON</h5><pre>{{ formatDiagnosticJson(job.resultJson) }}</pre></section>
    </article>
    <p v-else class="job-detail-empty">Job not found.</p>
    <template #actions><div class="fixed-chrome-dialog-actions fixed-chrome-dialog-actions--end">
      <button type="button" class="btn btn--secondary" @click="close">Close</button>
    </div></template>
  </FixedChromeDialog>
</template>

<script setup lang="ts">
import { computed, watch, onUnmounted } from 'vue';
import { storeToRefs } from 'pinia';
import { useRoute, useRouter } from 'vue-router';
import FixedChromeDialog from '../../shared/components/FixedChromeDialog.vue';
import { useSystemJobsStore } from '../stores/systemJobsStore';
import { formatDiagnosticJson, formatJobDate, jobTypeLabel, statusClass } from '../utils/jobPresentation';

const route = useRoute();
const router = useRouter();
const store = useSystemJobsStore();
const { byId, detailLoading, detailError } = storeToRefs(store);
const id = computed(() => {
  const raw = route.params.jobId;
  const parsed = Number(Array.isArray(raw) ? raw[0] : raw);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
});
const job = computed(() => id.value === null ? null : byId.value[id.value] ?? null);
const loading = computed(() => id.value !== null && detailLoading.value[id.value] === true);
const error = computed(() => id.value === null ? null : detailError.value[id.value] ?? null);
watch(id, value => {
  store.setOpenDetail(value);
  if (value !== null) void store.loadDetails(value);
}, { immediate: true });
onUnmounted(() => { store.setOpenDetail(null); });
function close() { void router.replace({ name: 'system-jobs' }); }
function refreshDetails() { if (id.value !== null) void store.loadDetails(id.value); }
</script>

<style scoped>
.job-detail-empty { margin: 0; color: var(--bo-ink-muted); }
.job-detail-report { display: grid; align-content: start; gap: 1rem; min-height: 0; min-width: 0; border: 1px solid var(--bo-border-soft); border-radius: 10px; padding: 0.8rem; background: var(--bo-surface-panel); overflow: auto; user-select: text; }
.job-detail-report h4, .job-detail-report h5, .job-detail-report p, .job-detail-report dl { margin: 0; }
.job-detail-facts { display: grid; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); gap: 0.55rem 0.9rem; }
.job-detail-facts dt { color: var(--bo-ink-muted); font-size: 0.76rem; font-weight: 800; text-transform: uppercase; }
.job-detail-facts dd { margin: 0.1rem 0 0; overflow-wrap: anywhere; }
.job-detail-report pre { margin: 0; border: 1px solid var(--bo-border-soft); border-radius: 8px; padding: 0.65rem; background: var(--bo-surface-muted); font: 0.82rem/1.45 ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; white-space: pre-wrap; overflow-wrap: anywhere; }
.job-logs-wrap { overflow-x: auto; }
.job-logs-wrap table { border-collapse: collapse; width: 100%; }
.job-logs-wrap th, .job-logs-wrap td { border-bottom: 1px solid var(--bo-border-soft); padding: 0.4rem; text-align: left; vertical-align: top; }
.job-status--completed { color: var(--bo-colour-success-ink); }
.job-status--failed { color: var(--bo-colour-danger-ink); }
.job-status--running { color: var(--bo-ink-strong); border-color: var(--bo-colour-warning); }
</style>
