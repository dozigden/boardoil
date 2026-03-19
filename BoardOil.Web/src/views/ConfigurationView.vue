<template>
  <section class="configuration-view">
    <header class="configuration-header">
      <h2>Configuration</h2>
      <p>Runtime settings visible to administrators.</p>
    </header>

    <p v-if="errorMessage" class="error">{{ errorMessage }}</p>

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

onMounted(async () => {
  const result = await configurationApi.getConfiguration();
  if (!result.ok) {
    errorMessage.value = result.error.message;
    return;
  }

  configuration.value = result.data;
});
</script>
