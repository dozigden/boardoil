import { ref } from 'vue';
import { defineStore } from 'pinia';
import {
  createOAuthTokenAuditsApi,
  type OAuthTokenAuditsApi
} from '../../shared/api/oauthTokenAuditsApi';
import { createSystemApi, type SystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { OAuthTokenAudit, OAuthTokenAuditList } from '../../shared/types/oauthTokenAuditTypes';

export const OAUTH_TOKEN_AUDIT_PAGE_SIZE_OPTIONS = [50, 100, 200] as const;
export const DEFAULT_OAUTH_TOKEN_AUDIT_PAGE_SIZE = 100;

export function createSystemOAuthTokenAuditsStore(
  oauthTokenAuditsApi: OAuthTokenAuditsApi = createOAuthTokenAuditsApi(),
  systemApi: SystemApi = createSystemApi()
) {
  return defineStore('systemOAuthTokenAudits', () => {
    const feedback = useUiFeedbackStore();
    const listLoading = ref(false);
    const listErrorMessage = ref<string | null>(null);
    const captureStateLoading = ref(false);
    const captureStateErrorMessage = ref<string | null>(null);
    const captureEnabled = ref<boolean | null>(null);
    const audits = ref<OAuthTokenAudit[]>([]);
    const offset = ref(0);
    const limit = ref(DEFAULT_OAUTH_TOKEN_AUDIT_PAGE_SIZE);
    const totalCount = ref(0);

    function SET_AUDIT_PAGE(page: OAuthTokenAuditList) {
      audits.value = page.items;
      offset.value = page.offset;
      limit.value = page.limit;
      totalCount.value = page.totalCount;
    }

    function CLEAR_AUDIT_PAGE() {
      audits.value = [];
      totalCount.value = 0;
    }

    function SET_CAPTURE_ENABLED(enabled: boolean | null) {
      captureEnabled.value = enabled;
    }

    async function loadAudits(nextOffset = offset.value, nextLimit = limit.value) {
      listLoading.value = true;
      listErrorMessage.value = null;
      try {
        const result = await oauthTokenAuditsApi.getOAuthTokenAudits(nextOffset, nextLimit);
        if (!result.ok) {
          CLEAR_AUDIT_PAGE();
          listErrorMessage.value = result.error.message;
          return false;
        }

        SET_AUDIT_PAGE(result.data);
        return true;
      } finally {
        listLoading.value = false;
      }
    }

    async function loadCaptureState() {
      captureStateLoading.value = true;
      captureStateErrorMessage.value = null;
      try {
        const result = await systemApi.getConfiguration();
        if (!result.ok) {
          SET_CAPTURE_ENABLED(null);
          captureStateErrorMessage.value = result.error.message;
          return false;
        }

        SET_CAPTURE_ENABLED(result.data.oauthLifecycleDiagnosticsEnabled);
        return true;
      } finally {
        captureStateLoading.value = false;
      }
    }

    async function refresh() {
      const [captureLoaded, auditsLoaded] = await Promise.all([
        loadCaptureState(),
        loadAudits()
      ]);
      return captureLoaded && auditsLoaded;
    }

    async function goPreviousPage() {
      if (offset.value <= 0) {
        return false;
      }

      return await loadAudits(Math.max(0, offset.value - limit.value), limit.value);
    }

    async function goNextPage() {
      if (offset.value + audits.value.length >= totalCount.value) {
        return false;
      }

      return await loadAudits(offset.value + limit.value, limit.value);
    }

    async function setPageSize(pageSize: number) {
      return await loadAudits(0, pageSize);
    }

    async function purgeExpiredAudits() {
      const result = await oauthTokenAuditsApi.purgeExpiredOAuthTokenAudits();
      if (!result.ok) {
        feedback.showToast(result.error.message, 'error');
        return null;
      }

      await loadAudits(0, limit.value);
      return result.data;
    }

    return {
      listLoading,
      listErrorMessage,
      captureStateLoading,
      captureStateErrorMessage,
      captureEnabled,
      audits,
      offset,
      limit,
      totalCount,
      loadAudits,
      loadCaptureState,
      refresh,
      goPreviousPage,
      goNextPage,
      setPageSize,
      purgeExpiredAudits
    };
  });
}

export const useSystemOAuthTokenAuditsStore = createSystemOAuthTokenAuditsStore();
