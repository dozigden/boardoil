<template>
  <section
    class="authentication-method-page"
    :class="{ 'authentication-method-page--standalone': administrator }"
  >
    <header class="authentication-method-header">
      <div>
        <h2 v-if="administrator">OAuth Connections</h2>
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
      class="authentication-method-layout"
      :class="{ 'authentication-method-layout--single': administrator }"
    >
      <section class="authentication-method-main entity-rows-list" aria-label="OAuth connections">
        <p v-if="!busy && connections.length === 0" class="authentication-method-empty">
          No OAuth connections have been authorized yet.
        </p>

        <article v-for="connection in connections" :key="connection.id" class="entity-row authentication-record-row">
          <div class="entity-row-main authentication-record-main">
            <header class="authentication-record-title">
              <h3 class="entity-row-title">{{ connection.name }}</h3>
              <span class="oauth-resource">{{ connection.resourceType.toUpperCase() }}</span>
            </header>

            <p v-if="administrator" class="oauth-owner">
              {{ connection.owner.displayName }} <span>@{{ connection.owner.userName }}</span>
            </p>

            <dl class="authentication-record-details">
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
              <div>
                <dt>Last used</dt>
                <dd>{{ formatDate(connection.lastUsedAtUtc) }}</dd>
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

      <aside v-if="!administrator" class="authentication-method-sidecar panel panel-stack">
        <h3>Project setup</h3>
        <article class="panel panel--base panel--compact panel-stack panel-stack--tight oauth-setup-item">
          <div class="btn-tab-list" role="tablist" aria-label="OAuth client setup">
            <button
              type="button"
              class="btn btn--tab"
              :class="{ 'is-active': setupClient === 'codex' }"
              role="tab"
              :aria-selected="setupClient === 'codex'"
              @click="setupClient = 'codex'"
            >
              Codex
            </button>
            <button
              type="button"
              class="btn btn--tab"
              :class="{ 'is-active': setupClient === 'claude-code' }"
              role="tab"
              :aria-selected="setupClient === 'claude-code'"
              @click="setupClient = 'claude-code'"
            >
              Claude Code
            </button>
          </div>
          <p v-if="setupClient === 'codex'">
            Copy this into the repository's <code>.codex/config.toml</code>.
          </p>
          <p v-else>
            Run this from the repository, then use <code>/mcp</code> in Claude Code to authenticate.
          </p>
          <p v-if="configurationErrorMessage" class="error">
            {{ configurationErrorMessage }}
          </p>
          <p v-else-if="!configSnippet">Loading OAuth configuration...</p>
          <div v-else class="authentication-setup-code-block">
            <pre class="authentication-setup-code">{{ configSnippet }}</pre>
            <button
              type="button"
              class="btn btn--secondary authentication-setup-copy"
              :disabled="busy"
              :aria-label="copyButtonLabel"
              :title="copyButtonLabel"
              @click="copyConfig"
            >
              <Copy :size="14" aria-hidden="true" />
            </button>
          </div>
        </article>
      </aside>
    </div>
  </section>
</template>

<script setup lang="ts">
import { Copy } from 'lucide-vue-next';
import { computed, onMounted, ref } from 'vue';
import { getMcpOAuthMetadata } from '../api/oauthMetadataApi';
import { createOAuthConnectionsApi } from '../api/oauthConnectionsApi';
import { createSystemOAuthConnectionsApi } from '../api/systemOAuthConnectionsApi';
import { useConfirm } from '../composables/useConfirm';
import type { OAuthConnection } from '../types/oauthConnectionTypes';
import {
  buildClaudeCodeOAuthCommand,
  buildCodexOAuthConfig
} from '../utils/oauthConnectionPresentation';

type SetupClient = 'codex' | 'claude-code';

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
const setupClient = ref<SetupClient>('codex');
const configSnippet = computed(() => {
  if (!mcpResourceUrl.value) {
    return null;
  }

  if (setupClient.value === 'claude-code') {
    return buildClaudeCodeOAuthCommand(mcpResourceUrl.value);
  }

  return buildCodexOAuthConfig(mcpResourceUrl.value);
});
const copyButtonLabel = computed(() => {
  if (setupClient.value === 'claude-code') {
    return 'Copy command';
  }

  return 'Copy config';
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
    if (setupClient.value === 'claude-code') {
      successMessage.value = 'Copied Claude Code OAuth command.';
    } else {
      successMessage.value = 'Copied Codex OAuth configuration.';
    }
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

.oauth-setup-item {
  min-width: 0;
}

</style>
