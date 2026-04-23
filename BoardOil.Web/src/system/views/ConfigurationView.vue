<template>
  <section class="configuration-view">
    <header class="configuration-header">
      <h2>Configuration</h2>
      <p>Runtime settings visible to administrators.</p>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section v-else class="panel panel-stack panel-stack--cozy">
      <div class="configuration-row">
        <span class="configuration-label">Allow insecure cookies</span>
        <span class="configuration-value">
          <span class="badge">{{ configuration?.allowInsecureCookies ? 'Enabled' : 'Disabled' }}</span>
        </span>
      </div>
      <p class="configuration-hint">
        {{ configuration?.allowInsecureCookies
          ? 'HTTP sessions are allowed. Not recommended.'
          : 'Secure cookies are enforced. HTTPS required (except localhost behavior).' }}
      </p>

      <div class="configuration-row configuration-row--start">
        <span class="configuration-label">MCP public base URL override</span>
        <span class="configuration-value">
          <span class="badge">{{ configuration?.mcpPublicBaseUrl ? 'Override set' : 'Auto (relative)' }}</span>
        </span>
      </div>
      <p class="configuration-hint">
        Leave blank to keep MCP discovery URLs relative (recommended default for Docker and proxy setups).
      </p>
      <label class="configuration-input-group">
        <span class="configuration-input-label">Public base URL</span>
        <input
          v-model="mcpPublicBaseUrlDraft"
          :disabled="saving"
          class="configuration-input"
          placeholder="https://boardoil.example.com"
          autocomplete="off"
          spellcheck="false"
        />
      </label>
      <div class="configuration-actions">
        <button type="button" class="btn" :disabled="saving" @click="saveConfiguration">
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
        <button type="button" class="btn btn--secondary" :disabled="saving" @click="resetToAuto">
          Use auto (relative)
        </button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import type { ConfigurationDto } from '../../shared/types/configurationTypes';

const systemApi = createSystemApi();
const configuration = ref<ConfigurationDto | null>(null);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const saving = ref(false);
const mcpPublicBaseUrlDraft = ref('');

onMounted(async () => {
  const result = await systemApi.getConfiguration();
  if (!result.ok) {
    errorMessage.value = result.error.message;
    return;
  }

  configuration.value = result.data;
  mcpPublicBaseUrlDraft.value = result.data.mcpPublicBaseUrl ?? '';
});

async function saveConfiguration() {
  saving.value = true;
  errorMessage.value = null;
  successMessage.value = null;
  try {
    const requestValue = mcpPublicBaseUrlDraft.value.trim();
    const result = await systemApi.updateConfiguration({
      mcpPublicBaseUrl: requestValue.length > 0 ? requestValue : null
    });
    if (!result.ok) {
      errorMessage.value = result.error.message;
      return;
    }

    configuration.value = result.data;
    mcpPublicBaseUrlDraft.value = result.data.mcpPublicBaseUrl ?? '';
    successMessage.value = result.data.mcpPublicBaseUrl
      ? 'Saved MCP public base URL override.'
      : 'Cleared override. MCP metadata now uses relative URLs.';
  } finally {
    saving.value = false;
  }
}

async function resetToAuto() {
  mcpPublicBaseUrlDraft.value = '';
  await saveConfiguration();
}
</script>

<style scoped>
.configuration-view {
  margin-top: 1rem;
  display: grid;
  gap: 0.9rem;
  max-width: 760px;
}

.configuration-header h2 {
  margin: 0;
}

.configuration-header p {
  margin: 0.2rem 0 0;
  color: var(--bo-ink-muted);
}

.configuration-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.configuration-row--start {
  align-items: flex-start;
}

.configuration-label {
  font-weight: 600;
  color: var(--bo-ink-strong);
}

.configuration-input-group {
  display: grid;
  gap: 0.35rem;
}

.configuration-input-label {
  font-weight: 600;
  color: var(--bo-ink-default);
}

.configuration-input {
  width: 100%;
}

.configuration-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.configuration-hint {
  margin: 0;
  color: var(--bo-ink-muted);
}
</style>
