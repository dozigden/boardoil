<template>
  <section class="client-account-tokens-view">
    <header class="client-account-tokens-header">
      <div>
        <h2 class="client-account-tokens-title">
          <RouterLink :to="{ name: 'client-accounts' }" class="client-account-tokens-title-link">
            Client Accounts
          </RouterLink>
          <span class="client-account-tokens-title-separator" aria-hidden="true">&gt;</span>
          <span>{{ client ? `${client.userName} tokens` : 'Client account tokens' }}</span>
        </h2>
        <p v-if="client">Manage access tokens for this client account.</p>
        <p v-else>Choose a client account to manage its access tokens.</p>
      </div>
      <div class="client-account-tokens-actions">
        <button type="button" class="btn" :disabled="isBusy || !client" @click="openCreateTokenDialog">
          Create token
        </button>
      </div>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section class="client-token-list">
      <p v-if="client && tokens.length === 0" class="client-accounts-empty">No tokens for this client yet.</p>
      <p v-if="!client && !errorMessage" class="client-accounts-empty">Loading client account...</p>
      <AccessTokenListItem
        v-for="token in tokens"
        :key="token.id"
        :token="token"
        :is-busy="isBusy"
        :token-status="tokenStatus"
        :describe-board-access="describeBoardAccess"
        :format-date="formatDate"
        @revoke="revokeToken"
      />
    </section>

    <AccessTokenCreateDialog
      :open="isCreateTokenDialogOpen"
      :busy="isBusy"
      :boards="boards"
      :default-scopes="clientDefaultScopes"
      :allowed-scopes="clientAllowedScopes"
      :allow-board-access-selection="false"
      @close="closeCreateTokenDialog"
      @submit="createClientToken"
    />
    <AccessTokenSecretModal
      :open="isSecretModalOpen"
      :busy="isBusy"
      :token="plainTextPat"
      :token-name="plainTextPatName"
      @close="dismissPlainTextPat"
      @copy="copyPlainTextPat"
    />
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { createSystemApi } from '../api/systemApi';
import { createBoardApi } from '../api/boardApi';
import AccessTokenCreateDialog from '../components/AccessTokenCreateDialog.vue';
import AccessTokenListItem from '../components/AccessTokenListItem.vue';
import AccessTokenSecretModal from '../components/AccessTokenSecretModal.vue';
import type { AccessToken, ClientAccount, CreateAccessTokenRequest, CreateClientAccessTokenRequest } from '../types/authTypes';
import type { BoardSummary } from '../types/boardTypes';

const systemApi = createSystemApi();
const boardApi = createBoardApi();
const route = useRoute();
const router = useRouter();

const clientDefaultScopes = ['api:read', 'api:write', 'api:admin', 'api:system'];
const clientAllowedScopes = ['mcp:read', 'mcp:write', 'api:read', 'api:write', 'api:admin', 'api:system'];

const clients = ref<ClientAccount[]>([]);
const clientId = ref<number | null>(null);
const tokens = ref<AccessToken[]>([]);
const boards = ref<BoardSummary[]>([]);

const loading = ref(false);
const tokenLoading = ref(false);
const tokenCreateBusy = ref(false);
const revokeBusyTokenId = ref<number | null>(null);
const isCreateTokenDialogOpen = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const plainTextPat = ref<string | null>(null);
const plainTextPatName = ref<string>('');

const client = computed(() => clients.value.find(entry => entry.id === clientId.value) ?? null);

const isBusy = computed(
  () => loading.value || tokenLoading.value || tokenCreateBusy.value || revokeBusyTokenId.value !== null
);
const isSecretModalOpen = computed(() => plainTextPat.value !== null);

function resolveClientId() {
  const raw = route.params.clientAccountId;
  if (typeof raw === 'string') {
    const parsed = Number.parseInt(raw, 10);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function openCreateTokenDialog() {
  if (!client.value) {
    return;
  }

  isCreateTokenDialogOpen.value = true;
}

function closeCreateTokenDialog() {
  isCreateTokenDialogOpen.value = false;
}

async function loadBoards() {
  const result = await boardApi.getBoards();
  if (result.ok) {
    boards.value = result.data;
  }
}

async function loadClients() {
  loading.value = true;
  errorMessage.value = null;
  try {
    const result = await systemApi.getClientAccounts();
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = result.data;
  } finally {
    loading.value = false;
  }
}

async function loadTokens(targetClientId: number) {
  tokenLoading.value = true;
  errorMessage.value = null;
  try {
    const result = await systemApi.getClientAccountTokens(targetClientId);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    tokens.value = sortTokens(result.data);
  } finally {
    tokenLoading.value = false;
  }
}

async function createClientToken(payload: CreateAccessTokenRequest) {
  if (!client.value) {
    return;
  }

  const request: CreateClientAccessTokenRequest = {
    name: payload.name,
    expiresInDays: payload.expiresInDays,
    scopes: payload.scopes
  };

  tokenCreateBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.createClientAccountToken(client.value.id, request);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    tokens.value = sortTokens([result.data.token, ...tokens.value.filter(token => token.id !== result.data.token.id)]);
    plainTextPat.value = result.data.plainTextToken;
    plainTextPatName.value = result.data.token.name;
    successMessage.value = `Created access token ${result.data.token.name}.`;
    isCreateTokenDialogOpen.value = false;
  } finally {
    tokenCreateBusy.value = false;
  }
}

async function revokeToken(token: AccessToken) {
  if (!client.value || token.revokedAtUtc) {
    return;
  }

  if (!window.confirm(`Revoke access token "${token.name}"? This cannot be undone.`)) {
    return;
  }

  revokeBusyTokenId.value = token.id;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.revokeClientAccountToken(client.value.id, token.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    successMessage.value = `Revoked access token ${token.name}.`;
    await loadTokens(client.value.id);
  } finally {
    revokeBusyTokenId.value = null;
  }
}

async function copyPlainTextPat() {
  if (!plainTextPat.value) {
    return;
  }

  await copyToClipboard(plainTextPat.value, `token ${plainTextPatName.value}`);
}

async function copyToClipboard(text: string, label: string) {
  try {
    await navigator.clipboard.writeText(text);
    successMessage.value = `Copied ${label} to clipboard.`;
    errorMessage.value = null;
  } catch {
    errorMessage.value = 'Could not copy to clipboard automatically.';
  }
}

function dismissPlainTextPat() {
  plainTextPat.value = null;
  plainTextPatName.value = '';
}

function tokenStatus(token: AccessToken) {
  if (token.revokedAtUtc) {
    return 'Revoked';
  }

  if (token.expiresAtUtc) {
    const expiresAt = Date.parse(token.expiresAtUtc);
    if (Number.isFinite(expiresAt) && expiresAt <= Date.now()) {
      return 'Expired';
    }
  }

  return 'Active';
}

function describeBoardAccess(token: AccessToken) {
  if (token.boardAccessMode === 'all') {
    return 'All boards';
  }

  if (token.allowedBoardIds.length === 0) {
    return 'No boards';
  }

  return token.allowedBoardIds
    .map(boardId => {
      const board = boards.value.find(entry => entry.id === boardId);
      return board ? `${board.name} (#${board.id})` : `#${boardId}`;
    })
    .join(', ');
}

function formatDate(value: string | null) {
  if (!value) {
    return 'Never';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

function sortTokens(items: AccessToken[]) {
  return [...items].sort((left, right) => {
    const leftTimestamp = Date.parse(left.createdAtUtc);
    const rightTimestamp = Date.parse(right.createdAtUtc);
    return rightTimestamp - leftTimestamp;
  });
}

watch(
  () => route.params.clientAccountId,
  async () => {
    const resolvedId = resolveClientId();
    if (resolvedId === null) {
      await router.replace({ name: 'client-accounts' });
      return;
    }

    clientId.value = resolvedId;
    dismissPlainTextPat();
    successMessage.value = null;
    errorMessage.value = null;

    if (clients.value.length === 0) {
      await loadClients();
    }

    if (errorMessage.value) {
      tokens.value = [];
      return;
    }

    if (!client.value) {
      errorMessage.value = 'Client account not found.';
      tokens.value = [];
      return;
    }

    await loadTokens(resolvedId);
  },
  { immediate: true }
);

onMounted(async () => {
  await loadBoards();
  if (clientId.value === null) {
    clientId.value = resolveClientId();
  }
});
</script>

<style scoped>
.client-account-tokens-view {
  margin-top: 1rem;
  display: grid;
  gap: 0.9rem;
}

.client-account-tokens-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.75rem;
}

.client-account-tokens-header h2 {
  margin: 0;
}

.client-account-tokens-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.client-account-tokens-actions {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.client-account-tokens-title {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

.client-account-tokens-title-link {
  color: var(--bo-link);
  text-decoration: none;
}

.client-account-tokens-title-link:hover,
.client-account-tokens-title-link:focus-visible {
  text-decoration: underline;
}

.client-account-tokens-title-separator {
  color: var(--bo-ink-muted);
  opacity: 0.45;
}

.client-token-list {
  display: grid;
  gap: 0.7rem;
}

.client-accounts-empty {
  margin: 0;
  color: var(--bo-ink-muted);
}
</style>
