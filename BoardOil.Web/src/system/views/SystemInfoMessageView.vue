<template>
  <section class="system-info-message-view">
    <template v-if="!form">
      <h2>System Info Message</h2>
      <LoadingIndicator v-if="loading" label="Loading system information message..." />
      <template v-else-if="loadError">
        <p class="error" role="alert">{{ loadError }}</p>
        <button type="button" class="btn" @click="loadMessage">Retry</button>
      </template>
    </template>
    <FormPage v-else :definition="form" discard-message="You have unsaved changes to the system information message. Discard them and leave?" @saved="messageSaved">
      <template #before-fields>
        <p class="system-info-message-hint">Shown in the app header for authenticated users.</p>
      </template>
      <template #fields-header="{ values }">
        <div class="system-info-message-status">
          <span>Status</span>
          <span class="badge">{{ values.enabled ? 'Enabled' : 'Disabled' }}</span>
        </div>
      </template>
      <template #after-fields="{ values }">
        <SystemInfoMessagePreview :draft="values" />
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
import { useSystemInfoMessageStore } from '../../shared/stores/systemInfoMessageStore';
import SystemInfoMessagePreview from '../components/SystemInfoMessagePreview.vue';
import { systemInfoMessageForm } from '../forms/systemInfoMessageForm';

const systemApi = createSystemApi();
const feedback = useUiFeedbackStore();
const store = useSystemInfoMessageStore();
const form = shallowRef<ReturnType<typeof systemInfoMessageForm> | null>(null);
const loading = ref(true);
const loadError = ref<string | null>(null);
let disposed = false;
onBeforeUnmount(() => { disposed = true; });
onMounted(loadMessage);

async function loadMessage() {
  loading.value = true;
  loadError.value = null;
  const result = await systemApi.getSystemInfoMessage();
  if (disposed) return;
  loading.value = false;
  if (!result.ok) {
    loadError.value = result.error.message;
    return;
  }
  form.value = systemInfoMessageForm(result.data, store.save);
}

function messageSaved() {
  feedback.showToast('Saved successfully.');
}
</script>

<style scoped>
.system-info-message-view { margin-top: 1rem; max-width: 760px; }
.system-info-message-hint { margin: 0; color: var(--bo-ink-muted); }
.system-info-message-status { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
</style>
