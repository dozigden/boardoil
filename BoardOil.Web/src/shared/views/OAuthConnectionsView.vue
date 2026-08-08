<template>
  <section class="oauth-connections-page entity-rows-page">
    <header class="oauth-connections-header">
      <div>
        <h2>OAuth Connections</h2>
        <p v-if="administrator">Inspect and revoke OAuth installations across all users.</p>
        <p v-else>Manage the OAuth installations you have authorized.</p>
      </div>
      <button type="button" class="btn btn--secondary" :disabled="busy" @click="loadConnections">
        Refresh
      </button>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <div
      class="oauth-connections-layout"
      :class="{ 'oauth-connections-layout--administrator': administrator }"
    >
      <section class="entity-rows-list oauth-connections-list" aria-label="OAuth connections">
        <p v-if="!busy && connections.length === 0" class="oauth-connections-empty">
          No OAuth connections have been authorized yet.
        </p>

        <article v-for="connection in connections" :key="connection.id" class="entity-row oauth-connection-row">
          <div class="entity-row-main oauth-connection-main">
            <header class="oauth-connection-title-row">
              <h3 class="entity-row-title">{{ connection.name }}</h3>
              <span class="oauth-resource">{{ connection.resourceType.toUpperCase() }}</span>
            </header>

            <p v-if="administrator" class="oauth-owner">
              {{ connection.owner.displayName }} <span>@{{ connection.owner.userName }}</span>
            </p>

            <dl class="oauth-connection-details">
              <div>
                <dt>Application</dt>
                <dd>{{ connection.oAuthClientDisplayName }}</dd>
              </div>
              <div>
                <dt>Scopes</dt>
                <dd>{{ formatScopes(connection.approvedScopes) }}</dd>
              </div>
              <div>
                <dt>Created</dt>
                <dd>{{ formatDate(connection.createdAtUtc) }}</dd>
              </div>
              <div>
                <dt>Last authorized</dt>
                <dd>{{ formatDate(connection.lastAuthorizedAtUtc) }}</dd>
              </div>
            </dl>
          </div>

          <div class="entity-row-actions">
            <button
              type="button"
              class="btn btn--danger"
              :disabled="busy"
              @click="revokeConnection(connection)"
            >
              Revoke
            </button>
          </div>
        </article>
      </section>

      <aside v-if="!administrator" class="panel panel-stack oauth-setup-panel">
        <h3>Codex project setup</h3>
        <p>
          Copy this into the repository's <code>.codex/config.toml</code>.
        </p>
        <p v-if="configurationErrorMessage" class="error">
          {{ configurationErrorMessage }}
        </p>
        <p v-else-if="!configSnippet">Loading OAuth configuration...</p>
        <div v-else class="oauth-config-block">
          <pre>{{ configSnippet }}</pre>
          <button type="button" class="btn btn--secondary" :disabled="busy" @click="copyConfig">
            Copy config
          </button>
        </div>
      </aside>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { getMcpOAuthMetadata } from '../api/oauthMetadataApi';
import { createOAuthConnectionsApi } from '../api/oauthConnectionsApi';
import { createSystemOAuthConnectionsApi } from '../api/systemOAuthConnectionsApi';
import { useConfirm } from '../composables/useConfirm';
import type { OAuthConnection } from '../types/oauthConnectionTypes';
import { buildCodexOAuthConfig } from '../utils/oauthConnectionPresentation';

const props = defineProps<{
  administrator?: boolean;
}>();

const api = createOAuthConnectionsApi();
const systemApi = createSystemOAuthConnectionsApi();
const { confirm } = useConfirm();
const connections = ref<OAuthConnection[]>([]);
const busy = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const configurationErrorMessage = ref<string | null>(null);
const mcpResourceUrl = ref<string | null>(null);
const configSnippet = computed(() => {
  if (!mcpResourceUrl.value) {
    return null;
  }

  return buildCodexOAuthConfig(mcpResourceUrl.value);
});

onMounted(() => {
  void loadConnections();
  if (!props.administrator) {
    void loadMcpResource();
  }
});

async function loadMcpResource() {
  configurationErrorMessage.value = null;
  const result = await getMcpOAuthMetadata();
  if (!result.ok) {
    configurationErrorMessage.value = result.error.message;
    return;
  }

  mcpResourceUrl.value = result.data.resource;
}

async function loadConnections() {
  busy.value = true;
  errorMessage.value = null;
  try {
    const result = props.administrator
      ? await systemApi.getConnections()
      : await api.getOwnConnections();
    if (!result.ok) {
      connections.value = [];
      errorMessage.value = result.error.message;
      return;
    }

    connections.value = result.data;
  } finally {
    busy.value = false;
  }
}

async function revokeConnection(connection: OAuthConnection) {
  const ownerContext = props.administrator ? ` for ${connection.owner.displayName}` : '';
  const confirmed = await confirm({
    title: 'Revoke OAuth connection',
    message: `Revoke and remove "${connection.name}"${ownerContext}? Its current access and refresh credentials will stop working immediately.`,
    confirmLabel: 'Revoke',
    danger: true
  });
  if (!confirmed) {
    return;
  }

  busy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = props.administrator
      ? await systemApi.revokeConnection(connection.id)
      : await api.revokeOwnConnection(connection.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    successMessage.value = `Revoked and removed OAuth connection ${connection.name}.`;
    await loadConnections();
  } finally {
    busy.value = false;
  }
}

async function copyConfig() {
  if (!configSnippet.value) {
    configurationErrorMessage.value = 'OAuth configuration has not loaded yet.';
    return;
  }

  try {
    await navigator.clipboard.writeText(configSnippet.value);
    successMessage.value = 'Copied Codex OAuth configuration.';
    errorMessage.value = null;
  } catch {
    errorMessage.value = 'Could not copy the configuration automatically.';
  }
}

function formatScopes(scopes: string[]) {
  return scopes.length === 0 ? 'None' : scopes.join(', ');
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
</script>

<style scoped>
.oauth-connections-page {
  max-width: 1180px;
}

.oauth-connections-header,
.oauth-connection-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.oauth-connections-header {
  margin-bottom: 1rem;
}

.oauth-connections-header h2,
.oauth-connections-header p,
.oauth-connection-title-row h3,
.oauth-setup-panel h3,
.oauth-setup-panel p {
  margin: 0;
}

.oauth-connections-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(260px, 340px);
  gap: 1rem;
  align-items: start;
}

.oauth-connections-layout--administrator {
  grid-template-columns: minmax(0, 1fr);
}

.oauth-connections-list {
  min-width: 0;
}

.oauth-connections-empty {
  margin: 0;
  padding: 1rem;
}

.oauth-connection-row {
  align-items: flex-start;
}

.oauth-connection-main {
  min-width: 0;
}

.oauth-connection-title-row {
  justify-content: flex-start;
  flex-wrap: wrap;
}

.oauth-resource {
  border: 1px solid var(--bo-border-default);
  border-radius: 999px;
  padding: 0.1rem 0.5rem;
  font-size: 0.78rem;
}

.oauth-resource,
.oauth-owner span {
  color: var(--bo-ink-muted);
}

.oauth-owner {
  margin: 0.35rem 0 0;
}

.oauth-connection-details {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 0.65rem 1rem;
  margin: 0.85rem 0;
}

.oauth-connection-details div {
  min-width: 0;
}

.oauth-connection-details dt {
  color: var(--bo-ink-muted);
  font-size: 0.78rem;
}

.oauth-connection-details dd {
  margin: 0.15rem 0 0;
  overflow-wrap: anywhere;
}

.oauth-setup-panel {
  position: sticky;
  top: 1rem;
}

.oauth-config-block {
  display: grid;
  gap: 0.65rem;
}

.oauth-config-block pre {
  margin: 0;
  padding: 0.75rem;
  overflow-x: auto;
  border-radius: 0.4rem;
  background: var(--bo-surface-muted);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

@media (max-width: 820px) {
  .oauth-connections-layout {
    grid-template-columns: 1fr;
  }

  .oauth-setup-panel {
    position: static;
  }
}
</style>
