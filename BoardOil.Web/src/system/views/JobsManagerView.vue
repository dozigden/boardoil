<template>
  <section class="jobs-manager">
    <SystemJobsTabs />
    <RouterView />
  </section>
  <RouterView name="dialog" />
</template>

<script setup lang="ts">
import { computed, onUnmounted, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import SystemJobsTabs from '../components/SystemJobsTabs.vue';
import { useAuthStore } from '../../shared/stores/authStore';
import { createSystemJobsRealtime } from '../realtime/systemJobsRealtime';
import { createSystemJobsContext } from '../realtime/systemJobsContext';
import { useSystemJobsStore } from '../stores/systemJobsStore';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

const route = useRoute();
const auth = useAuthStore();
const jobs = useSystemJobsStore();
const feedback = useUiFeedbackStore();
const realtime = createSystemJobsRealtime({
  changed: jobs.invalidated,
  recovered: jobs.recovered,
  warning: message => {
    if (message) feedback.setWarning(message);
    else if (feedback.warningMessage.startsWith('Job updates') ||
      feedback.warningMessage.startsWith('Live job updates')) feedback.clearWarning();
  }
});
const jobsContextActive = computed(() => auth.isAdmin &&
  (route.name === 'system-jobs' || route.name === 'system-job-details'));
const context = createSystemJobsContext(realtime, () => {
  jobs.dispose();
  if (feedback.warningMessage.startsWith('Job updates') ||
    feedback.warningMessage.startsWith('Live job updates')) feedback.clearWarning();
}, error => {
  console.warn('System jobs realtime connection failed.', error);
  feedback.setWarning('Live job updates are unavailable. Use Refresh if needed.');
});
watch(jobsContextActive, active => {
  void context.setActive(active);
}, { immediate: true });
onUnmounted(() => { void context.setActive(false); });
</script>

<style scoped>
.jobs-manager { display: flex; flex-direction: column; gap: 0.75rem; min-width: 0; min-height: 100%; }
</style>
