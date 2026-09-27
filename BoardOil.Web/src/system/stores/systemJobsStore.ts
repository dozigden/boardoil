import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { createJobsApi, type JobsApi } from '../../shared/api/jobsApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { Job, JobDetails, JobLog } from '../../shared/types/jobTypes';

export const JOB_PAGE_SIZE_OPTIONS = [50, 100, 200] as const;
export type JobEntity = Job & { logs: JobLog[] | null };

export function createSystemJobsStore(api: JobsApi = createJobsApi()) {
  return defineStore('systemJobs', () => {
    const feedback = useUiFeedbackStore();
    const byId = ref<Record<number, JobEntity>>({});
    const ids = ref<number[]>([]);
    const offset = ref(0);
    const limit = ref(100);
    const totalCount = ref(0);
    const listLoading = ref(false);
    const listError = ref<string | null>(null);
    const detailLoading = ref<Record<number, boolean>>({});
    const detailError = ref<Record<number, string | null>>({});
    const visible = ref(false);
    const openDetailId = ref<number | null>(null);
    const jobs = computed(() => ids.value.map(id => byId.value[id]).filter(job => job !== undefined));
    let generation = 0;
    let listVersion = 0;
    let entityRevision = 0;
    let requestedOffset = 0;
    let requestedLimit = 100;
    const revisionById: Record<number, number> = {};
    const detailVersionById: Record<number, number> = {};

    function mergeEntity(
      job: Job, logs: JobLog[] | null, requestRevision: number,
      previous: JobEntity | undefined
    ): JobEntity {
      if ((revisionById[job.id] ?? 0) > requestRevision && previous &&
        compareJobUpdateTimes(previous.updatedAtUtc, job.updatedAtUtc) >= 0) {
        if (logs !== null && previous?.updatedAtUtc === job.updatedAtUtc) {
          revisionById[job.id] = ++entityRevision;
          return { ...previous, logs };
        }
        return previous;
      }
      // List summaries only retain logs if the job has not changed since details loaded.
      const retainedLogs = previous?.updatedAtUtc === job.updatedAtUtc ? previous.logs : null;
      revisionById[job.id] = ++entityRevision;
      return { ...job, logs: logs ?? retainedLogs ?? null };
    }

    function applyEntity(job: Job, logs: JobLog[] | null, requestRevision: number) {
      byId.value = {
        ...byId.value,
        [job.id]: mergeEntity(job, logs, requestRevision, byId.value[job.id])
      };
    }

    async function loadJobs(nextOffset = requestedOffset, nextLimit = requestedLimit) {
      requestedOffset = nextOffset;
      requestedLimit = nextLimit;
      const currentGeneration = generation;
      const requestVersion = ++listVersion;
      const requestRevision = entityRevision;
      listLoading.value = true;
      listError.value = null;
      try {
        const result = await api.getJobs(nextOffset, nextLimit);
        if (currentGeneration !== generation || requestVersion !== listVersion) return false;
        if (!result.ok) {
          listError.value = result.error.message;
          feedback.showToast(result.error.message, 'error');
          return false;
        }
        const nextById = { ...byId.value };
        for (const job of result.data.items) {
          nextById[job.id] = mergeEntity(job, null, requestRevision, nextById[job.id]);
        }
        byId.value = nextById;
        ids.value = result.data.items.map(job => job.id);
        offset.value = result.data.offset;
        limit.value = result.data.limit;
        totalCount.value = result.data.totalCount;
        return true;
      } finally {
        if (currentGeneration === generation && requestVersion === listVersion) listLoading.value = false;
      }
    }

    async function loadDetails(id: number, retryIfStale = true) {
      const currentGeneration = generation;
      const requestVersion = (detailVersionById[id] ?? 0) + 1;
      detailVersionById[id] = requestVersion;
      const requestRevision = entityRevision;
      detailLoading.value = { ...detailLoading.value, [id]: true };
      detailError.value = { ...detailError.value, [id]: null };
      try {
        const result = await api.getJobDetails(id);
        if (currentGeneration !== generation || requestVersion !== detailVersionById[id]) return null;
        if (!result.ok) {
          detailError.value = { ...detailError.value, [id]: result.error.message };
          if (result.error.statusCode === 404) {
            byId.value = Object.fromEntries(Object.entries(byId.value).filter(([key]) => Number(key) !== id));
          }
          feedback.showToast(result.error.message, 'error');
          return null;
        }
        // A newer list/detail response may have arrived while this request was in flight.
        const { logs, ...job } = result.data as JobDetails;
        applyEntity(job, [...logs].sort((a, b) => a.loggedAtUtc.localeCompare(b.loggedAtUtc) || a.id - b.id), requestRevision);
        const current = byId.value[id];
        if (retryIfStale && current?.logs === null &&
          compareJobUpdateTimes(current.updatedAtUtc, job.updatedAtUtc) > 0) {
          return await loadDetails(id, false);
        }
        return byId.value[id] ?? null;
      } finally {
        if (currentGeneration === generation && requestVersion === detailVersionById[id]) {
          detailLoading.value = { ...detailLoading.value, [id]: false };
        }
      }
    }

    async function invalidated(id: number | null) {
      if (id !== null) {
        const existing = byId.value[id];
        if (existing) byId.value = { ...byId.value, [id]: { ...existing, logs: null } };
      } else {
        byId.value = Object.fromEntries(Object.entries(byId.value).map(([key, value]) => [key, { ...value, logs: null }]));
      }
      if (!visible.value) return;
      const loads: Promise<unknown>[] = [loadJobs()];
      if (openDetailId.value !== null && (id === null || id === openDetailId.value)) loads.push(loadDetails(openDetailId.value));
      await Promise.all(loads);
    }

    async function recovered() {
      if (!visible.value) return;
      const loads: Promise<unknown>[] = [loadJobs()];
      if (openDetailId.value !== null) loads.push(loadDetails(openDetailId.value));
      await Promise.all(loads);
    }

    function dispose() {
      generation++;
      listVersion++;
      byId.value = {};
      ids.value = [];
      offset.value = 0;
      limit.value = 100;
      requestedOffset = 0;
      requestedLimit = 100;
      totalCount.value = 0;
      listLoading.value = false;
      listError.value = null;
      detailLoading.value = {};
      detailError.value = {};
      visible.value = false;
      openDetailId.value = null;
      for (const key of Object.keys(revisionById)) delete revisionById[Number(key)];
      for (const key of Object.keys(detailVersionById)) delete detailVersionById[Number(key)];
      entityRevision = 0;
    }

    function goPreviousPage() {
      if (offset.value <= 0) return Promise.resolve(false);
      return loadJobs(Math.max(0, offset.value - limit.value), limit.value);
    }
    function goNextPage() {
      if (offset.value + jobs.value.length >= totalCount.value) return Promise.resolve(false);
      return loadJobs(offset.value + limit.value, limit.value);
    }
    function setPageSize(size: number) { return loadJobs(0, size); }

    return { byId, ids, jobs, offset, limit, totalCount, listLoading, listError,
      detailLoading, detailError, visible, openDetailId, loadJobs, loadDetails,
      invalidated, recovered, dispose, goPreviousPage, goNextPage, setPageSize };
  });
}

// Both timestamps come from the same UTC DateTime API contract. Compare its seven
// fractional digits so two writes within one millisecond retain their order.
function compareJobUpdateTimes(left: string, right: string): number {
  const seconds = left.slice(0, 19).localeCompare(right.slice(0, 19));
  if (seconds !== 0) return seconds;
  const fraction = (value: string) =>
    Number((/^\.(\d{1,7})/.exec(value.slice(19))?.[1] ?? '').padEnd(7, '0'));
  return fraction(left) - fraction(right);
}

export const useSystemJobsStore = createSystemJobsStore();
