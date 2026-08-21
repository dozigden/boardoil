import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ok } from '../types/result';

const getEnvelope = vi.fn();
const postData = vi.fn();

vi.mock('./http', () => ({
  getEnvelope: (...args: unknown[]) => getEnvelope(...args),
  postData: (...args: unknown[]) => postData(...args)
}));

import { createOAuthTokenAuditsApi } from './oauthTokenAuditsApi';

describe('oauthTokenAuditsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('loads a bounded page from the administrator OAuth diagnostics endpoint', async () => {
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
    const api = createOAuthTokenAuditsApi();

    const result = await api.getOAuthTokenAudits(50, 100);

    expect(getEnvelope).toHaveBeenCalledWith(
      '/api/system/oauth-token-audits?offset=50&limit=100'
    );
    expect(result.ok).toBe(true);
  });

  it('posts a manual OAuth log retention purge request', async () => {
    postData.mockResolvedValue(ok({
      retentionDays: 14,
      cutoffUtc: '2026-08-07T00:00:00Z',
      deletedCount: 3
    }));
    const api = createOAuthTokenAuditsApi();

    const result = await api.purgeExpiredOAuthTokenAudits();

    expect(postData).toHaveBeenCalledWith('/api/system/oauth-token-audits:purge', {});
    expect(result.ok).toBe(true);
  });
});
