import { ref } from 'vue';
import { defineStore } from 'pinia';
import {
  createOAuthTokenAuditsApi,
  type OAuthTokenAuditsApi
} from '../../shared/api/oauthTokenAuditsApi';
import { createSystemApi, type SystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { OAuthTokenAudit } from '../../shared/types/oauthTokenAuditTypes';

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

    async function loadAudits(nextOffset = offset.value, nextLimit = limit.value) {
      listLoading.value = true;
      listErrorMessage.value = null;
      try {
        const result = await oauthTokenAuditsApi.getOAuthTokenAudits(nextOffset, nextLimit);
        if (!result.ok) {
          audits.value = [];
          totalCount.value = 0;
          listErrorMessage.value = result.error.message;
          return false;
        }

        audits.value = result.data.items;
        offset.value = result.data.offset;
        limit.value = result.data.limit;
        totalCount.value = result.data.totalCount;
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
          captureEnabled.value = null;
          captureStateErrorMessage.value = result.error.message;
          return false;
        }

        captureEnabled.value = result.data.oauthLifecycleDiagnosticsEnabled;
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
