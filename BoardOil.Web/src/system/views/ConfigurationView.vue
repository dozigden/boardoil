<template>
  <section class="configuration-view">
    <header class="configuration-header">
      <div>
        <h2>Configuration</h2>
      </div>
      <div class="configuration-save">
        <button
          type="button"
          class="btn"
          :disabled="saving || configuration === null"
          @click="saveConfiguration"
        >
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
      </div>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

    <div v-else class="configuration-sections">
      <section class="panel panel-stack panel-stack--cozy">
        <header class="configuration-section-header">
          <h3>Runtime information</h3>
          <p>Information only. These settings are controlled by the server environment.</p>
        </header>

        <div class="configuration-row">
          <span class="configuration-label">Allow insecure cookies</span>
          <span class="badge">{{ configuration?.allowInsecureCookies ? 'Enabled' : 'Disabled' }}</span>
        </div>
        <p class="configuration-hint">
          {{ configuration?.allowInsecureCookies
            ? 'HTTP sessions are allowed. Not recommended.'
            : 'Secure cookies are enforced. HTTPS is required outside localhost.' }}
        </p>
        <p class="configuration-hint">
          Set at deployment with <code>BoardOilAuth:AllowInsecureCookies</code> or
          <code>BoardOilAuth__AllowInsecureCookies</code>.
        </p>
      </section>

      <section class="panel panel-stack panel-stack--cozy">
        <header class="configuration-section-header">
          <h3>Editable settings</h3>
        </header>

        <section class="configuration-setting">
          <label class="configuration-checkbox-row">
            <input
              v-model="oauthLifecycleDiagnosticsEnabledDraft"
              :disabled="saving"
              type="checkbox"
            />
            <span>Log OAuth requests</span>
          </label>
          <p class="configuration-hint">
            When enabled, BoardOil retains OAuth identities, requested scopes, hashed token fingerprints,
            trace identifiers, and user-agent metadata.
          </p>
        </section>

        <section class="configuration-setting">
          <label class="configuration-input-group">
            <span class="configuration-input-label">MCP public base URL</span>
            <span class="configuration-input-row">
              <input
                v-model="mcpPublicBaseUrlDraft"
                :disabled="saving"
                class="configuration-input"
                placeholder="https://boardoil.example.com"
                autocomplete="off"
                spellcheck="false"
              />
              <button
                type="button"
                class="btn btn--secondary"
                :disabled="saving || mcpPublicBaseUrlDraft.length === 0"
                @click="useAutomaticUrl"
              >
                Clear
              </button>
            </span>
          </label>
          <p class="configuration-hint">
            Leave blank to use automatic relative discovery URLs, recommended for Docker and proxy setups.
          </p>
        </section>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { ConfigurationDto } from '../../shared/types/configurationTypes';

const systemApi = createSystemApi();
const feedback = useUiFeedbackStore();
const configuration = ref<ConfigurationDto | null>(null);
const errorMessage = ref<string | null>(null);
const saving = ref(false);
const mcpPublicBaseUrlDraft = ref('');
const oauthLifecycleDiagnosticsEnabledDraft = ref(false);

onMounted(async () => {
  const configurationResult = await systemApi.getConfiguration();

  if (!configurationResult.ok) {
    errorMessage.value = configurationResult.error.message;
    return;
  }

  applyConfigurationDraft(configurationResult.data);
});

async function saveConfiguration() {
  saving.value = true;
  try {
    const requestValue = mcpPublicBaseUrlDraft.value.trim();
    const configurationResult = await systemApi.updateConfiguration({
      mcpPublicBaseUrl: requestValue.length > 0 ? requestValue : null,
      oauthLifecycleDiagnosticsEnabled: oauthLifecycleDiagnosticsEnabledDraft.value
    });
    if (!configurationResult.ok) {
      feedback.showToast(configurationResult.error.message, 'error');
      return;
    }

    applyConfigurationDraft(configurationResult.data);
    feedback.showToast('Saved successfully.');
  } finally {
    saving.value = false;
  }
}

function useAutomaticUrl() {
  mcpPublicBaseUrlDraft.value = '';
}

function applyConfigurationDraft(nextConfiguration: ConfigurationDto) {
  configuration.value = nextConfiguration;
  mcpPublicBaseUrlDraft.value = nextConfiguration.mcpPublicBaseUrl ?? '';
  oauthLifecycleDiagnosticsEnabledDraft.value = nextConfiguration.oauthLifecycleDiagnosticsEnabled;
}
</script>

<style scoped>
.configuration-view {
  margin-top: 1rem;
  display: grid;
  gap: 0.9rem;
  max-width: 760px;
}

.configuration-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.configuration-header h2 {
  margin: 0;
}

.configuration-sections {
  display: grid;
  gap: 0.9rem;
}

.configuration-section-header {
  display: grid;
  gap: 0.2rem;
}

.configuration-section-header h3,
.configuration-section-header p {
  margin: 0;
}

.configuration-section-header h3 {
  color: var(--bo-ink-strong);
  font-size: 1rem;
}

.configuration-section-header p {
  color: var(--bo-ink-muted);
}

.configuration-setting {
  display: grid;
  gap: 0.55rem;
}

.configuration-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
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

.configuration-input-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.configuration-input-row .configuration-input {
  flex: 1 1 auto;
  min-width: 0;
}

.configuration-checkbox-row {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.configuration-hint {
  margin: 0;
  color: var(--bo-ink-muted);
}

.configuration-hint code {
  color: var(--bo-ink-default);
}

@media (max-width: 620px) {
  .configuration-header {
    align-items: stretch;
    flex-direction: column;
  }

}
</style>
