import { ref } from 'vue';
import { defineStore } from 'pinia';
import { createScheduledJobsApi, type ScheduledJobsApi } from '../../shared/api/scheduledJobsApi';
import type { RunScheduledJobResult, ScheduledJob } from '../../shared/types/scheduledJobTypes';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

export function createScheduledJobsStore(api: Pick<ScheduledJobsApi, 'getSchedules' | 'runNow'> = createScheduledJobsApi()) {
  return defineStore('scheduledJobs', () => {
    const feedback = useUiFeedbackStore();
    const schedules = ref<ScheduledJob[]>([]);
    const loading = ref(false);
    const error = ref<string | null>(null);
    const running = ref<Record<string, boolean>>({});
    const runResults = ref<Record<string, RunScheduledJobResult>>({});
    const runErrors = ref<Record<string, string>>({});
    let generation = 0;
    let loadVersion = 0;

    async function loadSchedules() {
      const version = ++loadVersion;
      const context = generation;
      loading.value = true;
      error.value = null;
      try {
        const result = await api.getSchedules();
        if (context !== generation || version !== loadVersion) return;
        if (!result.ok) { error.value = result.error.message; return; }
        schedules.value = result.data;
      } finally {
        if (context === generation && version === loadVersion) loading.value = false;
      }
    }

    async function runNow(name: string) {
      if (running.value[name]) return;
      const context = generation;
      running.value = { ...running.value, [name]: true };
      runErrors.value = { ...runErrors.value, [name]: '' };
      runResults.value = Object.fromEntries(Object.entries(runResults.value).filter(([key]) => key !== name));
      try {
        const result = await api.runNow(name);
        if (context !== generation) return;
        if (!result.ok) {
          runErrors.value = { ...runErrors.value, [name]: result.error.message };
          feedback.showToast(result.error.message, 'error');
          return;
        }
        runResults.value = { ...runResults.value, [name]: result.data };
        const count = result.data.enqueuedCount;
        let message = `${count} jobs queued.`;
        if (count === 0) message = 'No jobs to queue.';
        else if (count === 1) message = 'Job queued.';
        feedback.showToast(message, 'success');
        await loadSchedules();
      } finally {
        if (context === generation) running.value = { ...running.value, [name]: false };
      }
    }

    function dispose() {
      generation++;
      schedules.value = [];
      loading.value = false;
      error.value = null;
      running.value = {};
      runResults.value = {};
      runErrors.value = {};
    }

    return { schedules, loading, error, running, runResults, runErrors, loadSchedules, runNow, dispose };
  });
}

export const useScheduledJobsStore = createScheduledJobsStore();
