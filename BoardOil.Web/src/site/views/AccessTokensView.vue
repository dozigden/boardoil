<template>
  <section class="authentication-method-page">
    <header class="authentication-method-header">
      <div>
        <p>Personal Access Tokens for MCP clients so that they can act as you.  If you want an agent to have its own identity use a client account instead.</p>
      </div>
      <button type="button" class="btn" :disabled="isBusy" @click="openCreateDialog">Create access token</button>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <div class="authentication-method-layout">
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

      <aside class="authentication-method-sidecar panel panel-stack">
        <h3>Setup snippets</h3>

        <article class="panel panel--base panel--compact panel-stack panel-stack--tight machine-pat-setup-item">
          <h4>MCP endpoint</h4>
          <div class="authentication-setup-code-block authentication-setup-code-block--endpoint">
            <code class="authentication-setup-code authentication-setup-code--inline">{{ mcpEndpoint }}</code>
            <button
              type="button"
              class="btn btn--secondary authentication-setup-copy"
              :disabled="isBusy"
              aria-label="Copy endpoint"
              title="Copy endpoint"
              @click="copySnippet(mcpEndpoint, 'MCP endpoint')"
            >
              <Copy :size="14" aria-hidden="true" />
            </button>
          </div>
        </article>

        <article class="panel panel--base panel--compact panel-stack panel-stack--tight machine-pat-setup-item">
          <header class="machine-pat-setup-item-header">
            <h4>Generic MCP config snippet</h4>
            <div class="btn-tab-list" role="tablist" aria-label="Generic MCP config formats">
              <button
                type="button"
                class="btn btn--tab"
                :class="{ 'is-active': configSnippetTab === 'json' }"
                role="tab"
                :aria-selected="configSnippetTab === 'json'"
                @click="configSnippetTab = 'json'"
              >
                JSON (Copilot)
              </button>
              <button
                type="button"
                class="btn btn--tab"
                :class="{ 'is-active': configSnippetTab === 'toml' }"
                role="tab"
                :aria-selected="configSnippetTab === 'toml'"
                @click="configSnippetTab = 'toml'"
              >
                TOML (Codex)
              </button>
            </div>
          </header>
          <div class="authentication-setup-code-block">
            <pre class="authentication-setup-code">{{ selectedConfigSnippet }}</pre>
            <button
              type="button"
              class="btn btn--secondary authentication-setup-copy"
              :disabled="isBusy"
              :aria-label="`Copy ${selectedConfigSnippetLabel} config`"
              :title="`Copy ${selectedConfigSnippetLabel} config`"
              @click="copySnippet(selectedConfigSnippet, `${selectedConfigSnippetLabel} config snippet`)"
            >
              <Copy :size="14" aria-hidden="true" />
            </button>
          </div>
        </article>

        <article class="panel panel--base panel--compact panel-stack panel-stack--tight machine-pat-setup-item">
          <header class="machine-pat-setup-item-header">
            <h4>Manual test</h4>
            <div class="btn-tab-list" role="tablist" aria-label="Manual test examples">
              <button
                type="button"
                class="btn btn--tab"
                :class="{ 'is-active': manualTestTab === 'curl' }"
                role="tab"
                :aria-selected="manualTestTab === 'curl'"
                @click="manualTestTab = 'curl'"
              >
                Curl
              </button>
              <button
                type="button"
                class="btn btn--tab"
                :class="{ 'is-active': manualTestTab === 'powershell' }"
                role="tab"
                :aria-selected="manualTestTab === 'powershell'"
                @click="manualTestTab = 'powershell'"
              >
                PowerShell
              </button>
            </div>
          </header>
          <div class="authentication-setup-code-block">
            <pre class="authentication-setup-code">{{ selectedManualTestSnippet }}</pre>
            <button
              type="button"
              class="btn btn--secondary authentication-setup-copy"
              :disabled="isBusy"
              :aria-label="`Copy ${selectedManualTestSnippetLabel} example`"
              :title="`Copy ${selectedManualTestSnippetLabel} example`"
              @click="copySnippet(selectedManualTestSnippet, `${selectedManualTestSnippetLabel} manual test command`)"
            >
              <Copy :size="14" aria-hidden="true" />
            </button>
          </div>
        </article>

        <p class="machine-pat-setup-note">
          Use PAT as the direct bearer token for MCP calls. PATs do not use refresh-token login.
        </p>
      </aside>
    </div>

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
      @copy="copyPlainTextPat"
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
import type { AccessToken, CreateAccessTokenRequest } from '../../shared/types/authTypes';

const authApi = createAuthApi();
const { confirm } = useConfirm();

const tokens = ref<AccessToken[]>([]);
const loading = ref(false);
const createBusy = ref(false);
const revokeBusyTokenId = ref<number | null>(null);
const isCreateDialogOpen = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const plainTextPat = ref<string | null>(null);
const plainTextPatName = ref<string>('');
const configSnippetTab = ref<'json' | 'toml'>('json');
const manualTestTab = ref<'curl' | 'powershell'>('curl');

const isBusy = computed(() => loading.value || createBusy.value || revokeBusyTokenId.value !== null);
const isSecretModalOpen = computed(() => plainTextPat.value !== null);
const mcpEndpoint = computed(() => `${window.location.origin}/mcp`);
const apiBaseUrl = computed(() => window.location.origin);
const userDefaultScopes = ['mcp:read', 'mcp:write'];
const userAllowedScopes = ['mcp:read', 'mcp:write'];
const genericConfigSnippetJson = computed(() =>
  `{
  "mcpServers": {
    "boardoil": {
      "transport": "http",
      "url": "${mcpEndpoint.value}",
      "headers": {
        "Authorization": "Bearer <YOUR_ACCESS_TOKEN>"
      }
    }
  }
}`
);
const genericConfigSnippetToml = computed(() =>
  `[mcp_servers.boardoil]
url = "${mcpEndpoint.value}"
bearer_token_env_var = "BOARDOIL_MCP_TOKEN"`
);
const selectedConfigSnippet = computed(() => (configSnippetTab.value === 'json' ? genericConfigSnippetJson.value : genericConfigSnippetToml.value));
const selectedConfigSnippetLabel = computed(() => (configSnippetTab.value === 'json' ? 'JSON' : 'TOML'));
const manualTestCurlSnippet = computed(() =>
  `curl -sS -X POST ${mcpEndpoint.value} \\
  -H "Authorization: Bearer <YOUR_ACCESS_TOKEN>" \\
  -H "Content-Type: application/json" \\
  --data '{"jsonrpc":"2.0","id":"tools-list","method":"tools/list"}'`
);
const manualTestPowerShellSnippet = computed(() =>
  `$endpoint = "${mcpEndpoint.value}"
$headers = @{
  Authorization = "Bearer <YOUR_ACCESS_TOKEN>"
  "Content-Type" = "application/json"
}
$body = '{"jsonrpc":"2.0","id":"tools-list","method":"tools/list"}'
Invoke-RestMethod -Method Post -Uri $endpoint -Headers $headers -Body $body`
);
const selectedManualTestSnippet = computed(() => (manualTestTab.value === 'curl' ? manualTestCurlSnippet.value : manualTestPowerShellSnippet.value));
const selectedManualTestSnippetLabel = computed(() => (manualTestTab.value === 'curl' ? 'Curl' : 'PowerShell'));

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

async function copyPlainTextPat() {
  if (!plainTextPat.value) {
    return;
  }

  const copied = await copyToClipboard(plainTextPat.value, `token ${plainTextPatName.value}`);
  if (!copied) {
    return;
  }
}

async function copySnippet(text: string, label: string) {
  await copyToClipboard(text, label);
}

async function copyToClipboard(text: string, label: string) {
  try {
    await navigator.clipboard.writeText(text);
    successMessage.value = `Copied ${label} to clipboard.`;
    errorMessage.value = null;
    return true;
  } catch {
    errorMessage.value = 'Could not copy to clipboard automatically.';
    return false;
  }
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
.machine-pat-setup-item h4 {
  margin: 0;
}

.machine-pat-setup-item-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.55rem;
}

.machine-pat-setup-note {
  margin: 0;
  color: var(--bo-ink-muted);
}

</style>
