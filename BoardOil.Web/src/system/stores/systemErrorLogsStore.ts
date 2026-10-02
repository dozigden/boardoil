import { computed, ref } from 'vue';
import { defineStore } from 'pinia';
import {
  createErrorLogsApi,
  type ErrorLogsApi
} from '../../shared/api/errorLogsApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type {
  ErrorLog,
  ErrorLogDetails,
  ErrorLogList,
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
    const feedback = useUiFeedbackStore();
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

    function SET_ERROR_LOG_PAGE(page: ErrorLogList) {
      const nextById: Record<number, ErrorLogEntity> = { ...errorLogById.value };
      for (const summary of page.items) {
        nextById[summary.id] = mergeErrorLogSummary(errorLogById.value[summary.id], summary);
      }

      orderedErrorLogIds.value = page.items.map(errorLog => errorLog.id);
      errorLogById.value = nextById;
      offset.value = page.offset;
      limit.value = page.limit;
      totalCount.value = page.totalCount;
    }

    function CLEAR_ERROR_LOG_PAGE() {
      orderedErrorLogIds.value = [];
      totalCount.value = 0;
    }

    function UPSERT_ERROR_LOG_DETAILS(details: ErrorLogDetails) {
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

    function CLEAR_ERROR_LOG_DETAILS(errorLogId: number) {
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

    function SET_DETAIL_LOADING(errorLogId: number, isLoading: boolean) {
      detailLoadingById.value = {
        ...detailLoadingById.value,
        [errorLogId]: isLoading
      };
    }

    function SET_DETAIL_ERROR(errorLogId: number, message: string | null) {
      detailErrorById.value = {
        ...detailErrorById.value,
        [errorLogId]: message
      };
    }

    async function loadErrorLogs(nextOffset = offset.value, nextLimit = limit.value) {
      listLoading.value = true;
      listErrorMessage.value = null;
      try {
        const result = await errorLogsApi.getErrorLogs(nextOffset, nextLimit);
        if (!result.ok) {
          CLEAR_ERROR_LOG_PAGE();
          listErrorMessage.value = result.error.message;
          return false;
        }

        SET_ERROR_LOG_PAGE(result.data);
        return true;
      } finally {
        listLoading.value = false;
      }
    }

    async function loadErrorLogDetails(errorLogId: number, force = false) {
      if (!force && errorLogById.value[errorLogId]?.hasCachedDetails === true) {
        return errorLogById.value[errorLogId] ?? null;
      }

      SET_DETAIL_LOADING(errorLogId, true);
      SET_DETAIL_ERROR(errorLogId, null);
      try {
        const result = await errorLogsApi.getErrorLogDetails(errorLogId);
        if (!result.ok) {
          SET_DETAIL_ERROR(errorLogId, result.error.message);
          CLEAR_ERROR_LOG_DETAILS(errorLogId);
          return null;
        }

        UPSERT_ERROR_LOG_DETAILS(result.data);
        return errorLogById.value[errorLogId] ?? null;
      } finally {
        SET_DETAIL_LOADING(errorLogId, false);
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
        feedback.showToast(result.error.message, 'error');
        return null;
      }

      await loadErrorLogs(0, limit.value);
      return result.data;
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
