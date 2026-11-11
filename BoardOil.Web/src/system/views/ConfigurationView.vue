<template>
  <section class="configuration-view">
    <template v-if="!form">
      <h2>Configuration</h2>
      <LoadingIndicator v-if="loading" label="Loading configuration..." />
      <template v-else-if="loadError">
        <p class="error" role="alert">{{ loadError }}</p>
        <button type="button" class="btn" @click="loadConfiguration">Retry</button>
      </template>
    </template>
    <FormPage v-else :definition="form" discard-message="You have unsaved changes in configuration. Discard them and leave?" @saved="configurationSaved">
      <template #before-fields>
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
      </template>
      <template #fields-header>
        <header class="configuration-section-header"><h3>Editable settings</h3></header>
      </template>
    </FormPage>
  </section>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, shallowRef } from 'vue';
import LoadingIndicator from '../../shared/components/LoadingIndicator.vue';
import FormPage from '../../shared/forms/FormPage.vue';
import { createSystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { ConfigurationDto } from '../../shared/types/configurationTypes';
import { configurationForm } from '../forms/configurationForm';

const systemApi = createSystemApi();
const feedback = useUiFeedbackStore();
const configuration = ref<ConfigurationDto | null>(null);
const loading = ref(true);
const loadError = ref<string | null>(null);
const form = shallowRef<ReturnType<typeof configurationForm> | null>(null);
let disposed = false;
onBeforeUnmount(() => { disposed = true; });
onMounted(loadConfiguration);

async function loadConfiguration() {
  loading.value = true;
  loadError.value = null;
  const [configurationResult, optionsResult] = await Promise.all([systemApi.getConfiguration(), systemApi.getTimeZoneOptions()]);
  if (disposed) return;
  loading.value = false;
  if (!configurationResult.ok) {
    loadError.value = `Could not load configuration: ${configurationResult.error.message}`;
    return;
  }
  if (!optionsResult.ok) {
    loadError.value = `Could not load timezone choices: ${optionsResult.error.message}`;
    return;
  }
  const timeZoneOptions = optionsResult.data.options.map(option => ({ value: option.id, label: option.displayName }));
  configuration.value = configurationResult.data;
  form.value = configurationForm(configurationResult.data, timeZoneOptions, systemApi.updateConfiguration);
}

function configurationSaved(value: ConfigurationDto) {
  configuration.value = value;
  feedback.showToast('Saved successfully.');
}
</script>

<style scoped>
.configuration-view { margin-top: 1rem; max-width: 760px; }
.configuration-section-header { display: grid; gap: 0.2rem; }
.configuration-section-header h3,
.configuration-section-header p { margin: 0; }
.configuration-section-header h3 { color: var(--bo-ink-strong); font-size: 1rem; }
.configuration-section-header p { color: var(--bo-ink-muted); }
.configuration-row { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
.configuration-label { font-weight: 600; color: var(--bo-ink-strong); }
.configuration-hint { margin: 0; color: var(--bo-ink-muted); }
.configuration-hint code { color: var(--bo-ink-default); }
</style>
