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
          :disabled="saving || !hasUnsavedChanges"
          @click="saveConfiguration"
        >
          {{ saving ? 'Saving...' : 'Save' }}
        </button>
      </div>
    </header>

    <LoadingIndicator v-if="loading" label="Loading configuration..." />
    <p v-else-if="errorMessage" class="error">{{ errorMessage }}</p>

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
        <section class="configuration-setting">
          <label :for="timeZoneInputId" class="configuration-input-label">System timezone</label>
          <SearchableSelect :id="timeZoneInputId" v-model="timeZoneDraft" label="System timezone"
            :options="timeZoneOptions" :disabled="saving || configuration === null" />
          <p class="configuration-hint">Used for scheduled job run times. Changing it resets schedule catch-up from now.</p>
          <p v-if="timeZoneError" class="error" role="alert">{{ timeZoneError }}</p>
        </section>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, useId } from 'vue';
import { onBeforeRouteLeave } from 'vue-router';
import SearchableSelect from '../../shared/components/SearchableSelect.vue';
import LoadingIndicator from '../../shared/components/LoadingIndicator.vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useConfirm } from '../../shared/composables/useConfirm';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { ConfigurationDto } from '../../shared/types/configurationTypes';

const systemApi = createSystemApi();
const timeZoneInputId = `configuration-timezone-${useId()}`;
const timeZoneDraft = ref('');
const timeZoneOptions = ref<{ value: string; label: string }[]>([]);
const timeZoneError = ref<string | null>(null);
let disposed = false;
onBeforeUnmount(() => { disposed = true; });
const feedback = useUiFeedbackStore();
const { confirm } = useConfirm();
const configuration = ref<ConfigurationDto | null>(null);
const loading = ref(true);
const errorMessage = ref<string | null>(null);
const saving = ref(false);
const mcpPublicBaseUrlDraft = ref('');
const oauthLifecycleDiagnosticsEnabledDraft = ref(false);
const hasUnsavedChanges = computed(() => {
  if (!configuration.value) return false;

  return timeZoneDraft.value !== configuration.value.systemTimeZoneId
    || mcpPublicBaseUrlDraft.value.trim() !== (configuration.value.mcpPublicBaseUrl ?? '')
    || oauthLifecycleDiagnosticsEnabledDraft.value !== configuration.value.oauthLifecycleDiagnosticsEnabled;
});

function onBeforeUnload(event: BeforeUnloadEvent) {
  if (!hasUnsavedChanges.value) return;
  event.preventDefault();
  event.returnValue = '';
}

onBeforeRouteLeave(async () => {
  if (saving.value) return false;
  if (!hasUnsavedChanges.value) return true;

  return await confirm({
    title: 'Discard unsaved changes',
    message: 'You have unsaved changes in configuration. Discard them and leave?',
    confirmLabel: 'Discard',
    danger: true
  });
});

onMounted(async () => {
  window.addEventListener('beforeunload', onBeforeUnload);
  const [configurationResult, optionsResult] = await Promise.all([systemApi.getConfiguration(), systemApi.getTimeZoneOptions()]);
  if (disposed) return;
  loading.value = false;

  if (!configurationResult.ok) {
    errorMessage.value = configurationResult.error.message;
    return;
  }

  if (!optionsResult.ok) {
    errorMessage.value = optionsResult.error.message;
    return;
  }
  timeZoneOptions.value = optionsResult.data.options.map(option => ({ value: option.id, label: option.displayName }));
  applyConfigurationDraft(configurationResult.data);
});

onBeforeUnmount(() => window.removeEventListener('beforeunload', onBeforeUnload));

async function saveConfiguration() {
  if (saving.value || !hasUnsavedChanges.value) return;
  saving.value = true;
  timeZoneError.value = null;
  try {
    const requestValue = mcpPublicBaseUrlDraft.value.trim();
    const configurationResult = await systemApi.updateConfiguration({
      mcpPublicBaseUrl: requestValue.length > 0 ? requestValue : null,
      oauthLifecycleDiagnosticsEnabled: oauthLifecycleDiagnosticsEnabledDraft.value,
      systemTimeZoneId: timeZoneDraft.value
    });
    if (disposed) return;
    if (!configurationResult.ok) {
      timeZoneError.value = configurationResult.error.validationErrors?.systemTimeZoneId?.join(' ') ?? null;
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
  timeZoneDraft.value = nextConfiguration.systemTimeZoneId;
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
