<template>
  <nav class="system-jobs-tabs" role="tablist" aria-label="System jobs">
    <RouterLink :to="{ name: 'system-jobs' }" class="system-jobs-tab"
      :class="{ 'is-active': historyActive }" role="tab" :aria-selected="historyActive">
      Job History
    </RouterLink>
    <RouterLink :to="{ name: 'system-scheduled-jobs' }" class="system-jobs-tab"
      :class="{ 'is-active': schedulesActive }" role="tab" :aria-selected="schedulesActive">
      Scheduled Jobs
    </RouterLink>
  </nav>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { RouterLink, useRoute } from 'vue-router';

const route = useRoute();
const historyActive = computed(() => route.name === 'system-jobs' || route.name === 'system-job-details');
const schedulesActive = computed(() => route.name === 'system-scheduled-jobs');
</script>

<style scoped>
.system-jobs-tabs { display: flex; align-items: flex-end; gap: 0.2rem; overflow-x: auto; overflow-y: hidden; border-bottom: 1px solid var(--bo-border-default); }
.system-jobs-tab { flex: 0 0 auto; margin-bottom: -1px; padding: 0.65rem 0.7rem 0.55rem; border-bottom: 3px solid transparent; color: var(--bo-ink-muted); font-weight: 650; text-decoration: none; }
.system-jobs-tab:hover, .system-jobs-tab:focus-visible { color: var(--bo-link); }
.system-jobs-tab:focus-visible { outline: 2px solid var(--bo-focus-ring); outline-offset: -2px; }
.system-jobs-tab.is-active { border-bottom-color: var(--bo-colour-energy); color: var(--bo-ink-strong); }
</style>
