import { ref } from 'vue';
import { defineStore } from 'pinia';
import { createScheduledJobsApi, type ScheduledJobsApi } from '../../shared/api/scheduledJobsApi';
import type { ScheduledJob } from '../../shared/types/scheduledJobTypes';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';

export function createScheduledJobsStore(api: Pick<ScheduledJobsApi, 'getSchedules' | 'runNow'> = createScheduledJobsApi()) {
  return defineStore('scheduledJobs', () => {
    const feedback = useUiFeedbackStore();
    const schedules = ref<ScheduledJob[]>([]);
    const loading = ref(false);
    const error = ref<string | null>(null);
    const running = ref<Record<string, boolean>>({});

    function SET_SCHEDULES(nextSchedules: ScheduledJob[]) {
      schedules.value = nextSchedules;
    }

    function SET_RUNNING(name: string, isRunning: boolean) {
      running.value = { ...running.value, [name]: isRunning };
    }

    function CLEAR_STATE() {
      schedules.value = [];
      loading.value = false;
      error.value = null;
      running.value = {};
    }

    async function loadSchedules() {
      loading.value = true;
      error.value = null;
      try {
        const result = await api.getSchedules();
        if (!result.ok) { error.value = result.error.message; return; }
        SET_SCHEDULES(result.data);
      } finally {
        loading.value = false;
      }
    }

    async function runNow(name: string) {
      if (running.value[name]) return;
      SET_RUNNING(name, true);
      try {
        const result = await api.runNow(name);
        if (!result.ok) {
          feedback.showToast(result.error.message, 'error');
          return;
        }
        const count = result.data.enqueuedCount;
        let message = `${count} jobs queued.`;
        if (count === 0) message = 'No jobs to queue.';
        else if (count === 1) message = 'Job queued.';
        feedback.showToast(message, 'success');
        await loadSchedules();
      } finally {
        SET_RUNNING(name, false);
      }
    }

    function dispose() {
      CLEAR_STATE();
    }

    return { schedules, loading, error, running, loadSchedules, runNow, dispose };
  });
}

export const useScheduledJobsStore = createScheduledJobsStore();
