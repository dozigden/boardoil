import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

describe('http api client', () => {
  beforeEach(() => {
    vi.resetModules();
    vi.stubGlobal('window', {
      location: {
        origin: 'http://localhost:5173'
      }
    });
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it('preserves HTTP status code in error payload', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: false,
      status: 401,
      clone: () => ({
        json: async () => ({ message: 'Unauthorized' })
      })
    } as unknown as Response);

    const { getEnvelope } = await import('./http');
    const result = await getEnvelope<unknown>('/api/board');

    expect(result.ok).toBe(false);
    if (result.ok) {
      throw new Error('Expected error result.');
    }

    expect(result.error.kind).toBe('http');
    expect(result.error.statusCode).toBe(401);
    expect(result.error.message).toBe('Unauthorized');
  });

  it('invokes unauthorized handler on 401 responses', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: false,
      status: 401,
      clone: () => ({
        json: async () => ({ message: 'Unauthorized' })
      })
    } as unknown as Response);

    const unauthorizedSpy = vi.fn();
    const { getEnvelope, setUnauthorizedHandler } = await import('./http');
    setUnauthorizedHandler(unauthorizedSpy);

    await getEnvelope<unknown>('/api/board');
    await Promise.resolve();

    expect(unauthorizedSpy).toHaveBeenCalledTimes(1);
  });
});
