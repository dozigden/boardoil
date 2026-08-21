import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { OAuthTokenAuditsApi } from '../../shared/api/oauthTokenAuditsApi';
import type { SystemApi } from '../../shared/api/systemApi';
import type { ConfigurationDto } from '../../shared/types/configurationTypes';
import type { OAuthTokenAudit } from '../../shared/types/oauthTokenAuditTypes';
import { err, ok } from '../../shared/types/result';

vi.mock('../../shared/api/oauthTokenAuditsApi', () => ({
  createOAuthTokenAuditsApi: vi.fn()
}));
vi.mock('../../shared/api/systemApi', () => ({
  createSystemApi: vi.fn()
}));

import { createSystemOAuthTokenAuditsStore } from './systemOAuthTokenAuditsStore';

describe('systemOAuthTokenAuditsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('loads capture state and historical diagnostics together', async () => {
    const auditsApi = createAuditsApi();
    const systemApi = createConfigurationApi(false);
    auditsApi.getOAuthTokenAudits.mockResolvedValue(ok({
      items: [newAudit(2), newAudit(1)],
      offset: 0,
      limit: 2,
      totalCount: 2
    }));
    const useStore = createSystemOAuthTokenAuditsStore(auditsApi, systemApi);
    const store = useStore();

    const loaded = await store.refresh();

    expect(loaded).toBe(true);
    expect(systemApi.getConfiguration).toHaveBeenCalledTimes(1);
    expect(auditsApi.getOAuthTokenAudits).toHaveBeenCalledWith(0, 100);
    expect(store.captureEnabled).toBe(false);
    expect(store.audits.map(audit => audit.id)).toEqual([2, 1]);
  });

  it('navigates through pages returned by the API', async () => {
    const auditsApi = createAuditsApi();
    const systemApi = createConfigurationApi(true);
    auditsApi.getOAuthTokenAudits
      .mockResolvedValueOnce(ok({
        items: [newAudit(3), newAudit(2)],
        offset: 0,
        limit: 2,
        totalCount: 3
      }))
      .mockResolvedValueOnce(ok({
        items: [newAudit(1)],
        offset: 2,
        limit: 2,
        totalCount: 3
      }));
    const useStore = createSystemOAuthTokenAuditsStore(auditsApi, systemApi);
    const store = useStore();

    await store.loadAudits(0, 2);
    await store.goNextPage();

    expect(auditsApi.getOAuthTokenAudits).toHaveBeenNthCalledWith(1, 0, 2);
    expect(auditsApi.getOAuthTokenAudits).toHaveBeenNthCalledWith(2, 2, 2);
    expect(store.audits.map(audit => audit.id)).toEqual([1]);
    expect(store.offset).toBe(2);
  });

  it('reports list and capture-state failures independently', async () => {
    const auditsApi = createAuditsApi();
    const systemApi = createConfigurationApi(true);
    auditsApi.getOAuthTokenAudits.mockResolvedValue(err({
      kind: 'api',
      message: 'Diagnostics unavailable.'
    }));
    systemApi.getConfiguration.mockResolvedValue(err({
      kind: 'api',
      message: 'Configuration unavailable.'
    }));
    const useStore = createSystemOAuthTokenAuditsStore(auditsApi, systemApi);
    const store = useStore();

    const loaded = await store.refresh();

    expect(loaded).toBe(false);
    expect(store.listErrorMessage).toBe('Diagnostics unavailable.');
    expect(store.captureStateErrorMessage).toBe('Configuration unavailable.');
    expect(store.captureEnabled).toBeNull();
  });

  it('purges expired OAuth logs then reloads the first page', async () => {
    const auditsApi = createAuditsApi();
    const systemApi = createConfigurationApi(true);
    auditsApi.purgeExpiredOAuthTokenAudits.mockResolvedValue(ok({
      retentionDays: 14,
      cutoffUtc: '2026-08-07T00:00:00Z',
      deletedCount: 4
    }));
    auditsApi.getOAuthTokenAudits.mockResolvedValue(ok({
      items: [],
      offset: 0,
      limit: 100,
      totalCount: 0
    }));
    const useStore = createSystemOAuthTokenAuditsStore(auditsApi, systemApi);
    const store = useStore();

    const result = await store.purgeExpiredAudits();

    expect(result?.deletedCount).toBe(4);
    expect(auditsApi.getOAuthTokenAudits).toHaveBeenCalledWith(0, 100);
    expect(store.offset).toBe(0);
  });
});

function createAuditsApi() {
  return {
    getOAuthTokenAudits: vi.fn(),
    purgeExpiredOAuthTokenAudits: vi.fn()
  } as unknown as {
    [K in keyof OAuthTokenAuditsApi]: ReturnType<typeof vi.fn>;
  } & OAuthTokenAuditsApi;
}

function createConfigurationApi(enabled: boolean) {
  const configuration: ConfigurationDto = {
    allowInsecureCookies: false,
    mcpPublicBaseUrl: null,
    oauthLifecycleDiagnosticsEnabled: enabled,
    oauthLifecycleDiagnosticsRetentionDays: 14
  };
  return {
    getConfiguration: vi.fn().mockResolvedValue(ok(configuration))
  } as unknown as {
    [K in keyof SystemApi]: ReturnType<typeof vi.fn>;
  } & SystemApi;
}

function newAudit(id: number): OAuthTokenAudit {
  return {
    id,
    occurredAtUtc: `2026-08-21T20:0${id}:00Z`,
    outcome: 'Succeeded',
    grantType: 'refresh_token',
    requestedScopes: 'mcp:read',
    errorCode: null,
    errorDescription: null,
    errorUri: null,
    presentedTokenFingerprint: `sha256:presented-${id}`,
    issuedRefreshTokenFingerprint: `sha256:issued-${id}`,
    authorizationId: `authorization-${id}`,
    oauthClientId: `client-${id}`,
    oauthConnectionId: id,
    oauthConnectionName: `Connection ${id}`,
    ownerUserId: 1,
    ownerUserName: 'admin',
    oauthClientDisplayName: 'Codex',
    resource: 'https://boardoil.example.com/mcp',
    traceIdentifier: `trace-${id}`,
    userAgent: 'test-agent'
  };
}
