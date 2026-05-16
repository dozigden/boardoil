<template>
  <section class="client-accounts-view client-accounts-page">
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

      <section class="client-accounts-grid-wrap">
        <BoGrid
          class="client-accounts-grid"
          :columns="gridFields"
          :items="clients"
          :is-loading="loading"
          empty-text="No client accounts have been created yet."
          sticky-header="100%"
          :total-count="clients.length"
          :offset="0"
          :limit="clients.length > 0 ? clients.length : 1"
          :show-pagination-controls="false"
        >
          <template #cell(id)="{ row }">
            <button
              type="button"
              class="client-id-link client-cell-text"
              :disabled="isBusy"
              @click="openEditDialog(asClientAccount(row))"
            >
              #{{ row.id }}
            </button>
          </template>
          <template #cell(displayName)="{ row }">
            <span class="client-row-leading">
              <UserAvatar
                :image-relative-path="String(row.profileImageRelativePath ?? '') || null"
                :display-name="String(row.displayName ?? '')"
                size="lg"
                class="client-row-avatar"
              />
              <span class="client-cell-text">{{ row.displayName }}</span>
            </span>
          </template>
          <template #cell(userName)="{ row }">
            <span class="client-cell-text">{{ row.userName }}</span>
          </template>
          <template #cell(email)="{ row }">
            <span class="client-cell-text">{{ row.email }}</span>
          </template>
          <template #cell(role)="{ row }">
            <span class="client-cell-text">{{ row.role }}</span>
          </template>
          <template #cell(isActive)="{ row }">
            <span class="client-cell-text">{{ row.isActive ? 'Active' : 'Inactive' }}</span>
          </template>
          <template #cell(actions)="{ row }">
            <BoDropdown
              align="right"
              icon-only
              label="Client account actions"
              :icon="MoreVertical"
              :disabled="isBusy"
            >
              <template #default="{ close }">
                <button
                  type="button"
                  class="bo-dropdown-item"
                  :disabled="isBusy"
                  @click="openEditClientFromMenu(asClientAccount(row), close)"
                >
                  Edit details
                </button>
                <span class="bo-dropdown-divider" aria-hidden="true"></span>
                <button
                  type="button"
                  class="bo-dropdown-item"
                  :disabled="isBusy"
                  @click="openClientTokensFromMenu(Number(row.id), close)"
                >
                  Tokens
                </button>
                <span class="bo-dropdown-divider" aria-hidden="true"></span>
                <button
                  type="button"
                  class="bo-dropdown-item"
                  :disabled="isBusy"
                  @click="openClientImagePicker(asClientAccount(row), close)"
                >
                  Upload image
                </button>
                <button
                  type="button"
                  class="bo-dropdown-item"
                  :disabled="isBusy || !asClientAccount(row).profileImageRelativePath"
                  @click="removeClientImageFromMenu(asClientAccount(row), close)"
                >
                  Remove image
                </button>
                <span class="bo-dropdown-divider" aria-hidden="true"></span>
                <button
                  type="button"
                  class="bo-dropdown-item"
                  :disabled="isBusy"
                  @click="deleteClientFromMenu(asClientAccount(row), close)"
                >
                  Delete
                </button>
              </template>
            </BoDropdown>
          </template>
        </BoGrid>
      </section>
    </div>

    <ClientAccountCreateDialog
      :open="isCreateDialogOpen"
      :busy="isBusy"
      @close="closeCreateDialog"
      @submit="createClientAccount"
    />
    <ClientAccountEditDialog
      :open="isEditDialogOpen"
      :busy="isBusy"
      :client="clientForEdit"
      @close="closeEditDialog"
      @submit="submitClientEdit"
    />

    <AccessTokenSecretModal
      :open="isSecretModalOpen"
      :busy="isBusy"
      :token="plainTextPat"
      :token-name="plainTextPatName"
      @close="dismissPlainTextPat"
      @copy="copyPlainTextPat"
    />

    <input
      ref="clientImageInput"
      type="file"
      accept="image/png,image/jpeg,image/webp"
      class="client-image-file-input"
      @change="onClientImageSelected"
    />

    <ProfileImageCropDialog
      :open="cropDialogOpen"
      :source-file="pendingClientImageFile"
      :busy="imageBusy"
      :error-message="errorMessage"
      @close="closeCropDialog"
      @confirm="uploadCroppedClientImage"
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
import BoGrid from '../../shared/components/BoGrid.vue';
import UserAvatar from '../../shared/components/UserAvatar.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import ProfileImageCropDialog from '../../site/components/ProfileImageCropDialog.vue';
import ClientAccountCreateDialog from '../components/ClientAccountCreateDialog.vue';
import ClientAccountEditDialog from '../components/ClientAccountEditDialog.vue';
import type { ClientAccount, CreateClientAccountRequest } from '../../shared/types/authTypes';

const systemApi = createSystemApi();
const { confirm } = useConfirm();
const clients = ref<ClientAccount[]>([]);

const loading = ref(false);
const createBusy = ref(false);
const editBusy = ref(false);
const imageBusy = ref(false);
const isCreateDialogOpen = ref(false);
const isEditDialogOpen = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const plainTextPat = ref<string | null>(null);
const plainTextPatName = ref<string>('');
const clientForEdit = ref<ClientAccount | null>(null);
const clientImageInput = ref<HTMLInputElement | null>(null);
const cropDialogOpen = ref(false);
const pendingClientImageClientId = ref<number | null>(null);
const pendingClientImageFile = ref<File | null>(null);

const router = useRouter();

const isBusy = computed(() => loading.value || createBusy.value || editBusy.value || imageBusy.value);
const isSecretModalOpen = computed(() => plainTextPat.value !== null);
const gridFields: Array<{
  key: string;
  label: string;
  rowKeyColumn?: boolean;
  width?: string;
  align?: 'end';
}> = [
  { key: 'id', label: 'Id', rowKeyColumn: true, width: '5.5rem' },
  { key: 'displayName', label: 'Display Name', width: '17rem' },
  { key: 'userName', label: 'User Name', width: '12rem' },
  { key: 'email', label: 'Email' },
  { key: 'role', label: 'Role', width: '8rem' },
  { key: 'isActive', label: 'Status', width: '8rem' },
  { key: 'actions', label: '', width: '4.5rem', align: 'end' }
];

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
}

function openEditDialog(client: ClientAccount) {
  clientForEdit.value = client;
  isEditDialogOpen.value = true;
}

function closeEditDialog() {
  clientForEdit.value = null;
  isEditDialogOpen.value = false;
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
    successMessage.value = `Created client account ${result.data.account.displayName}.`;
    isCreateDialogOpen.value = false;
  } finally {
    createBusy.value = false;
  }
}

async function submitClientEdit(payload: { displayName: string; email: string; role: 'Admin' | 'Standard'; isActive: boolean }) {
  const selectedClient = clientForEdit.value;
  if (!selectedClient) {
    return;
  }

  editBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.updateClientAccount(selectedClient.id, payload);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = clients.value.map(client => (client.id === selectedClient.id ? result.data : client));
    closeEditDialog();
    successMessage.value = `Updated client account ${result.data.displayName}.`;
  } finally {
    editBusy.value = false;
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
  const confirmed = await confirm({
    title: 'Delete client account',
    message: `Delete client account "${client.displayName}"? This revokes its access and cannot be undone.`,
    confirmLabel: 'Delete',
    danger: true
  });
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
    successMessage.value = `Deleted client account ${client.displayName}.`;
  } finally {
    loading.value = false;
  }
}

function openClientImagePicker(client: ClientAccount, close: () => void) {
  if (isBusy.value) {
    return;
  }

  close();
  pendingClientImageClientId.value = client.id;
  pendingClientImageFile.value = null;
  clientImageInput.value?.click();
}

function onClientImageSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file || pendingClientImageClientId.value === null) {
    input.value = '';
    return;
  }

  pendingClientImageFile.value = file;
  cropDialogOpen.value = true;
  input.value = '';
}

function closeCropDialog() {
  if (imageBusy.value) {
    return;
  }

  resetCropDialogState();
}

function resetCropDialogState() {
  cropDialogOpen.value = false;
  pendingClientImageFile.value = null;
  pendingClientImageClientId.value = null;
}

async function uploadCroppedClientImage(file: File) {
  const clientId = pendingClientImageClientId.value;
  if (clientId === null) {
    return;
  }

  imageBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.uploadClientAccountProfileImage(clientId, file);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = clients.value.map(client =>
      client.id === clientId
        ? { ...client, profileImageRelativePath: result.data.relativePath }
        : client
    );
    resetCropDialogState();
    successMessage.value = 'Updated client account image.';
  } finally {
    imageBusy.value = false;
  }
}

async function removeClientImage(client: ClientAccount) {
  imageBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await systemApi.deleteClientAccountProfileImage(client.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    clients.value = clients.value.map(entry =>
      entry.id === client.id
        ? { ...entry, profileImageRelativePath: null }
        : entry
    );
    successMessage.value = `Removed image for ${client.displayName}.`;
  } finally {
    imageBusy.value = false;
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

async function removeClientImageFromMenu(client: ClientAccount, close: () => void) {
  close();
  await removeClientImage(client);
}

function openEditClientFromMenu(client: ClientAccount, close: () => void) {
  close();
  openEditDialog(client);
}

function asClientAccount(row: Record<string, unknown>): ClientAccount {
  return row as ClientAccount;
}

onMounted(async () => {
  await loadClients();
});
</script>

<style scoped>
.client-accounts-view {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  gap: 0.9rem;
}

.client-accounts-page {
  height: 100%;
  margin-top: 1rem;
  min-height: 0;
  overflow: hidden;
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
  display: flex;
  flex-direction: column;
  flex: 1;
  gap: 0.9rem;
  min-height: 0;
  overflow: hidden;
}

.client-accounts-grid-wrap {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.client-accounts-grid {
  height: 100%;
  min-height: 0;
}

.client-cell-text {
  display: block;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.client-id-link {
  background: transparent;
  border: none;
  padding: 0;
  color: var(--bo-link);
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 0.12em;
}

.client-id-link:disabled {
  cursor: default;
  color: var(--bo-ink-muted);
  text-decoration: none;
}

.client-row-leading {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  min-width: 0;
}

.client-row-avatar {
  flex: 0 0 auto;
}

.client-image-file-input {
  display: none;
}
</style>
