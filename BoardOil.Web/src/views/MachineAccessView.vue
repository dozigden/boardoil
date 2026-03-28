<template>
  <section class="machine-access-view">
    <header class="machine-access-header">
      <div>
        <h2>Machine Access Tokens</h2>
        <p>Create and manage Personal Access Tokens (PATs) for MCP clients.</p>
      </div>
      <button type="button" :disabled="isBusy" @click="openCreateDialog">Create token</button>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section v-if="plainTextPat" class="machine-pat-secret">
      <h3>Copy token now</h3>
      <p>This value is only shown once for <strong>{{ plainTextPatName }}</strong>.</p>
      <code class="machine-pat-secret-value">{{ plainTextPat }}</code>
      <div class="machine-pat-secret-actions">
        <button type="button" :disabled="isBusy" @click="copyPlainTextPat">
          {{ copiedPat ? 'Copied' : 'Copy token' }}
        </button>
        <button type="button" class="ghost" :disabled="isBusy" @click="dismissPlainTextPat">Hide token</button>
      </div>
    </section>

    <section class="machine-pat-list">
      <header class="machine-pat-list-header">
        <h3>Existing tokens</h3>
        <button type="button" class="ghost" :disabled="isBusy" @click="refreshTokens">Refresh</button>
      </header>

      <p v-if="tokens.length === 0" class="machine-pat-empty">No machine tokens have been created yet.</p>

      <article v-for="token in tokens" :key="token.id" class="machine-pat-item">
        <div class="machine-pat-item-header">
          <strong>{{ token.name }}</strong>
          <span class="users-badges">
            <span class="card-id">{{ tokenStatus(token) }}</span>
            <span class="card-id">{{ token.tokenPrefix }}</span>
          </span>
        </div>

        <div class="machine-pat-item-meta">
          <span><strong>Scopes:</strong> {{ token.scopes.join(', ') || 'None' }}</span>
          <span><strong>Boards:</strong> {{ describeBoardAccess(token) }}</span>
          <span><strong>Created:</strong> {{ formatDate(token.createdAtUtc) }}</span>
          <span><strong>Expires:</strong> {{ formatDate(token.expiresAtUtc) }}</span>
          <span><strong>Last used:</strong> {{ formatDate(token.lastUsedAtUtc) }}</span>
          <span><strong>Revoked:</strong> {{ formatDate(token.revokedAtUtc) }}</span>
        </div>

        <div class="machine-pat-item-actions">
          <button
            type="button"
            class="ghost"
            :disabled="isBusy || token.revokedAtUtc !== null"
            @click="revokeToken(token)"
          >
            {{ token.revokedAtUtc ? 'Revoked' : 'Revoke token' }}
          </button>
        </div>
      </article>
    </section>

    <MachinePatCreateDialog
      :open="isCreateDialogOpen"
      :busy="isBusy"
      :boards="boards"
      @close="closeCreateDialog"
      @submit="createToken"
    />
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { createAuthApi } from '../api/authApi';
import { createBoardApi } from '../api/boardApi';
import MachinePatCreateDialog from '../components/MachinePatCreateDialog.vue';
import type { CreateMachinePatRequest, MachinePat } from '../types/authTypes';
import type { BoardSummary } from '../types/boardTypes';

const authApi = createAuthApi();
const boardApi = createBoardApi();

const boards = ref<BoardSummary[]>([]);
const tokens = ref<MachinePat[]>([]);
const loading = ref(false);
const createBusy = ref(false);
const revokeBusyTokenId = ref<number | null>(null);
const isCreateDialogOpen = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const plainTextPat = ref<string | null>(null);
const plainTextPatName = ref<string>('');
const copiedPat = ref(false);

const isBusy = computed(() => loading.value || createBusy.value || revokeBusyTokenId.value !== null);

onMounted(async () => {
  await loadInitialData();
});

async function loadInitialData() {
  loading.value = true;
  errorMessage.value = null;
  try {
    const [boardsResult, tokensResult] = await Promise.all([boardApi.getBoards(), authApi.getMachinePats()]);
    if (!boardsResult.ok) {
      errorMessage.value = boardsResult.error.message;
      return;
    }

    if (!tokensResult.ok) {
      errorMessage.value = tokensResult.error.message;
      return;
    }

    boards.value = [...boardsResult.data].sort((left, right) => left.id - right.id);
    tokens.value = sortTokens(tokensResult.data);
  } finally {
    loading.value = false;
  }
}

async function refreshTokens() {
  loading.value = true;
  errorMessage.value = null;
  try {
    const result = await authApi.getMachinePats();
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    tokens.value = sortTokens(result.data);
  } finally {
    loading.value = false;
  }
}

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
}

async function createToken(payload: CreateMachinePatRequest) {
  createBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.createMachinePat(payload);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    tokens.value = sortTokens([result.data.token, ...tokens.value.filter(token => token.id !== result.data.token.id)]);
    plainTextPat.value = result.data.plainTextToken;
    plainTextPatName.value = result.data.token.name;
    copiedPat.value = false;
    isCreateDialogOpen.value = false;
    successMessage.value = `Created token ${result.data.token.name}. Copy it now; it will not be shown again.`;
  } finally {
    createBusy.value = false;
  }
}

async function revokeToken(token: MachinePat) {
  if (token.revokedAtUtc) {
    return;
  }

  if (!window.confirm(`Revoke token "${token.name}"? This cannot be undone.`)) {
    return;
  }

  revokeBusyTokenId.value = token.id;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.revokeMachinePat(token.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    successMessage.value = `Revoked token ${token.name}.`;
    await refreshTokens();
  } finally {
    revokeBusyTokenId.value = null;
  }
}

async function copyPlainTextPat() {
  if (!plainTextPat.value) {
    return;
  }

  try {
    await navigator.clipboard.writeText(plainTextPat.value);
    copiedPat.value = true;
    successMessage.value = `Copied token ${plainTextPatName.value} to clipboard.`;
  } catch {
    errorMessage.value = 'Could not copy token to clipboard automatically.';
  }
}

function dismissPlainTextPat() {
  plainTextPat.value = null;
  plainTextPatName.value = '';
  copiedPat.value = false;
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

function tokenStatus(token: MachinePat) {
  if (token.revokedAtUtc) {
    return 'Revoked';
  }

  if (token.expiresAtUtc) {
    const parsedExpiry = new Date(token.expiresAtUtc);
    if (!Number.isNaN(parsedExpiry.getTime()) && parsedExpiry.getTime() <= Date.now()) {
      return 'Expired';
    }
  }

  return 'Active';
}

function describeBoardAccess(token: MachinePat) {
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

function sortTokens(items: MachinePat[]) {
  return [...items].sort((left, right) => {
    const leftTimestamp = Date.parse(left.createdAtUtc);
    const rightTimestamp = Date.parse(right.createdAtUtc);
    return rightTimestamp - leftTimestamp;
  });
}
</script>
