<template>
  <section class="authentication-method-page">
    <section class="authentication-method-setup panel panel-stack">
      <header class="authentication-method-setup-header">
        <div>
          <h3>Connect with an access token</h3>
          <p>Set up the client after creating a token with the scopes it needs.</p>
        </div>
        <div class="btn-tab-list" role="tablist" aria-label="Access token client setup">
          <button
            type="button"
            class="btn btn--tab"
            :class="{ 'is-active': configSnippetTab === 'claude-code' }"
            role="tab"
            :aria-selected="configSnippetTab === 'claude-code'"
            @click="configSnippetTab = 'claude-code'"
          >
            Claude Code
          </button>
          <button
            type="button"
            class="btn btn--tab"
            :class="{ 'is-active': configSnippetTab === 'codex' }"
            role="tab"
            :aria-selected="configSnippetTab === 'codex'"
            @click="configSnippetTab = 'codex'"
          >
            Codex
          </button>
        </div>
      </header>

      <div class="authentication-method-setup-content">
        <section class="authentication-method-setup-section">
          <h4>MCP endpoint</h4>
          <div class="authentication-setup-code-block authentication-setup-code-block--endpoint">
            <code class="authentication-setup-code authentication-setup-code--inline">{{ mcpEndpoint }}</code>
            <button
              type="button"
              class="btn btn--secondary authentication-setup-copy"
              :disabled="isBusy"
              aria-label="Copy endpoint"
              title="Copy endpoint"
              @click="copySnippet(mcpEndpoint)"
            >
              <Copy :size="14" aria-hidden="true" />
            </button>
          </div>
        </section>

        <section class="authentication-method-setup-section">
          <h4>{{ selectedConfigSnippetLabel }} setup</h4>
          <div class="authentication-setup-code-block">
            <pre class="authentication-setup-code">{{ selectedConfigSnippet }}</pre>
            <button
              type="button"
              class="btn btn--secondary authentication-setup-copy"
              :disabled="isBusy"
              :aria-label="`Copy ${selectedConfigSnippetLabel} config`"
              :title="`Copy ${selectedConfigSnippetLabel} config`"
              @click="copySnippet(selectedConfigSnippet)"
            >
              <Copy :size="14" aria-hidden="true" />
            </button>
          </div>
        </section>
      </div>

      <p class="machine-pat-setup-note">
        Set <code>BOARDOIL_MCP_TOKEN</code> in the environment before using these examples. Keep access tokens in private user configuration and never commit them to source control.
      </p>
    </section>

    <header class="authentication-method-header">
      <div>
        <h3>Access tokens</h3>
        <p>Access tokens are a manual fallback for clients that cannot use OAuth. They act as you; use a client account when an agent needs its own identity.</p>
      </div>
      <button type="button" class="btn" :disabled="isBusy" @click="openCreateDialog">Create access token</button>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section class="authentication-method-main entity-rows-list" aria-label="Access tokens">
      <p v-if="tokens.length === 0" class="authentication-method-empty">No access tokens have been created yet.</p>

      <AccessTokenListItem
        v-for="token in tokens"
        :key="token.id"
        :token="token"
        :is-busy="isBusy"
        :token-status="tokenStatus"
        :format-date="formatDate"
        @revoke="revokeToken"
      />
    </section>

    <AccessTokenCreateDialog
      :open="isCreateDialogOpen"
      :busy="isBusy"
      :default-scopes="userDefaultScopes"
      :allowed-scopes="userAllowedScopes"
      @close="closeCreateDialog"
      @submit="createToken"
    />
    <AccessTokenSecretModal
      :open="isSecretModalOpen"
      :busy="isBusy"
      :token="plainTextPat"
      :token-name="plainTextPatName"
      @close="dismissPlainTextPat"
    />
  </section>
</template>

<script setup lang="ts">
import { Copy } from 'lucide-vue-next';
import { computed, onMounted, ref } from 'vue';
import { createAuthApi } from '../../shared/api/authApi';
import AccessTokenCreateDialog from '../../shared/components/AccessTokenCreateDialog.vue';
import AccessTokenListItem from '../../shared/components/AccessTokenListItem.vue';
import AccessTokenSecretModal from '../../shared/components/AccessTokenSecretModal.vue';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { AccessToken, CreateAccessTokenRequest } from '../../shared/types/authTypes';
import {
  buildClaudeCodeAccessTokenCommand,
  buildCodexAccessTokenConfig
} from '../../shared/utils/accessTokenPresentation';
import { copyTextToClipboard } from '../../shared/utils/clipboard';

const authApi = createAuthApi();
const { confirm } = useConfirm();
const feedback = useUiFeedbackStore();

const tokens = ref<AccessToken[]>([]);
const loading = ref(false);
const createBusy = ref(false);
const revokeBusyTokenId = ref<number | null>(null);
const isCreateDialogOpen = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const plainTextPat = ref<string | null>(null);
const plainTextPatName = ref<string>('');
const configSnippetTab = ref<'claude-code' | 'codex'>('claude-code');

const isBusy = computed(() => loading.value || createBusy.value || revokeBusyTokenId.value !== null);
const isSecretModalOpen = computed(() => plainTextPat.value !== null);
const mcpEndpoint = computed(() => `${window.location.origin}/mcp`);
const userDefaultScopes = ['mcp:read', 'mcp:write'];
const userAllowedScopes = ['mcp:read', 'mcp:write'];
const claudeCodeConfigSnippet = computed(() =>
  buildClaudeCodeAccessTokenCommand(mcpEndpoint.value)
);
const codexConfigSnippet = computed(() =>
  buildCodexAccessTokenConfig(mcpEndpoint.value)
);
const selectedConfigSnippet = computed(() => {
  if (configSnippetTab.value === 'claude-code') {
    return claudeCodeConfigSnippet.value;
  }

  return codexConfigSnippet.value;
});
const selectedConfigSnippetLabel = computed(() => {
  if (configSnippetTab.value === 'claude-code') {
    return 'Claude Code';
  }

  return 'Codex';
});

onMounted(async () => {
  await loadInitialData();
});

async function loadInitialData() {
  loading.value = true;
  errorMessage.value = null;
  try {
    const tokensResult = await authApi.getAccessTokens();
    if (!tokensResult.ok) {
      errorMessage.value = tokensResult.error.message;
      return;
    }

    tokens.value = sortTokens(tokensResult.data);
  } finally {
    loading.value = false;
  }
}

async function refreshTokens() {
  loading.value = true;
  errorMessage.value = null;
  try {
    const result = await authApi.getAccessTokens();
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

async function createToken(payload: CreateAccessTokenRequest) {
  createBusy.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.createAccessToken(payload);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    tokens.value = sortTokens([result.data.token, ...tokens.value.filter(token => token.id !== result.data.token.id)]);
    plainTextPat.value = result.data.plainTextToken;
    plainTextPatName.value = result.data.token.name;
    isCreateDialogOpen.value = false;
    successMessage.value = `Created access token ${result.data.token.name}.`;
  } finally {
    createBusy.value = false;
  }
}

async function revokeToken(token: AccessToken) {
  if (token.revokedAtUtc) {
    return;
  }

  const confirmed = await confirm({
    title: 'Revoke access token',
    message: `Revoke access token "${token.name}"? This cannot be undone.`,
    confirmLabel: 'Revoke',
    danger: true
  });
  if (!confirmed) {
    return;
  }

  revokeBusyTokenId.value = token.id;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const result = await authApi.revokeAccessToken(token.id);
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    successMessage.value = `Revoked access token ${token.name}.`;
    await refreshTokens();
  } finally {
    revokeBusyTokenId.value = null;
  }
}

async function copySnippet(text: string) {
  const copied = await copyTextToClipboard(text);
  if (copied) {
    feedback.showToast('Copied');
    return;
  }

  feedback.showToast('Could not copy to clipboard automatically.', 'error');
}

function dismissPlainTextPat() {
  plainTextPat.value = null;
  plainTextPatName.value = '';
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

function tokenStatus(token: AccessToken) {
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

function sortTokens(items: AccessToken[]) {
  return [...items].sort((left, right) => {
    const leftTimestamp = Date.parse(left.createdAtUtc);
    const rightTimestamp = Date.parse(right.createdAtUtc);
    return rightTimestamp - leftTimestamp;
  });
}
</script>

<style scoped>
.machine-pat-setup-note {
  margin: 0;
  color: var(--bo-ink-muted);
}

</style>
