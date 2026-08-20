<template>
  <ModalDialog
    class="error-log-details-modal"
    :open="true"
    title="Error Log Details"
    close-label="Close error log details"
    size="fill"
    @close="close"
  >
    <template #headerActions>
      <button
        type="button"
        class="btn btn--secondary"
        :disabled="!currentErrorLog || currentErrorLogLoading"
        @click="copyDetails"
      >
        Copy details
      </button>
    </template>

    <p v-if="currentErrorLogLoading" class="error-log-detail-empty">
      Loading error log details...
    </p>
    <p v-else-if="currentErrorLogError" class="error">{{ currentErrorLogError }}</p>
    <article
      v-else-if="currentErrorLog"
      class="error-log-report"
      @copy="onReportCopy"
    >
      <h4>Error #{{ currentErrorLog.id }}</h4>

      <dl class="error-log-report-facts">
        <div>
          <dt>Occurred</dt>
          <dd>{{ formatDate(currentErrorLog.occurredAtUtc) }}</dd>
        </div>
        <div>
          <dt>Source</dt>
          <dd>{{ currentErrorLog.source }}</dd>
        </div>
        <div>
          <dt>Area</dt>
          <dd>{{ currentErrorLog.area }}</dd>
        </div>
        <div>
          <dt>Actor</dt>
          <dd>{{ nullableReference(currentErrorLog.actorUserId) }}</dd>
        </div>
        <div>
          <dt>Trace</dt>
          <dd>{{ currentErrorLog.traceIdentifier ?? '-' }}</dd>
        </div>
        <div>
          <dt>Request</dt>
          <dd>{{ formatErrorLogRequest(currentErrorLog) }}</dd>
        </div>
      </dl>

      <section>
        <h5>Exception</h5>
        <p class="error-log-report-type">{{ currentErrorLog.exceptionType }}</p>
        <p class="error-log-report-message">{{ currentErrorLog.message }}</p>
      </section>

      <section>
        <h5>Stack Trace</h5>
        <pre>{{ formatErrorLogStackTrace(currentErrorLog.stackTrace) }}</pre>
      </section>

      <section>
        <h5>Context JSON</h5>
        <pre>{{ formatErrorLogContextJson(currentErrorLog.contextJson) }}</pre>
      </section>
    </article>
    <p v-else class="error-log-detail-empty">Error log not found.</p>
  </ModalDialog>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import ModalDialog from '../../shared/components/ModalDialog.vue';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import { copyTextToClipboard } from '../../shared/utils/clipboard';
import { useSystemErrorLogsStore } from '../stores/systemErrorLogsStore';
import {
  buildErrorLogMarkdown,
  formatErrorLogContextJson,
  formatErrorLogRequest,
  formatErrorLogStackTrace
} from '../utils/errorLogMarkdown';

const route = useRoute();
const router = useRouter();
const feedback = useUiFeedbackStore();
const errorLogsStore = useSystemErrorLogsStore();
const { detailLoadingById, detailErrorById, errorLogById } = storeToRefs(errorLogsStore);

const errorLogId = computed(() => {
  const raw = route.params.errorLogId;
  const first = Array.isArray(raw) ? raw[0] : raw;
  const parsed = Number(first);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
});

const currentErrorLog = computed(() => {
  if (errorLogId.value === null) {
    return null;
  }

  return errorLogById.value[errorLogId.value] ?? null;
});
const currentErrorLogLoading = computed(() =>
  errorLogId.value === null
    ? false
    : detailLoadingById.value[errorLogId.value] === true
);
const currentErrorLogError = computed(() =>
  errorLogId.value === null
    ? null
    : detailErrorById.value[errorLogId.value] ?? null
);
const markdownText = computed(() => {
  const errorLog = currentErrorLog.value;
  if (!errorLog) {
    return '';
  }

  return buildErrorLogMarkdown(errorLog, formatDate(errorLog.occurredAtUtc));
});

async function copyDetails() {
  if (!markdownText.value) {
    return;
  }

  const copied = await copyTextToClipboard(markdownText.value);
  if (copied) {
    feedback.showToast('Copied');
    return;
  }

  feedback.showToast('Could not copy automatically. Select the report and copy it manually.', 'error');
}

function onReportCopy(event: ClipboardEvent) {
  if (!markdownText.value || !event.clipboardData) {
    return;
  }

  event.clipboardData.setData('text/plain', markdownText.value);
  event.preventDefault();
  feedback.showToast('Copied');
}

async function close() {
  await router.replace({ name: 'system-error-logs' });
}

function nullableReference(value: number | null): string {
  return value === null ? '-' : `#${value}`;
}

function formatDate(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

watch(
  errorLogId,
  async id => {
    if (id !== null) {
      await errorLogsStore.loadErrorLogDetails(id);
    }
  },
  { immediate: true }
);
</script>

<style scoped>
.error-log-details-modal :deep(.card-modal-content) {
  overflow: hidden;
}

.error-log-detail-empty {
  margin: 0;
  color: var(--bo-ink-muted);
}

.error-log-report {
  display: grid;
  flex: 1 1 auto;
  gap: 1rem;
  min-height: 0;
  min-width: 0;
  border: 1px solid var(--bo-border-soft);
  border-radius: 10px;
  padding: 0.8rem;
  background: var(--bo-surface-panel);
  color: var(--bo-ink-default);
  overflow-y: auto;
  user-select: text;
}

.error-log-report h4,
.error-log-report h5,
.error-log-report p,
.error-log-report dl {
  margin: 0;
}

.error-log-report h4 {
  font-size: 1rem;
}

.error-log-report h5 {
  margin-bottom: 0.35rem;
  font-size: 0.9rem;
}

.error-log-report-facts {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr));
  gap: 0.55rem 0.9rem;
}

.error-log-report-facts div {
  min-width: 0;
}

.error-log-report-facts dt {
  color: var(--bo-ink-muted);
  font-size: 0.76rem;
  font-weight: 800;
  text-transform: uppercase;
}

.error-log-report-facts dd {
  margin: 0.1rem 0 0;
  overflow-wrap: anywhere;
}

.error-log-report-type,
.error-log-report pre {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
}

.error-log-report-type {
  overflow-wrap: anywhere;
  font-size: 0.86rem;
}

.error-log-report-message {
  margin-top: 0.35rem !important;
  color: var(--bo-colour-danger);
  overflow-wrap: anywhere;
}

.error-log-report pre {
  margin: 0;
  border: 1px solid var(--bo-border-soft);
  border-radius: 8px;
  padding: 0.65rem;
  background: var(--bo-surface-muted);
  font-size: 0.82rem;
  line-height: 1.45;
  white-space: pre-wrap;
  overflow-wrap: anywhere;
  word-break: break-word;
}
</style>
