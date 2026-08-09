import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import {
  createErrorLogsApi,
  type ErrorLogsApi
} from '../../shared/api/errorLogsApi';
import type {
  ErrorLog,
  ErrorLogDetails,
  ErrorLogPurgeResult
} from '../../shared/types/errorLogTypes';

export const ERROR_LOG_PAGE_SIZE_OPTIONS = [50, 100, 200] as const;
export const DEFAULT_ERROR_LOG_PAGE_SIZE = 100;

type ErrorLogEntity = ErrorLog & {
  stackTrace: string | null;
  contextJson: string | null;
  hasCachedDetails: boolean;
};

export function createSystemErrorLogsStore(
  errorLogsApi: ErrorLogsApi = createErrorLogsApi()
) {
  return defineStore('systemErrorLogs', () => {
    const listLoading = ref(false);
    const listErrorMessage = ref<string | null>(null);
    const detailLoadingById = ref<Record<number, boolean>>({});
    const detailErrorById = ref<Record<number, string | null>>({});
    const orderedErrorLogIds = ref<number[]>([]);
    const errorLogById = ref<Record<number, ErrorLogEntity>>({});
    const offset = ref(0);
    const limit = ref(DEFAULT_ERROR_LOG_PAGE_SIZE);
    const totalCount = ref(0);
    const errorLogs = computed(() =>
      orderedErrorLogIds.value
        .map(id => errorLogById.value[id])
        .filter(errorLog => errorLog !== undefined)
    );

    async function loadErrorLogs(nextOffset = offset.value, nextLimit = limit.value) {
      listLoading.value = true;
      listErrorMessage.value = null;
      try {
        const result = await errorLogsApi.getErrorLogs(nextOffset, nextLimit);
        if (!result.ok) {
          orderedErrorLogIds.value = [];
          totalCount.value = 0;
          listErrorMessage.value = result.error.message;
          return false;
        }

        mergeErrorLogSummaries(result.data.items);
        offset.value = result.data.offset;
        limit.value = result.data.limit;
        totalCount.value = result.data.totalCount;
        return true;
      } finally {
        listLoading.value = false;
      }
    }

    async function loadErrorLogDetails(errorLogId: number, force = false) {
      if (!force && errorLogById.value[errorLogId]?.hasCachedDetails === true) {
        return errorLogById.value[errorLogId] ?? null;
      }

      setDetailLoading(errorLogId, true);
      setDetailError(errorLogId, null);
      try {
        const result = await errorLogsApi.getErrorLogDetails(errorLogId);
        if (!result.ok) {
          setDetailError(errorLogId, result.error.message);
          markDetailsNotCached(errorLogId);
          return null;
        }

        mergeErrorLogDetails(result.data);
        return errorLogById.value[errorLogId] ?? null;
      } finally {
        setDetailLoading(errorLogId, false);
      }
    }

    async function goPreviousPage() {
      if (offset.value <= 0) {
        return false;
      }

      return await loadErrorLogs(Math.max(0, offset.value - limit.value), limit.value);
    }

    async function goNextPage() {
      if (offset.value + errorLogs.value.length >= totalCount.value) {
        return false;
      }

      return await loadErrorLogs(offset.value + limit.value, limit.value);
    }

    async function setPageSize(pageSize: number) {
      return await loadErrorLogs(0, pageSize);
    }

    async function purgeExpiredErrorLogs(): Promise<ErrorLogPurgeResult | null> {
      const result = await errorLogsApi.purgeExpiredErrorLogs();
      if (!result.ok) {
        listErrorMessage.value = result.error.message;
        return null;
      }

      await loadErrorLogs(0, limit.value);
      return result.data;
    }

    function mergeErrorLogSummaries(summaries: ErrorLog[]) {
      const nextById: Record<number, ErrorLogEntity> = { ...errorLogById.value };
      for (const summary of summaries) {
        nextById[summary.id] = mergeErrorLogSummary(errorLogById.value[summary.id], summary);
      }

      orderedErrorLogIds.value = summaries.map(errorLog => errorLog.id);
      errorLogById.value = nextById;
    }

    function mergeErrorLogSummary(
      existing: ErrorLogEntity | undefined,
      summary: ErrorLog
    ): ErrorLogEntity {
      return {
        ...summary,
        stackTrace: existing?.stackTrace ?? null,
        contextJson: existing?.contextJson ?? null,
        hasCachedDetails: existing?.hasCachedDetails ?? false
      };
    }

    function mergeErrorLogDetails(details: ErrorLogDetails) {
      const existing = errorLogById.value[details.id];
      const { stackTrace, contextJson, ...summary } = details;
      errorLogById.value = {
        ...errorLogById.value,
        [details.id]: {
          ...mergeErrorLogSummary(existing, summary),
          stackTrace,
          contextJson,
          hasCachedDetails: true
        }
      };
    }

    function markDetailsNotCached(errorLogId: number) {
      const existing = errorLogById.value[errorLogId];
      if (!existing) {
        return;
      }

      errorLogById.value = {
        ...errorLogById.value,
        [errorLogId]: {
          ...existing,
          stackTrace: null,
          contextJson: null,
          hasCachedDetails: false
        }
      };
    }

    function setDetailLoading(errorLogId: number, isLoading: boolean) {
      detailLoadingById.value = {
        ...detailLoadingById.value,
        [errorLogId]: isLoading
      };
    }

    function setDetailError(errorLogId: number, message: string | null) {
      detailErrorById.value = {
        ...detailErrorById.value,
        [errorLogId]: message
      };
    }

    return {
      listLoading,
      listErrorMessage,
      detailLoadingById,
      detailErrorById,
      errorLogById,
      errorLogs,
      offset,
      limit,
      totalCount,
      loadErrorLogs,
      loadErrorLogDetails,
      goPreviousPage,
      goNextPage,
      setPageSize,
      purgeExpiredErrorLogs
    };
  });
}

export const useSystemErrorLogsStore = createSystemErrorLogsStore();
