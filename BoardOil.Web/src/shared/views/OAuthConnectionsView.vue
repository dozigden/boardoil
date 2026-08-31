<template>
  <section
    class="authentication-method-page"
    :class="{ 'authentication-method-page--standalone': administrator }"
  >
    <section v-if="!administrator" class="authentication-method-setup panel panel-stack">
      <header class="authentication-method-setup-header">
        <h3>Connect an MCP client</h3>
        <div class="authentication-method-setup-actions">
          <div class="btn-tab-list" role="tablist" aria-label="OAuth client setup">
            <button
              type="button"
              class="btn btn--tab"
              :class="{ 'is-active': setupClient === 'vscode' }"
              role="tab"
              :aria-selected="setupClient === 'vscode'"
              @click="setupClient = 'vscode'"
            >
              VS Code
            </button>
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
          <RouterLink
            :to="{ name: 'user-admin-mcp-help', hash: setupHelpHash }"
            class="btn btn--secondary authentication-method-help-link"
          >
            Help
          </RouterLink>
        </div>
      </header>

      <div class="authentication-method-setup-content">
        <div class="authentication-method-setup-section">
          <ol v-if="setupClient === 'vscode'" class="authentication-method-setup-steps">
            <li>Run <code>MCP: Add Server</code> and choose <code>HTTP</code>.</li>
            <li>
              Enter
              <span
                v-if="setupSnippet"
                class="authentication-setup-code-block authentication-setup-code-block--endpoint authentication-method-inline-copy"
              >
                <code class="authentication-setup-code authentication-setup-code--inline">{{ setupSnippet }}</code>
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
              </span>
              <span v-else-if="configurationErrorMessage" class="error">{{ configurationErrorMessage }}</span>
              <span v-else>the OAuth URL when it has loaded</span>, name it <code>boardoil</code>, and choose
              <code>Global</code> or <code>Workspace</code>.
            </li>
            <li>Start the server and complete sign-in when VS Code opens BoardOil.</li>
          </ol>
          <p v-else-if="setupClient === 'codex'">
            Add this to <code>~/.codex/config.toml</code>, then run <code>codex mcp login boardoil</code>,
            or tell your agent to connect.
          </p>
          <p v-else>
            Run this once, then run <code>claude mcp login boardoil</code> or use <code>/mcp</code> in Claude Code.
            Use <code>--scope local</code> for a private configuration tied to the current project, or
            <code>--scope project</code> for a shared <code>.mcp.json</code> configuration.
          </p>
        </div>

        <div v-if="setupClient !== 'vscode'" class="authentication-method-setup-section">
          <p v-if="configurationErrorMessage" class="error">
            {{ configurationErrorMessage }}
          </p>
          <p v-else-if="!setupSnippet">Loading OAuth configuration...</p>
          <div v-else class="authentication-setup-code-block">
            <pre class="authentication-setup-code">{{ setupSnippet }}</pre>
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
        </div>
      </div>
    </section>

    <header class="authentication-method-header">
      <div>
        <h2 v-if="administrator">OAuth Connections</h2>
        <h3 v-else>Authorized connections</h3>
        <p v-if="administrator">Inspect and revoke OAuth installations across all users.</p>
        <p v-else>Manage the OAuth installations you have authorized.</p>
      </div>
      <button type="button" class="btn btn--secondary" :disabled="busy" @click="loadConnections">
        Refresh
      </button>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

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
              <dd>{{ connection.oauthClientDisplayName }}</dd>
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
  </section>
</template>

<script setup lang="ts">
import { Copy } from '@lucide/vue';
import { computed, onMounted, ref } from 'vue';
import { RouterLink } from 'vue-router';
import { getMcpOAuthMetadata } from '../api/oauthMetadataApi';
import { createOAuthConnectionsApi } from '../api/oauthConnectionsApi';
import { createSystemOAuthConnectionsApi } from '../api/systemOAuthConnectionsApi';
import { useConfirm } from '../composables/useConfirm';
import { useUiFeedbackStore } from '../stores/uiFeedbackStore';
import type { OAuthConnection } from '../types/oauthConnectionTypes';
import {
  buildClaudeCodeOAuthCommand,
  buildCodexOAuthConfig,
  buildVsCodeOAuthUrl
} from '../utils/oauthConnectionPresentation';
import { copyTextToClipboard } from '../utils/clipboard';

type SetupClient = 'vscode' | 'codex' | 'claude-code';

const props = defineProps<{
  administrator?: boolean;
}>();

const api = createOAuthConnectionsApi();
const systemApi = createSystemOAuthConnectionsApi();
const { confirm } = useConfirm();
const feedback = useUiFeedbackStore();
const connections = ref<OAuthConnection[]>([]);
const busy = ref(false);
const errorMessage = ref<string | null>(null);
const configurationErrorMessage = ref<string | null>(null);
const mcpResourceUrl = ref<string | null>(null);
const setupClient = ref<SetupClient>('vscode');
const setupHelpHash = computed(() => {
  if (setupClient.value === 'vscode') {
    return '#vs-code-and-github-copilot';
  }

  if (setupClient.value === 'claude-code') {
    return '#claude-code';
  }

  return '#codex';
});
const setupSnippet = computed(() => {
  if (!mcpResourceUrl.value) {
    return null;
  }

  if (setupClient.value === 'vscode') {
    return buildVsCodeOAuthUrl(mcpResourceUrl.value);
  }

  if (setupClient.value === 'claude-code') {
    return buildClaudeCodeOAuthCommand(mcpResourceUrl.value);
  }

  return buildCodexOAuthConfig(mcpResourceUrl.value);
});
const copyButtonLabel = computed(() => {
  if (setupClient.value === 'vscode') {
    return 'Copy URL';
  }

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
  try {
    const result = props.administrator
      ? await systemApi.revokeConnection(connection.id)
      : await api.revokeOwnConnection(connection.id);
    if (!result.ok) {
      feedback.showToast(result.error.message, 'error');
      return;
    }

    feedback.showToast('Revoked successfully.');
    await loadConnections();
  } finally {
    busy.value = false;
  }
}

async function copyConfig() {
  if (!setupSnippet.value) {
    configurationErrorMessage.value = 'OAuth configuration has not loaded yet.';
    return;
  }

  const copied = await copyTextToClipboard(setupSnippet.value);
  if (copied) {
    feedback.showToast('Copied');
    return;
  }

  feedback.showToast('Could not copy the setup automatically.', 'error');
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

</style>
