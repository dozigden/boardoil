import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ErrorLogsApi } from '../../shared/api/errorLogsApi';
import type { ErrorLog, ErrorLogDetails } from '../../shared/types/errorLogTypes';
import { ok } from '../../shared/types/result';

vi.mock('../../shared/api/errorLogsApi', () => ({
  createErrorLogsApi: vi.fn()
}));

import { createSystemErrorLogsStore } from './systemErrorLogsStore';

describe('systemErrorLogsStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('loads pages newest-first as returned by the API and navigates forward', async () => {
    const api = createApi();
    api.getErrorLogs
      .mockResolvedValueOnce(ok({
        items: [newErrorLog(3, 'third'), newErrorLog(2, 'second')],
        offset: 0,
        limit: 2,
        totalCount: 3
      }))
      .mockResolvedValueOnce(ok({
        items: [newErrorLog(1, 'first')],
        offset: 2,
        limit: 2,
        totalCount: 3
      }));
    const useStore = createSystemErrorLogsStore(api);
    const store = useStore();

    await store.loadErrorLogs(0, 2);
    await store.goNextPage();

    expect(api.getErrorLogs).toHaveBeenNthCalledWith(1, 0, 2);
    expect(api.getErrorLogs).toHaveBeenNthCalledWith(2, 2, 2);
    expect(store.errorLogs.map(errorLog => errorLog.message)).toEqual(['first']);
    expect(store.offset).toBe(2);
    expect(store.totalCount).toBe(3);
  });

  it('loads and caches full details', async () => {
    const api = createApi();
    api.getErrorLogs.mockResolvedValue(ok({
      items: [newErrorLog(7, 'failed')],
      offset: 0,
      limit: 100,
      totalCount: 1
    }));
    api.getErrorLogDetails.mockResolvedValue(ok(newErrorLogDetails(7, 'failed')));
    const useStore = createSystemErrorLogsStore(api);
    const store = useStore();
    await store.loadErrorLogs();

    const first = await store.loadErrorLogDetails(7);
    const cached = await store.loadErrorLogDetails(7);

    expect(api.getErrorLogDetails).toHaveBeenCalledTimes(1);
    expect(first?.stackTrace).toBe('stack failed');
    expect(cached?.contextJson).toBe('{"endpoint":"failed"}');
  });

  it('purges expired logs then reloads the first page', async () => {
    const api = createApi();
    api.purgeExpiredErrorLogs.mockResolvedValue(ok({
      retentionDays: 14,
      cutoffUtc: '2026-07-26T00:00:00Z',
      deletedCount: 4
    }));
    api.getErrorLogs.mockResolvedValue(ok({
      items: [],
      offset: 0,
      limit: 100,
      totalCount: 0
    }));
    const useStore = createSystemErrorLogsStore(api);
    const store = useStore();

    const result = await store.purgeExpiredErrorLogs();

    expect(result?.deletedCount).toBe(4);
    expect(api.getErrorLogs).toHaveBeenCalledWith(0, 100);
    expect(store.offset).toBe(0);
  });
});

function createApi() {
  return {
    getErrorLogs: vi.fn(),
    getErrorLogDetails: vi.fn(),
    purgeExpiredErrorLogs: vi.fn()
  } as unknown as {
    [K in keyof ErrorLogsApi]: ReturnType<typeof vi.fn>;
  } & ErrorLogsApi;
}

function newErrorLog(id: number, message: string): ErrorLog {
  return {
    id,
    occurredAtUtc: `2026-08-09T12:0${id}:00Z`,
    source: 'Backend',
    area: 'ApiRequest',
    exceptionType: 'System.InvalidOperationException',
    message,
    traceIdentifier: `trace-${id}`,
    requestMethod: 'GET',
    requestPath: '/api/test',
    actorUserId: 1,
    createdAtUtc: '2026-08-09T12:00:00Z',
    updatedAtUtc: '2026-08-09T12:00:00Z'
  };
}

function newErrorLogDetails(id: number, message: string): ErrorLogDetails {
  return {
    ...newErrorLog(id, message),
    stackTrace: `stack ${message}`,
    contextJson: `{"endpoint":"${message}"}`
  };
}
