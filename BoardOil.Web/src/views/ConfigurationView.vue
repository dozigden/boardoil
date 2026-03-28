<template>
  <section class="configuration-view">
    <header class="configuration-header">
      <h2>Configuration</h2>
      <p>Runtime settings visible to administrators.</p>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>
    <p v-if="successMessage" class="success">{{ successMessage }}</p>

    <section v-else class="configuration-card">
      <div class="configuration-row">
        <span class="configuration-label">Allow insecure cookies</span>
        <span class="configuration-value">
          <span class="card-id">{{ configuration?.allowInsecureCookies ? 'Enabled' : 'Disabled' }}</span>
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
          <span class="card-id">{{ configuration?.mcpPublicBaseUrl ? 'Override set' : 'Auto (relative)' }}</span>
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
        <button type="button" :disabled="saving" @click="saveConfiguration">
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
        <button type="button" class="ghost" :disabled="saving" @click="resetToAuto">
          Use auto (relative)
        </button>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { createConfigurationApi } from '../api/configurationApi';
import type { ConfigurationDto } from '../types/configurationTypes';

const configurationApi = createConfigurationApi();
const configuration = ref<ConfigurationDto | null>(null);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);
const saving = ref(false);
const mcpPublicBaseUrlDraft = ref('');

onMounted(async () => {
  const result = await configurationApi.getConfiguration();
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
    const result = await configurationApi.updateConfiguration({
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
