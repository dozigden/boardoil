<template>
  <section class="machine-access-view">
    <header class="machine-access-header">
      <div>
        <h2>Access Tokens</h2>
        <p>Create and manage Personal Access Tokens (PATs) for MCP and REST API clients.</p>
      </div>
    </header>

    <div class="machine-access-layout">
      <section class="machine-access-column machine-access-column--tokens">
        <div class="machine-access-actions">
          <button type="button" class="btn" :disabled="isBusy" @click="openCreateDialog">Create access token</button>
        </div>
        <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
        <p v-if="successMessage" class="success">{{ successMessage }}</p>

        <section class="machine-pat-list">
          <header class="machine-pat-list-header">
            <h3>Existing tokens</h3>
          </header>

          <p v-if="tokens.length === 0" class="machine-pat-empty">No access tokens have been created yet.</p>

          <article v-for="token in tokens" :key="token.id" class="panel panel-stack panel-stack--compact machine-pat-item">
            <div class="machine-pat-item-header">
              <strong>{{ token.name }}</strong>
              <span class="badge-group">
                <span class="badge">{{ tokenStatus(token) }}</span>
                <span class="badge">{{ token.tokenPrefix }}</span>
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
                class="btn btn--secondary"
                :disabled="isBusy || token.revokedAtUtc !== null"
                @click="revokeToken(token)"
              >
                {{ token.revokedAtUtc ? 'Revoked' : 'Revoke token' }}
              </button>
            </div>
          </article>
        </section>
      </section>

      <aside class="machine-access-column machine-access-column--guide">
        <section class="panel panel-stack machine-pat-setup">
          <h3>Setup snippets</h3>

          <article class="panel panel--base panel--compact panel-stack panel-stack--tight machine-pat-setup-item">
            <h4>MCP endpoint</h4>
            <div class="machine-pat-code-block machine-pat-code-block--endpoint">
              <code class="machine-pat-setup-code machine-pat-inline-code">{{ mcpEndpoint }}</code>
              <button
                type="button"
                class="btn btn--secondary machine-pat-copy-icon"
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
              <div class="machine-pat-tab-list" role="tablist" aria-label="Generic MCP config formats">
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
            <div class="machine-pat-code-block">
              <pre class="machine-pat-setup-code">{{ selectedConfigSnippet }}</pre>
              <button
                type="button"
                class="btn btn--secondary machine-pat-copy-icon"
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
              <div class="machine-pat-tab-list" role="tablist" aria-label="Manual test examples">
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
            <div class="machine-pat-code-block">
              <pre class="machine-pat-setup-code">{{ selectedManualTestSnippet }}</pre>
              <button
                type="button"
                class="btn btn--secondary machine-pat-copy-icon"
                :disabled="isBusy"
                :aria-label="`Copy ${selectedManualTestSnippetLabel} example`"
                :title="`Copy ${selectedManualTestSnippetLabel} example`"
                @click="copySnippet(selectedManualTestSnippet, `${selectedManualTestSnippetLabel} manual test command`)"
              >
                <Copy :size="14" aria-hidden="true" />
              </button>
            </div>
          </article>

          <article class="panel panel--base panel--compact panel-stack panel-stack--tight machine-pat-setup-item">
            <header class="machine-pat-setup-item-header">
              <h4>REST API auth examples</h4>
              <div class="machine-pat-tab-list" role="tablist" aria-label="REST API auth examples">
                <button
                  type="button"
                  class="btn btn--tab"
                  :class="{ 'is-active': restAuthTab === 'curl' }"
                  role="tab"
                  :aria-selected="restAuthTab === 'curl'"
                  @click="restAuthTab = 'curl'"
                >
                  Curl
                </button>
                <button
                  type="button"
                  class="btn btn--tab"
                  :class="{ 'is-active': restAuthTab === 'powershell' }"
                  role="tab"
                  :aria-selected="restAuthTab === 'powershell'"
                  @click="restAuthTab = 'powershell'"
                >
                  PowerShell
                </button>
              </div>
            </header>
            <div class="machine-pat-code-block">
              <pre class="machine-pat-setup-code">{{ selectedRestAuthSnippet }}</pre>
              <button
                type="button"
                class="btn btn--secondary machine-pat-copy-icon"
                :disabled="isBusy"
                :aria-label="`Copy ${selectedRestAuthSnippetLabel} REST API example`"
                :title="`Copy ${selectedRestAuthSnippetLabel} REST API example`"
                @click="copySnippet(selectedRestAuthSnippet, `${selectedRestAuthSnippetLabel} REST API auth example`)"
              >
                <Copy :size="14" aria-hidden="true" />
              </button>
            </div>
          </article>

          <p class="machine-pat-setup-note">
            Use PAT as the direct bearer token for MCP and REST calls. PATs do not use refresh-token login.
          </p>
        </section>
      </aside>
    </div>

    <AccessTokenCreateDialog
      :open="isCreateDialogOpen"
      :busy="isBusy"
      :boards="boards"
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
import { createAuthApi } from '../api/authApi';
import { createBoardApi } from '../api/boardApi';
import AccessTokenCreateDialog from '../components/AccessTokenCreateDialog.vue';
import AccessTokenSecretModal from '../components/AccessTokenSecretModal.vue';
import type { AccessToken, CreateAccessTokenRequest } from '../types/authTypes';
import type { BoardSummary } from '../types/boardTypes';

const authApi = createAuthApi();
const boardApi = createBoardApi();

const boards = ref<BoardSummary[]>([]);
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
const restAuthTab = ref<'curl' | 'powershell'>('curl');

const isBusy = computed(() => loading.value || createBusy.value || revokeBusyTokenId.value !== null);
const isSecretModalOpen = computed(() => plainTextPat.value !== null);
const mcpEndpoint = computed(() => `${window.location.origin}/mcp`);
const apiBaseUrl = computed(() => window.location.origin);
const genericConfigSnippetJson = computed(() =>
  `{
  "mcpServers": {
    "boardoil": {
      "transport": "http",
      "url": "${mcpEndpoint.value}",
      "headers": {
        "Authorization": "Bearer <YOUR_PAT>"
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
  -H "Authorization: Bearer <YOUR_PAT>" \\
  -H "Content-Type: application/json" \\
  --data '{"jsonrpc":"2.0","id":"tools-list","method":"tools/list"}'`
);
const manualTestPowerShellSnippet = computed(() =>
  `$endpoint = "${mcpEndpoint.value}"
$headers = @{
  Authorization = "Bearer <YOUR_PAT>"
  "Content-Type" = "application/json"
}
$body = '{"jsonrpc":"2.0","id":"tools-list","method":"tools/list"}'
Invoke-RestMethod -Method Post -Uri $endpoint -Headers $headers -Body $body`
);
const selectedManualTestSnippet = computed(() => (manualTestTab.value === 'curl' ? manualTestCurlSnippet.value : manualTestPowerShellSnippet.value));
const selectedManualTestSnippetLabel = computed(() => (manualTestTab.value === 'curl' ? 'Curl' : 'PowerShell'));
const restAuthCurlSnippet = computed(() =>
  `curl -sS "${apiBaseUrl.value}/api/boards" \\
  -H "Authorization: Bearer <YOUR_PAT>" \\
  -H "Accept: application/json"`
);
const restAuthPowerShellSnippet = computed(() =>
  `$endpoint = "${apiBaseUrl.value}/api/boards"
$headers = @{
  Authorization = "Bearer <YOUR_PAT>"
  Accept = "application/json"
}
Invoke-RestMethod -Method Get -Uri $endpoint -Headers $headers`
);
const selectedRestAuthSnippet = computed(() => (restAuthTab.value === 'curl' ? restAuthCurlSnippet.value : restAuthPowerShellSnippet.value));
const selectedRestAuthSnippetLabel = computed(() => (restAuthTab.value === 'curl' ? 'Curl' : 'PowerShell'));

onMounted(async () => {
  await loadInitialData();
});

async function loadInitialData() {
  loading.value = true;
  errorMessage.value = null;
  try {
    const [boardsResult, tokensResult] = await Promise.all([boardApi.getBoards(), authApi.getAccessTokens()]);
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

  if (!window.confirm(`Revoke access token "${token.name}"? This cannot be undone.`)) {
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

function sortTokens(items: AccessToken[]) {
  return [...items].sort((left, right) => {
    const leftTimestamp = Date.parse(left.createdAtUtc);
    const rightTimestamp = Date.parse(right.createdAtUtc);
    return rightTimestamp - leftTimestamp;
  });
}
</script>

<style scoped>
.machine-access-view {
  margin-top: 1rem;
  display: grid;
  gap: 0.9rem;
}

.machine-access-layout {
  display: grid;
  grid-template-columns: minmax(340px, 1.25fr) minmax(320px, 1fr);
  gap: 0.9rem;
  align-items: start;
}

.machine-access-column {
  min-width: 0;
  display: grid;
  gap: 0.9rem;
}

.machine-access-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 0.75rem;
}

.machine-access-header h2 {
  margin: 0;
}

.machine-access-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.machine-access-actions {
  display: flex;
  align-items: center;
  justify-content: flex-start;
}

.machine-pat-code-block {
  position: relative;
  min-width: 0;
  max-width: 100%;
}

.machine-pat-code-block--endpoint {
  display: inline-block;
  width: fit-content;
  min-width: 0;
  max-width: 100%;
}

.machine-pat-code-block--endpoint > .machine-pat-setup-code {
  display: block;
  max-width: 100%;
}

.machine-pat-code-block--endpoint .machine-pat-copy-icon {
  top: 50%;
  transform: translateY(-50%);
}

.machine-pat-code-block > .machine-pat-setup-code {
  max-width: 100%;
  padding-right: 2.4rem;
}

.machine-pat-copy-icon {
  position: absolute;
  top: 0.42rem;
  right: 0.42rem;
  width: 1.75rem;
  min-width: 1.75rem;
  height: 1.75rem;
  padding: 0;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-color: color-mix(in oklab, var(--bo-colour-energy) 45%, var(--bo-border-soft));
  background: color-mix(in oklab, var(--bo-colour-energy) 16%, var(--bo-surface-base));
  color: color-mix(in oklab, var(--bo-colour-energy) 84%, var(--bo-ink-strong));
  opacity: 0;
  pointer-events: none;
  transition: opacity 120ms ease-in-out, background 120ms ease-in-out, border-color 120ms ease-in-out, color 120ms ease-in-out;
}

.machine-pat-code-block:hover .machine-pat-copy-icon,
.machine-pat-code-block:focus-within .machine-pat-copy-icon {
  opacity: 1;
  pointer-events: auto;
}

.machine-pat-copy-icon:hover:not(:disabled),
.machine-pat-copy-icon:focus-visible {
  border-color: var(--bo-colour-energy-strong);
  background: color-mix(in oklab, var(--bo-colour-energy) 26%, var(--bo-surface-base));
  color: color-mix(in oklab, var(--bo-colour-energy-strong) 82%, var(--bo-colour-brand));
}

.machine-pat-setup h3 {
  margin: 0;
}

.machine-pat-setup-steps {
  margin: 0;
  padding-left: 1.15rem;
  color: var(--bo-ink-default);
  display: grid;
  gap: 0.3rem;
}

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

.machine-pat-tab-list {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.2rem;
  border: 1px solid color-mix(in oklab, var(--bo-colour-energy) 45%, var(--bo-border-soft));
  border-radius: 999px;
  background: color-mix(in oklab, var(--bo-colour-energy) 14%, var(--bo-surface-base));
}

.machine-pat-setup-code {
  margin: 0;
  padding: 0.55rem;
  border-radius: 8px;
  border: 1px solid var(--bo-border-soft);
  background: var(--bo-surface-muted);
  color: var(--bo-ink-strong);
  overflow-x: auto;
  font-family: "Cascadia Mono", "Consolas", "Liberation Mono", monospace;
  font-size: 0.82rem;
  line-height: 1.35;
}

.machine-pat-inline-code {
  white-space: nowrap;
  text-overflow: ellipsis;
  overflow: hidden;
}

.machine-pat-setup-note {
  margin: 0;
  color: var(--bo-ink-muted);
}

.machine-pat-scope-hint {
  margin: 0;
  color: var(--bo-ink-muted);
  line-height: 1.35;
}

.machine-pat-list {
  display: grid;
  gap: 0.55rem;
}

.machine-pat-list-header {
  display: block;
}

.machine-pat-list-header h3 {
  margin: 0;
}

.machine-pat-empty {
  margin: 0;
  color: var(--bo-ink-muted);
}

.machine-pat-item-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.7rem;
}

.machine-pat-item-meta {
  display: grid;
  gap: 0.3rem;
  color: var(--bo-ink-default);
}

.machine-pat-item-actions {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 1040px) {
  .machine-access-layout {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 720px) {
  .machine-pat-copy-icon {
    opacity: 1;
    pointer-events: auto;
  }

  .machine-pat-item-header {
    flex-direction: column;
    align-items: flex-start;
  }

  .machine-pat-item-actions {
    justify-content: flex-start;
  }
}
</style>
