<template>
  <section class="entity-rows-page project-connections-page">
    <header class="entity-rows-header">
      <div class="entity-rows-header-copy">
        <h2>Project Connections</h2>
        <p>Stable, non-secret MCP resource URLs owned by existing client accounts.</p>
      </div>
      <button type="button" class="btn" :disabled="busy || clients.length === 0" @click="openCreateDialog">
        Create connection
      </button>
    </header>

    <p v-if="displayErrorMessage" class="error">{{ displayErrorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>
    <p v-if="!busy && clients.length === 0" class="entity-rows-empty">
      Create a client account before adding a project connection.
    </p>
    <p v-else-if="!busy && connections.length === 0" class="entity-rows-empty">
      No project connections have been created yet.
    </p>

    <ul v-else class="entity-rows-list">
      <li v-for="connection in connections" :key="connection.id" class="entity-row project-connection-row">
        <div class="entity-row-main project-connection-main">
          <div class="project-connection-heading">
            <h3 class="entity-row-title">{{ connection.name }}</h3>
            <span class="badge">{{ connection.isActive ? 'Active' : 'Revoked' }}</span>
            <span v-for="scope in connection.allowedScopes" :key="scope" class="badge">{{ scope }}</span>
          </div>

          <p class="project-connection-owner">
            {{ connection.clientAccountDisplayName }}
            <span>(@{{ connection.clientAccountUserName }})</span>
          </p>

          <div class="project-connection-url-row">
            <code>{{ connection.resourceUrl }}</code>
            <button
              type="button"
              class="btn btn--secondary"
              :disabled="busy"
              @click="copyResourceUrl(connection)"
            >
              Copy URL
            </button>
          </div>

          <p class="project-connection-metadata">
            Created by {{ connection.createdByUserName }} on {{ formatDate(connection.createdAtUtc) }}.
            <template v-if="connection.revokedAtUtc">
              Revoked by {{ connection.revokedByUserName ?? 'a deleted administrator' }} on
              {{ formatDate(connection.revokedAtUtc) }}.
            </template>
          </p>
        </div>

        <div class="entity-row-actions">
          <button
            v-if="connection.isActive"
            type="button"
            class="btn btn--danger"
            :disabled="busy"
            @click="revokeConnection(connection)"
          >
            Revoke
          </button>
        </div>
      </li>
    </ul>

    <McpProjectConnectionCreateDialog
      :open="isCreateDialogOpen"
      :busy="busy"
      :clients="clients"
      @close="closeCreateDialog"
      @submit="createConnection"
    />
  </section>
</template>

<script setup lang="ts">
import { storeToRefs } from 'pinia';
import { computed, onMounted, ref } from 'vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import type {
  CreateMcpProjectConnectionRequest,
  McpProjectConnection
} from '../../shared/types/mcpProjectConnectionTypes';
import McpProjectConnectionCreateDialog from '../components/McpProjectConnectionCreateDialog.vue';
import { useSystemClientAccountsStore } from '../stores/systemClientAccountsStore';
import { useSystemProjectConnectionsStore } from '../stores/systemProjectConnectionsStore';

const clientAccountsStore = useSystemClientAccountsStore();
const projectConnectionsStore = useSystemProjectConnectionsStore();
const { clients, busy: clientsBusy, errorMessage: clientsErrorMessage } = storeToRefs(clientAccountsStore);
const {
  connections,
  busy: connectionsBusy,
  errorMessage: connectionsErrorMessage,
  successMessage
} = storeToRefs(projectConnectionsStore);
const { confirm } = useConfirm();
const isCreateDialogOpen = ref(false);

const busy = computed(() => clientsBusy.value || connectionsBusy.value);
const displayErrorMessage = computed(
  () => connectionsErrorMessage.value ?? clientsErrorMessage.value
);

function openCreateDialog() {
  isCreateDialogOpen.value = true;
}

function closeCreateDialog() {
  isCreateDialogOpen.value = false;
}

async function createConnection(request: CreateMcpProjectConnectionRequest) {
  const created = await projectConnectionsStore.createConnection(request);
  if (created) {
    closeCreateDialog();
  }
}

async function revokeConnection(connection: McpProjectConnection) {
  const accepted = await confirm({
    title: 'Revoke project connection',
    message: `Revoke ${connection.name}? Its resource URL will no longer be authorisable.`,
    confirmLabel: 'Revoke',
    danger: true
  });
  if (!accepted) {
    return;
  }

  await projectConnectionsStore.revokeConnection(connection);
}

async function copyResourceUrl(connection: McpProjectConnection) {
  try {
    await navigator.clipboard.writeText(connection.resourceUrl);
    successMessage.value = `Copied ${connection.name} URL to clipboard.`;
    connectionsErrorMessage.value = null;
  } catch {
    connectionsErrorMessage.value = 'Could not copy to clipboard automatically.';
  }
}

function formatDate(value: string) {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString();
}

onMounted(async () => {
  projectConnectionsStore.clearMessages();
  await Promise.all([
    clientAccountsStore.loadClients(),
    projectConnectionsStore.loadConnections()
  ]);
});
</script>

<style scoped>
.project-connections-page {
  max-width: 980px;
}

.project-connection-row {
  align-items: flex-start;
}

.project-connection-main {
  display: grid;
  gap: 0.45rem;
}

.project-connection-heading,
.project-connection-url-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.4rem;
}

.project-connection-owner,
.project-connection-metadata {
  margin: 0;
}

.project-connection-owner span,
.project-connection-metadata {
  color: var(--bo-ink-muted);
}

.project-connection-url-row code {
  min-width: 0;
  overflow-wrap: anywhere;
}

.project-connection-url-row .btn {
  padding-block: 0.35rem;
}

@media (max-width: 720px) {
  .project-connection-row {
    align-items: stretch;
    flex-direction: column;
  }

  .entity-row-actions {
    justify-content: flex-end;
  }
}
</style>
