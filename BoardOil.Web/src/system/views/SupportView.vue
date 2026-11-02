<template>
  <section class="support-view">
    <header class="support-header">
      <h2>Support</h2>
      <button type="button" class="btn btn--secondary" :disabled="loading" @click="load">
        Refresh
      </button>
    </header>

    <LoadingIndicator v-if="loading" label="Loading support information..." />
    <p v-else-if="errorMessage" class="error" role="alert">{{ errorMessage }}</p>
    <section v-else-if="info" class="panel panel-stack panel-stack--cozy" aria-labelledby="support-database-title">
      <h3 id="support-database-title">Database</h3>
      <dl class="support-facts">
        <dt>Provider</dt><dd>{{ info.database.provider }}</dd>
        <dt>Version</dt><dd>{{ info.database.version }}</dd>
        <dt>Journal mode</dt><dd>{{ info.database.journalMode.toUpperCase() }}</dd>
        <dt>Allocated size</dt><dd>{{ info.database.sizeBytes.toLocaleString() }} bytes</dd>
      </dl>
    </section>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { getSupportInfo } from '../../shared/api/supportApi';
import type { SupportInfo } from '../../shared/types/supportTypes';
import LoadingIndicator from '../../shared/components/LoadingIndicator.vue';

const info = ref<SupportInfo | null>(null);
const loading = ref(true);
const errorMessage = ref('');

async function load() {
  loading.value = true;
  errorMessage.value = '';
  try {
    const result = await getSupportInfo();
    if (result.ok) {
      info.value = result.data;
    } else {
      errorMessage.value = result.error.message;
    }
  } finally {
    loading.value = false;
  }
}

onMounted(load);
</script>

<style scoped>
.support-view { display: flex; flex-direction: column; gap: 1rem; }
.support-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
.support-facts { display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 0.75rem 2rem; margin: 0; }
.support-facts dt { color: var(--bo-ink-muted); }
.support-facts dd { margin: 0; overflow-wrap: anywhere; }
</style>
