import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import { createJobsApi, type JobsApi } from '../../shared/api/jobsApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { Job, JobDetails, JobList, JobLog } from '../../shared/types/jobTypes';

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
    let requestedOffset = 0;
    let requestedLimit = 100;

    function SET_JOB_PAGE(page: JobList) {
      const nextById = { ...byId.value };
      for (const job of page.items) {
        nextById[job.id] = mergeJobSummary(nextById[job.id], job);
      }
      byId.value = nextById;
      ids.value = page.items.map(job => job.id);
      offset.value = page.offset;
      limit.value = page.limit;
      totalCount.value = page.totalCount;
    }

    function UPSERT_JOB_DETAILS(details: JobDetails) {
      byId.value = {
        ...byId.value,
        [details.id]: {
          ...details,
          logs: [...details.logs].sort((a, b) => a.loggedAtUtc.localeCompare(b.loggedAtUtc) || a.id - b.id)
        }
      };
    }

    function REMOVE_JOB(id: number) {
      byId.value = Object.fromEntries(Object.entries(byId.value).filter(([key]) => Number(key) !== id));
    }

    function CLEAR_JOB_LOGS(id: number | null) {
      if (id !== null) {
        const existing = byId.value[id];
        if (existing) byId.value = { ...byId.value, [id]: { ...existing, logs: null } };
      } else {
        byId.value = Object.fromEntries(Object.entries(byId.value).map(([key, value]) => [key, { ...value, logs: null }]));
      }
    }

    function SET_DETAIL_LOADING(id: number, loading: boolean) {
      detailLoading.value = { ...detailLoading.value, [id]: loading };
    }

    function SET_DETAIL_ERROR(id: number, message: string | null) {
      detailError.value = { ...detailError.value, [id]: message };
    }

    function SET_VISIBLE(isVisible: boolean) {
      visible.value = isVisible;
    }

    function SET_OPEN_DETAIL(id: number | null) {
      openDetailId.value = id;
    }

    function CLEAR_STATE() {
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
    }

    async function loadJobs(nextOffset = requestedOffset, nextLimit = requestedLimit) {
      requestedOffset = nextOffset;
      requestedLimit = nextLimit;
      listLoading.value = true;
      listError.value = null;
      try {
        const result = await api.getJobs(nextOffset, nextLimit);
        if (!result.ok) {
          listError.value = result.error.message;
          feedback.showToast(result.error.message, 'error');
          return false;
        }
        SET_JOB_PAGE(result.data);
        return true;
      } finally {
        listLoading.value = false;
      }
    }

    async function loadDetails(id: number) {
      SET_DETAIL_LOADING(id, true);
      SET_DETAIL_ERROR(id, null);
      try {
        const result = await api.getJobDetails(id);
        if (!result.ok) {
          SET_DETAIL_ERROR(id, result.error.message);
          if (result.error.statusCode === 404) REMOVE_JOB(id);
          feedback.showToast(result.error.message, 'error');
          return null;
        }
        UPSERT_JOB_DETAILS(result.data);
        return byId.value[id] ?? null;
      } finally {
        SET_DETAIL_LOADING(id, false);
      }
    }

    async function invalidated(id: number | null) {
      CLEAR_JOB_LOGS(id);
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

    function setVisible(isVisible: boolean) {
      SET_VISIBLE(isVisible);
    }

    function setOpenDetail(id: number | null) {
      SET_OPEN_DETAIL(id);
    }

    function dispose() {
      CLEAR_STATE();
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

    function mergeJobSummary(previous: JobEntity | undefined, job: Job): JobEntity {
      const logs = previous?.updatedAtUtc === job.updatedAtUtc ? previous.logs : null;
      return { ...job, logs };
    }

    return { byId, ids, jobs, offset, limit, totalCount, listLoading, listError,
      detailLoading, detailError, visible, openDetailId, loadJobs, loadDetails,
      invalidated, recovered, setVisible, setOpenDetail, dispose, goPreviousPage, goNextPage, setPageSize };
  });
}

export const useSystemJobsStore = createSystemJobsStore();
