<template>
  <nav class="system-logs-tabs" role="tablist" aria-label="System logs">
    <RouterLink
      :to="{ name: 'system-error-logs' }"
      class="system-logs-tab"
      :class="{ 'is-active': errorLogsActive }"
      role="tab"
      :aria-selected="errorLogsActive"
    >
      Error Logs
    </RouterLink>
    <RouterLink
      :to="{ name: 'system-oauth-logs' }"
      class="system-logs-tab"
      :class="{ 'is-active': oauthLogsActive }"
      role="tab"
      :aria-selected="oauthLogsActive"
    >
      OAuth Logs
    </RouterLink>
  </nav>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { RouterLink, useRoute } from 'vue-router';

const route = useRoute();
const errorLogsActive = computed(() =>
  route.name === 'system-error-logs' || route.name === 'system-error-log-details');
const oauthLogsActive = computed(() => route.name === 'system-oauth-logs');
</script>

<style scoped>
.system-logs-tabs {
  display: flex;
  align-items: flex-end;
  gap: 0.2rem;
  overflow-x: auto;
  overflow-y: hidden;
  border-bottom: 1px solid var(--bo-border-default);
}

.system-logs-tab {
  flex: 0 0 auto;
  margin-bottom: -1px;
  padding: 0.65rem 0.7rem 0.55rem;
  border-bottom: 3px solid transparent;
  color: var(--bo-ink-muted);
  font-weight: 650;
  text-decoration: none;
}

.system-logs-tab:hover,
.system-logs-tab:focus-visible {
  color: var(--bo-link);
}

.system-logs-tab:focus-visible {
  outline: 2px solid var(--bo-focus-ring);
  outline-offset: -2px;
}

.system-logs-tab.is-active {
  border-bottom-color: var(--bo-colour-energy);
  color: var(--bo-ink-strong);
}
</style>
