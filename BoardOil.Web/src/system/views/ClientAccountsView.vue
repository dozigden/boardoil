<template>
  <section class="client-accounts-view">
    <header class="client-accounts-header">
      <div>
        <h2>Client Accounts</h2>
        <p>Client accounts are used for REST API access from other applications, or MCP if you want the agent to have its own identity.</p>
      </div>
      <button type="button" class="btn" :disabled="isBusy" @click="openCreateDialog">Create client account</button>
    </header>

    <div class="client-accounts-layout">
      <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
      <p v-if="successMessage" class="success">{{ successMessage }}</p>

      <section class="client-accounts-list">
        <p v-if="clients.length === 0" class="client-accounts-empty">No client accounts have been created yet.</p>
        <article v-for="client in clients" :key="client.id" class="entity-row client-account-row">
          <button
            type="button"
            class="entity-row-main entity-row-main-button"
            :disabled="isBusy"
            :aria-label="`Manage tokens for client account ${client.userName}`"
            @click="openClientTokens(client.id)"
          >
            <span class="badge">#{{ client.id }}</span>
            <strong class="entity-row-title">{{ client.userName }}</strong>
            <span class="entity-row-badges badge-group">
              <span class="badge">{{ client.role }}</span>
              <span class="badge">{{ client.isActive ? 'Active' : 'Inactive' }}</span>
            </span>
          </button>
          <div class="entity-row-actions">
            <BoDropdown
              align="right"
              icon-only
              label="Client account actions"
              :icon="MoreVertical"
              :disabled="isBusy"
            >
              <template #default="{ close }">
                <button type="button" class="bo-dropdown-item" :disabled="isBusy" @click="openClientTokensFromMenu(client.id, close)">
                  Tokens
                </button>
                <span class="bo-dropdown-divider" aria-hidden="true"></span>
                <button type="button" class="bo-dropdown-item" :disabled="isBusy" @click="deleteClientFromMenu(client, close)">
                  Delete
                </button>
              </template>
            </BoDropdown>
          </div>
        </article>
      </section>
    </div>

    <ClientAccountCreateDialog
      :open="isCreateDialogOpen"
      :busy="isBusy"
      @close="closeCreateDialog"
      @submit="createClientAccount"
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
import { MoreVertical } from 'lucide-vue-next';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { createSystemApi } from '../../shared/api/systemApi';
import AccessTokenSecretModal from '../../shared/components/AccessTokenSecretModal.vue';
import BoDropdown from '../../shared/components/BoDropdown.vue';
import ClientAccountCreateDialog from '../components/ClientAccountCreateDialog.vue';
import type { ClientAccount, CreateClientAccountRequest } from '../../shared/types/authTypes';

const systemApi = createSystemApi();
const clients = ref<ClientAccount[]>([]);

const loading = ref(false);
const createBusy = ref(false);
const isCreateDialogOpen = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const plainTextPat = ref<string | null>(null);
const plainTextPatName = ref<string>('');

const router = useRouter();

const isBusy = computed(() => loading.value || createBusy.value);
const isSecretModalOpen = computed(() => plainTextPat.value !== null);

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
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

async function createClientAccount(payload: CreateClientAccountRequest) {
  createBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.createClientAccount(payload);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = [...clients.value, result.data.account].sort((a, b) => a.userName.localeCompare(b.userName));
    plainTextPat.value = result.data.token.plainTextToken;
    plainTextPatName.value = result.data.token.token.name;
    successMessage.value = `Created client account ${result.data.account.userName}.`;
    isCreateDialogOpen.value = false;
  } finally {
    createBusy.value = false;
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

function openClientTokens(clientId: number) {
  dismissPlainTextPat();
  router.push({ name: 'client-account-tokens', params: { clientAccountId: clientId } });
}

async function deleteClientAccount(client: ClientAccount) {
  const confirmed = window.confirm(`Delete client account "${client.userName}"? This revokes its access and cannot be undone.`);
  if (!confirmed) {
    return;
  }

  loading.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.deleteClientAccount(client.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = clients.value.filter(entry => entry.id !== client.id);
    successMessage.value = `Deleted client account ${client.userName}.`;
  } finally {
    loading.value = false;
  }
}

function openClientTokensFromMenu(clientId: number, close: () => void) {
  close();
  openClientTokens(clientId);
}

async function deleteClientFromMenu(client: ClientAccount, close: () => void) {
  close();
  await deleteClientAccount(client);
}

onMounted(async () => {
  await loadClients();
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
  gap: 0.9rem;
  align-items: start;
}

.client-accounts-list {
  display: grid;
  gap: 0.6rem;
}

.client-accounts-empty {
  margin: 0;
  color: var(--bo-ink-muted);
}
</style>
