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

  it('refreshes session and retries once when a protected request returns 401', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ message: 'Unauthorized' })
        })
      } as unknown as Response)
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          success: true,
          data: { csrfToken: 'csrf-refreshed' },
          statusCode: 200
        })
      } as unknown as Response)
      .mockResolvedValueOnce({
        ok: true,
        status: 200,
        json: async () => ({
          success: true,
          data: { id: 42 },
          statusCode: 200
        })
      } as unknown as Response);

    const unauthorizedSpy = vi.fn();
    const { getEnvelope, setUnauthorizedHandler } = await import('./http');
    setUnauthorizedHandler(unauthorizedSpy);

    const result = await getEnvelope<{ id: number }>('/api/board');

    expect(result.ok).toBe(true);
    if (!result.ok) {
      throw new Error('Expected success result.');
    }

    expect(result.data.data).toEqual({ id: 42 });
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(fetchMock.mock.calls[1]?.[0]).toBe('http://localhost:5173/api/auth/refresh');
    expect(unauthorizedSpy).not.toHaveBeenCalled();
  });

  it('invokes unauthorized handler when refresh fails after a 401', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ message: 'Unauthorized' })
        })
      } as unknown as Response)
      .mockResolvedValueOnce({
        ok: false,
        status: 401,
        clone: () => ({
          json: async () => ({ message: 'Refresh failed' })
        })
      } as unknown as Response);

    const unauthorizedSpy = vi.fn();
    const { getEnvelope, setUnauthorizedHandler } = await import('./http');
    setUnauthorizedHandler(unauthorizedSpy);

    const result = await getEnvelope<unknown>('/api/board');

    expect(result.ok).toBe(false);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe('http://localhost:5173/api/auth/refresh');
    expect(unauthorizedSpy).toHaveBeenCalledTimes(1);
  });

  it('does not invoke unauthorized handler for login endpoint 401 responses', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: false,
      status: 401,
      clone: () => ({
        json: async () => ({ message: 'Invalid username or password.' })
      })
    } as unknown as Response);

    const unauthorizedSpy = vi.fn();
    const { postData, setUnauthorizedHandler } = await import('./http');
    setUnauthorizedHandler(unauthorizedSpy);

    const result = await postData('/api/auth/login', { userName: 'admin', password: 'bad' });

    expect(result.ok).toBe(false);
    expect(unauthorizedSpy).not.toHaveBeenCalled();
  });

  it('returns binary payload with filename from content-disposition', async () => {
    const fetchMock = vi.mocked(fetch);
    const fileBlob = new Blob(['zip-content'], { type: 'application/zip' });
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      headers: new Headers({
        'Content-Type': 'application/zip',
        'Content-Disposition': "attachment; filename*=UTF-8''BoardOil.boardoil.zip"
      }),
      blob: async () => fileBlob
    } as unknown as Response);

    const { getBinary } = await import('./http');
    const result = await getBinary('/api/boards/1/export');

    expect(result.ok).toBe(true);
    if (!result.ok) {
      throw new Error('Expected success result.');
    }

    expect(result.data.fileName).toBe('BoardOil.boardoil.zip');
    expect(result.data.contentType).toBe('application/zip');
    expect(result.data.blob).toBe(fileBlob);
  });
});
