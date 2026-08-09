import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ok } from '../types/result';

const getEnvelope = vi.fn();
const postData = vi.fn();

vi.mock('./http', () => ({
  getEnvelope: (...args: unknown[]) => getEnvelope(...args),
  postData: (...args: unknown[]) => postData(...args)
}));

import { createErrorLogsApi } from './errorLogsApi';

describe('errorLogsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads a bounded page from the system error-log endpoint', async () => {
    getEnvelope.mockResolvedValue(ok({
      success: true,
      statusCode: 200,
      data: {
        items: [],
        offset: 50,
        limit: 100,
        totalCount: 120
      }
    }));
    const api = createErrorLogsApi();

    const result = await api.getErrorLogs(50, 100);

    expect(getEnvelope).toHaveBeenCalledWith('/api/system/error-logs?offset=50&limit=100');
    expect(result.ok).toBe(true);
  });

  it('loads details using the numeric error-log id', async () => {
    getEnvelope.mockResolvedValue(ok({
      success: true,
      statusCode: 200,
      data: { id: 42 }
    }));
    const api = createErrorLogsApi();

    await api.getErrorLogDetails(42);

    expect(getEnvelope).toHaveBeenCalledWith('/api/system/error-logs/42');
  });

  it('posts a manual retention purge request', async () => {
    postData.mockResolvedValue(ok({
      retentionDays: 14,
      cutoffUtc: '2026-07-26T00:00:00Z',
      deletedCount: 3
    }));
    const api = createErrorLogsApi();

    const result = await api.purgeExpiredErrorLogs();

    expect(postData).toHaveBeenCalledWith('/api/system/error-logs:purge', {});
    expect(result).toEqual(ok({
      retentionDays: 14,
      cutoffUtc: '2026-07-26T00:00:00Z',
      deletedCount: 3
    }));
  });
});
