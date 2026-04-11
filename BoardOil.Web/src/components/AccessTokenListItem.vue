<template>
  <article class="panel panel-stack panel-stack--compact machine-pat-item">
    <div class="machine-pat-item-header">
      <strong>{{ token.name }}</strong>
      <div class="machine-pat-item-header-actions">
        <span class="badge-group">
          <span class="badge">{{ tokenStatus(token) }}</span>
          <span class="badge">{{ token.tokenPrefix }}</span>
        </span>
        <button
          type="button"
          class="btn btn--secondary"
          :disabled="isBusy || token.revokedAtUtc !== null"
          @click="emit('revoke', token)"
        >
          {{ token.revokedAtUtc ? 'Revoked' : 'Revoke token' }}
        </button>
      </div>
    </div>

    <div class="machine-pat-item-meta">
      <span><strong>Scopes:</strong> {{ token.scopes.join(', ') || 'None' }}</span>
      <span><strong>Boards:</strong> {{ describeBoardAccess(token) }}</span>
      <span><strong>Created:</strong> {{ formatDate(token.createdAtUtc) }}</span>
      <span><strong>Expires:</strong> {{ formatDate(token.expiresAtUtc) }}</span>
      <span><strong>Last used:</strong> {{ formatDate(token.lastUsedAtUtc) }}</span>
      <span><strong>Revoked:</strong> {{ formatDate(token.revokedAtUtc) }}</span>
    </div>

  </article>
</template>

<script setup lang="ts">
import type { AccessToken } from '../types/authTypes';

interface Props {
  token: AccessToken;
  isBusy: boolean;
  tokenStatus: (token: AccessToken) => string;
  describeBoardAccess: (token: AccessToken) => string;
  formatDate: (value: string | null) => string;
}

const props = defineProps<Props>();

const emit = defineEmits<{ revoke: [AccessToken] }>();

const { token, isBusy, tokenStatus, describeBoardAccess, formatDate } = props;
</script>

<style scoped>
.machine-pat-item-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.7rem;
}

.machine-pat-item-header-actions {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
}

.machine-pat-item-meta {
  display: grid;
  gap: 0.3rem;
  color: var(--bo-ink-default);
}

@media (max-width: 720px) {
  .machine-pat-item-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .machine-pat-item-header-actions {
    flex-wrap: wrap;
  }
}
</style>
