<template>
  <section class="machine-access-view">
    <header class="machine-access-header">
      <div>
        <h2>Machine Access Tokens</h2>
        <p>Create and manage Personal Access Tokens (PATs) for MCP clients.</p>
      </div>
    </header>

    <div class="machine-access-layout">
      <section class="machine-access-column machine-access-column--tokens">
        <div class="machine-access-actions">
          <button type="button" class="btn" :disabled="isBusy" @click="openCreateDialog">Create token</button>
        </div>
        <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
        <p v-if="successMessage" class="success">{{ successMessage }}</p>

        <section v-if="plainTextPat" class="panel panel-stack machine-pat-secret machine-pat-secret--danger">
          <h3>Copy token now</h3>
          <p>This value is only shown once for <strong>{{ plainTextPatName }}</strong>.</p>
          <div class="machine-pat-code-block">
            <code class="machine-pat-secret-value">{{ plainTextPat }}</code>
            <button
              type="button"
              class="btn btn--secondary machine-pat-copy-icon"
              :disabled="isBusy"
              aria-label="Copy token"
              title="Copy token"
              @click="copyPlainTextPat"
            >
              <Copy :size="14" aria-hidden="true" />
            </button>
          </div>
          <div class="machine-pat-secret-actions">
            <button type="button" class="btn btn--secondary" :disabled="isBusy" @click="dismissPlainTextPat">Hide token</button>
          </div>
        </section>

        <section class="machine-pat-list">
          <header class="machine-pat-list-header">
            <h3>Existing tokens</h3>
          </header>

          <p v-if="tokens.length === 0" class="machine-pat-empty">No machine tokens have been created yet.</p>

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

          <p class="machine-pat-setup-note">
            Use PAT as the direct bearer token for MCP calls. PATs do not use refresh-token login.
          </p>
        </section>
      </aside>
    </div>

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
import { Copy } from 'lucide-vue-next';
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
const configSnippetTab = ref<'json' | 'toml'>('json');
const manualTestTab = ref<'curl' | 'powershell'>('curl');

const isBusy = computed(() => loading.value || createBusy.value || revokeBusyTokenId.value !== null);
const mcpEndpoint = computed(() => `${window.location.origin}/mcp`);
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
bearer_token_env_var = "<YOUR_PAT>"`
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
