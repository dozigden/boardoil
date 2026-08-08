<template>
  <article class="entity-row authentication-record-row">
    <div class="entity-row-main authentication-record-main">
      <header class="authentication-record-title">
        <h3 class="entity-row-title">{{ token.name }}</h3>
        <span class="badge-group">
          <span class="badge">{{ tokenStatus(token) }}</span>
          <span class="badge">{{ token.tokenPrefix }}</span>
        </span>
      </header>

      <dl class="authentication-record-details">
        <div>
          <dt>Scopes</dt>
          <dd>{{ token.scopes.join(', ') || 'None' }}</dd>
        </div>
        <div>
          <dt>Created</dt>
          <dd>{{ formatDate(token.createdAtUtc) }}</dd>
        </div>
        <div>
          <dt>Expires</dt>
          <dd>{{ formatDate(token.expiresAtUtc) }}</dd>
        </div>
        <div>
          <dt>Last used</dt>
          <dd>{{ formatDate(token.lastUsedAtUtc) }}</dd>
        </div>
        <div>
          <dt>Revoked</dt>
          <dd>{{ formatDate(token.revokedAtUtc) }}</dd>
        </div>
      </dl>
    </div>

    <div class="entity-row-actions">
      <button
        type="button"
        class="btn btn--danger"
        :disabled="isBusy || token.revokedAtUtc !== null"
        @click="emit('revoke', token)"
      >
        {{ token.revokedAtUtc ? 'Revoked' : 'Revoke' }}
      </button>
    </div>
  </article>
</template>

<script setup lang="ts">
import type { AccessToken } from '../types/authTypes';

interface Props {
  token: AccessToken;
  isBusy: boolean;
  tokenStatus: (token: AccessToken) => string;
  formatDate: (value: string | null) => string;
}

const props = defineProps<Props>();

const emit = defineEmits<{ revoke: [AccessToken] }>();

const { token, isBusy, tokenStatus, formatDate } = props;
</script>
