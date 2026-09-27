<template>
  <AdminSplitLayout title="System" :items="navItems">
    <RouterView />
  </AdminSplitLayout>
  <RouterView name="dialog" />
</template>

<script setup lang="ts">
import { computed, onUnmounted, watch } from 'vue';
import { RouterView, useRoute } from 'vue-router';
import AdminSplitLayout from '../components/AdminSplitLayout.vue';
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
  (route.path.startsWith('/admin/system/jobs') || route.path.startsWith('/admin/system/scheduled-jobs')));
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

const navItems = [
  {
    label: 'Boards',
    to: { name: 'system-admin-boards' },
    activeRouteNames: ['system-admin-boards', 'system-admin-board-members']
  },
  {
    label: 'Users',
    to: { name: 'users' }
  },
  {
    label: 'Client Accounts',
    to: { name: 'client-accounts' }
  },
  {
    label: 'OAuth Connections',
    to: { name: 'system-admin-oauth-connections' }
  },
  {
    label: 'Configuration',
    to: { name: 'configuration' }
  },
  {
    label: 'System Message',
    to: { name: 'system-info-message' }
  },
  {
    label: 'Jobs',
    to: { name: 'system-jobs' },
    activeRouteNames: ['system-jobs', 'system-job-details']
  },
  {
    label: 'Logs',
    to: { name: 'system-error-logs' },
    activeRouteNames: ['system-error-logs', 'system-error-log-details', 'system-oauth-logs']
  }
];
</script>
