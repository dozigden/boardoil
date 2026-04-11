<template>
  <section class="client-accounts-view">
    <header class="client-accounts-header">
      <div>
        <h2>Client Accounts</h2>
        <p>Manage service accounts and their access tokens.</p>
      </div>
      <button type="button" class="btn" :disabled="isBusy" @click="openCreateDialog">Create client account</button>
    </header>

    <div class="client-accounts-layout">
      <section class="client-accounts-column">
        <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
        <p v-if="successMessage" class="success">{{ successMessage }}</p>

        <section class="client-accounts-list">
          <p v-if="clients.length === 0" class="client-accounts-empty">No client accounts have been created yet.</p>
          <article
            v-for="client in clients"
            :key="client.id"
            class="entity-row client-account-row"
            :class="{ 'is-selected': client.id === selectedClientId }"
          >
            <button
              type="button"
              class="entity-row-main entity-row-main-button"
              :disabled="isBusy"
              :aria-label="`Select client account ${client.userName}`"
              @click="selectClient(client.id)"
            >
              <span class="badge">#{{ client.id }}</span>
              <strong class="entity-row-title">{{ client.userName }}</strong>
              <span class="entity-row-badges badge-group">
                <span class="badge">{{ client.role }}</span>
                <span class="badge">{{ client.isActive ? 'Active' : 'Inactive' }}</span>
              </span>
            </button>
            <div class="entity-row-actions">
              <button type="button" class="btn btn--secondary" :disabled="isBusy" @click="selectClient(client.id)">
                Tokens
              </button>
            </div>
          </article>
        </section>
      </section>

      <section class="client-accounts-column">
        <header class="client-tokens-header">
          <div>
            <h3 v-if="selectedClient">{{ selectedClient.userName }} tokens</h3>
            <h3 v-else>Client tokens</h3>
            <p v-if="!selectedClient">Select a client account to manage its tokens.</p>
          </div>
          <button
            type="button"
            class="btn"
            :disabled="isBusy || !selectedClient"
            @click="openCreateTokenDialog"
          >
            Create token
          </button>
        </header>

        <section class="client-token-list">
          <p v-if="selectedClient && tokens.length === 0" class="client-accounts-empty">No tokens for this client yet.</p>
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
      </section>
    </div>

    <ClientAccountCreateDialog
      :open="isCreateDialogOpen"
      :busy="isBusy"
      @close="closeCreateDialog"
      @submit="createClientAccount"
    />

    <AccessTokenCreateDialog
      :open="isCreateTokenDialogOpen"
      :busy="isBusy"
      :boards="boards"
      :default-scopes="clientDefaultScopes"
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
import { computed, onMounted, ref } from 'vue';
import { createAuthApi } from '../api/authApi';
import { createBoardApi } from '../api/boardApi';
import AccessTokenCreateDialog from '../components/AccessTokenCreateDialog.vue';
import AccessTokenListItem from '../components/AccessTokenListItem.vue';
import AccessTokenSecretModal from '../components/AccessTokenSecretModal.vue';
import ClientAccountCreateDialog from '../components/ClientAccountCreateDialog.vue';
import type { AccessToken, ClientAccount, CreateAccessTokenRequest, CreateClientAccountRequest } from '../types/authTypes';
import type { BoardSummary } from '../types/boardTypes';

const authApi = createAuthApi();
const boardApi = createBoardApi();

const clientDefaultScopes = ['api:read', 'api:write', 'api:admin', 'api:system'];

const clients = ref<ClientAccount[]>([]);
const tokens = ref<AccessToken[]>([]);
const boards = ref<BoardSummary[]>([]);
const selectedClientId = ref<number | null>(null);

const loading = ref(false);
const tokenLoading = ref(false);
const createBusy = ref(false);
const tokenCreateBusy = ref(false);
const revokeBusyTokenId = ref<number | null>(null);
const isCreateDialogOpen = ref(false);
const isCreateTokenDialogOpen = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const plainTextPat = ref<string | null>(null);
const plainTextPatName = ref<string>('');

const selectedClient = computed(() => clients.value.find(client => client.id === selectedClientId.value) ?? null);

const isBusy = computed(
  () => loading.value || tokenLoading.value || createBusy.value || tokenCreateBusy.value || revokeBusyTokenId.value !== null
);
const isSecretModalOpen = computed(() => plainTextPat.value !== null);

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
}

function openCreateTokenDialog() {
  if (!selectedClient.value) {
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
    const result = await authApi.getClientAccounts();
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = result.data;
    if (selectedClientId.value !== null && !clients.value.some(client => client.id === selectedClientId.value)) {
      selectedClientId.value = null;
      tokens.value = [];
    }
  } finally {
    loading.value = false;
  }
}

async function loadTokens(clientAccountId: number) {
  tokenLoading.value = true;
  errorMessage.value = null;
  try {
    const result = await authApi.getClientAccountTokens(clientAccountId);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    tokens.value = sortTokens(result.data);
  } finally {
    tokenLoading.value = false;
  }
}

async function selectClient(clientId: number) {
  if (isBusy.value) {
    return;
  }

  selectedClientId.value = clientId;
  dismissPlainTextPat();
  await loadTokens(clientId);
}

async function createClientAccount(payload: CreateClientAccountRequest) {
  createBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.createClientAccount(payload);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = [...clients.value, result.data.account].sort((a, b) => a.userName.localeCompare(b.userName));
    selectedClientId.value = result.data.account.id;
    tokens.value = sortTokens([result.data.token.token]);
    plainTextPat.value = result.data.token.plainTextToken;
    plainTextPatName.value = result.data.token.token.name;
    successMessage.value = `Created client account ${result.data.account.userName}.`;
    isCreateDialogOpen.value = false;
  } finally {
    createBusy.value = false;
  }
}

async function createClientToken(payload: CreateAccessTokenRequest) {
  if (!selectedClient.value) {
    return;
  }

  tokenCreateBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.createClientAccountToken(selectedClient.value.id, payload);
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
  if (!selectedClient.value || token.revokedAtUtc) {
    return;
  }

  if (!window.confirm(`Revoke access token "${token.name}"? This cannot be undone.`)) {
    return;
  }

  revokeBusyTokenId.value = token.id;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.revokeClientAccountToken(selectedClient.value.id, token.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    successMessage.value = `Revoked access token ${token.name}.`;
    await loadTokens(selectedClient.value.id);
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

onMounted(async () => {
  await Promise.all([loadBoards(), loadClients()]);
});
</script>

<style scoped>
.client-accounts-view {
  margin-top: 1rem;
  display: grid;
  gap: 0.9rem;
}

.client-accounts-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.75rem;
}

.client-accounts-header h2 {
  margin: 0;
}

.client-accounts-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.client-accounts-layout {
  display: grid;
  grid-template-columns: minmax(320px, 1.1fr) minmax(320px, 1fr);
  gap: 0.9rem;
  align-items: start;
}

.client-accounts-column {
  min-width: 0;
  display: grid;
  gap: 0.9rem;
}

.client-accounts-list {
  display: grid;
  gap: 0.6rem;
}

.client-account-row.is-selected {
  border-color: color-mix(in oklab, var(--bo-colour-energy) 40%, var(--bo-border-soft));
  background: color-mix(in oklab, var(--bo-colour-energy) 10%, var(--bo-surface-base));
}

.client-accounts-empty {
  margin: 0;
  color: var(--bo-ink-muted);
}

.client-tokens-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.5rem;
}

.client-tokens-header h3 {
  margin: 0;
}

.client-tokens-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.client-token-list {
  display: grid;
  gap: 0.7rem;
}

@media (max-width: 960px) {
  .client-accounts-layout {
    grid-template-columns: 1fr;
  }
}
</style>
