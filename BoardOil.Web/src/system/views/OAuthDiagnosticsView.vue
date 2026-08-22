<template>
  <section class="oauth-diagnostics-view">
    <SystemLogsTabs />

    <header class="oauth-diagnostics-header">
      <div>
        <p class="oauth-diagnostics-capture-status">
          <span>{{ captureStatusText }}</span>
          <RouterLink :to="{ name: 'configuration' }">Configuration</RouterLink>
        </p>
      </div>
      <div class="oauth-diagnostics-toolbar">
        <label class="oauth-diagnostics-page-size">
          <span>Page size</span>
          <select :value="limit" :disabled="busy" @change="onPageSizeChanged">
            <option v-for="option in pageSizeOptions" :key="option" :value="option">
              {{ option }}
            </option>
          </select>
        </label>
        <button type="button" class="btn btn--secondary" :disabled="busy" @click="refresh">
          Refresh
        </button>
        <button type="button" class="btn btn--danger" :disabled="busy" @click="purgeExpired">
          {{ purging ? 'Purging...' : 'Purge old logs' }}
        </button>
      </div>
    </header>

    <p v-if="listErrorMessage" class="error">{{ listErrorMessage }}</p>

    <section v-else class="oauth-diagnostics-grid-region">
      <BoGrid
        :columns="gridFields"
        :items="rows"
        :is-loading="listLoading"
        :empty-text="emptyText"
        sticky-header="100%"
        :total-count="totalCount"
        :offset="offset"
        :limit="limit"
        row-clickable
        @row-clicked="openAuditDetails"
        @previous-page="auditStore.goPreviousPage"
        @next-page="auditStore.goNextPage"
      >
        <template #cell(id)="{ row }">
          <span class="oauth-diagnostic-id">#{{ row.id }}</span>
        </template>
        <template #cell(occurredAtUtc)="{ row }">
          <span>{{ formatDate(String(row.occurredAtUtc ?? '')) }}</span>
        </template>
        <template #cell(outcome)="{ row }">
          <span
            class="badge oauth-diagnostic-outcome"
            :class="getOutcomeClass(String(row.outcome ?? ''))"
          >
            {{ row.outcome }}
          </span>
        </template>
        <template #cell(grantType)="{ row }">
          <span>{{ formatGrantType(String(row.grantType ?? '')) }}</span>
        </template>
        <template #cell(connection)="{ row }">
          <span class="oauth-diagnostic-identity">
            <strong>{{ formatConnectionName(row) }}</strong>
            <small v-if="formatConnectionContext(row)">{{ formatConnectionContext(row) }}</small>
          </span>
        </template>
        <template #cell(error)="{ row }">
          <span class="oauth-diagnostic-error" :title="formatError(row)">
            {{ formatError(row) }}
          </span>
        </template>
      </BoGrid>
    </section>

    <OAuthTokenAuditDetailsDialog :audit="selectedAudit" @close="closeAuditDetails" />
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';
import BoGrid from '../../shared/components/BoGrid.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { OAuthTokenAudit } from '../../shared/types/oauthTokenAuditTypes';
import OAuthTokenAuditDetailsDialog from '../components/OAuthTokenAuditDetailsDialog.vue';
import SystemLogsTabs from '../components/SystemLogsTabs.vue';
import {
  OAUTH_TOKEN_AUDIT_PAGE_SIZE_OPTIONS,
  useSystemOAuthTokenAuditsStore
} from '../stores/systemOAuthTokenAuditsStore';

const auditStore = useSystemOAuthTokenAuditsStore();
const { confirm } = useConfirm();
const feedback = useUiFeedbackStore();
const {
  listLoading,
  listErrorMessage,
  captureStateLoading,
  captureStateErrorMessage,
  captureEnabled,
  audits,
  offset,
  limit,
  totalCount
} = storeToRefs(auditStore);
const rows = computed(() => audits.value as unknown as Record<string, unknown>[]);
const loading = computed(() => listLoading.value || captureStateLoading.value);
const purging = ref(false);
const selectedAudit = ref<OAuthTokenAudit | null>(null);
const busy = computed(() => loading.value || purging.value);
const captureStatusText = computed(() => {
  if (captureStateLoading.value) {
    return 'Checking logging status...';
  }

  if (captureStateErrorMessage.value !== null || captureEnabled.value === null) {
    return 'Logging status unavailable.';
  }

  return captureEnabled.value ? 'Logging enabled.' : 'Logging disabled.';
});
const pageSizeOptions = OAUTH_TOKEN_AUDIT_PAGE_SIZE_OPTIONS;
const emptyText = computed(() => {
  if (totalCount.value > 0) {
    return 'No OAuth logs are available on this page.';
  }

  if (captureEnabled.value === false) {
    return 'Logging is disabled and there are no historical OAuth logs.';
  }

  return 'No OAuth logs have been captured.';
});

const gridFields = [
  { key: 'id', label: 'Id', rowKeyColumn: true, width: '5.5rem' },
  { key: 'occurredAtUtc', label: 'Occurred', width: '13rem' },
  { key: 'outcome', label: 'Outcome', width: '8rem' },
  { key: 'grantType', label: 'Grant', width: '10rem' },
  { key: 'connection', label: 'Connection / client', width: 'minmax(14rem, 1fr)' },
  { key: 'error', label: 'Error', width: 'minmax(14rem, 1fr)' }
];

function onPageSizeChanged(event: Event) {
  const target = event.target;
  if (!(target instanceof HTMLSelectElement)) {
    return;
  }

  void auditStore.setPageSize(Number(target.value));
}

function refresh() {
  void auditStore.refresh();
}

function openAuditDetails(row: Record<string, unknown>) {
  if (typeof row.id !== 'number') {
    return;
  }

  selectedAudit.value = audits.value.find(audit => audit.id === row.id) ?? null;
}

function closeAuditDetails() {
  selectedAudit.value = null;
}

async function purgeExpired() {
  const accepted = await confirm({
    title: 'Purge old OAuth logs?',
    message: 'Delete OAuth logs older than the configured retention period? Newer logs will be kept.',
    confirmLabel: 'Purge old logs',
    danger: true
  });
  if (!accepted) {
    return;
  }

  purging.value = true;
  try {
    const result = await auditStore.purgeExpiredAudits();
    if (result !== null) {
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

function formatGrantType(value: string): string {
  if (value === 'authorization_code') {
    return 'Authorization code';
  }

  if (value === 'refresh_token') {
    return 'Refresh token';
  }

  return value || 'Unknown';
}

function formatConnectionName(row: Record<string, unknown>): string {
  return readText(row.oauthConnectionName)
    ?? readText(row.oauthClientDisplayName)
    ?? readText(row.oauthClientId)
    ?? 'Unresolved client';
}

function formatConnectionContext(row: Record<string, unknown>): string {
  const parts = [
    readText(row.ownerUserName),
    readText(row.oauthClientDisplayName),
    readText(row.oauthClientId)
  ];
  return [...new Set(parts.filter((part): part is string => part !== null))]
    .filter(part => part !== formatConnectionName(row))
    .join(' · ');
}

function formatError(row: Record<string, unknown>): string {
  const errorCode = readText(row.errorCode);
  const errorDescription = readText(row.errorDescription);
  if (errorCode !== null && errorDescription !== null) {
    return `${errorCode}: ${errorDescription}`;
  }

  return errorCode ?? errorDescription ?? '—';
}

function getOutcomeClass(outcome: string) {
  return {
    'oauth-diagnostic-outcome--succeeded': outcome === 'Succeeded',
    'oauth-diagnostic-outcome--rejected': outcome === 'Rejected'
  };
}

function readText(value: unknown): string | null {
  if (typeof value !== 'string') {
    return null;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

onMounted(async () => {
  await auditStore.refresh();
});
</script>

<style scoped>
.oauth-diagnostics-view {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  min-height: 100%;
}

.oauth-diagnostics-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
}

.oauth-diagnostics-header p {
  margin: 0;
}

.oauth-diagnostics-header p {
  color: var(--bo-ink-muted);
}

.oauth-diagnostics-toolbar {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.55rem;
}

.oauth-diagnostics-page-size {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  color: var(--bo-ink-muted);
  font-size: 0.86rem;
  font-weight: 700;
}

.oauth-diagnostics-page-size select {
  border: 1px solid var(--bo-border-default);
  border-radius: 8px;
  padding: 0.45rem 0.55rem;
  background: var(--bo-surface-panel);
  color: var(--bo-ink-default);
}

.oauth-diagnostics-capture-status {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.oauth-diagnostics-grid-region {
  flex: 1 1 auto;
  min-height: 22rem;
}

.oauth-diagnostic-id {
  font-weight: 700;
}

.oauth-diagnostic-outcome--succeeded {
  color: var(--bo-colour-success-ink);
}

.oauth-diagnostic-outcome--rejected {
  color: var(--bo-colour-danger-ink);
}

.oauth-diagnostic-identity {
  display: grid;
  gap: 0.15rem;
  min-width: 0;
}

.oauth-diagnostic-identity strong,
.oauth-diagnostic-identity small,
.oauth-diagnostic-error {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.oauth-diagnostic-identity small {
  color: var(--bo-ink-muted);
}

.oauth-diagnostic-error {
  display: block;
}

@media (max-width: 900px) {
  .oauth-diagnostics-header {
    align-items: stretch;
    flex-direction: column;
  }

  .oauth-diagnostics-toolbar {
    justify-content: flex-start;
    flex-wrap: wrap;
  }
}

@media (max-width: 560px) {
  .oauth-diagnostics-toolbar,
  .oauth-diagnostics-page-size {
    align-items: stretch;
    flex-direction: column;
  }

  .oauth-diagnostics-toolbar .btn {
    width: 100%;
  }
}
</style>
