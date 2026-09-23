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
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it('returns JSON responses that are not wrapped in an API envelope', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ resource: 'https://boardoil.example.com/mcp' })
    } as unknown as Response);

    const { getJson } = await import('./http');
    const result = await getJson<{ resource: string }>(
      '/.well-known/oauth-protected-resource/mcp'
    );

    expect(result).toEqual({
      ok: true,
      data: { resource: 'https://boardoil.example.com/mcp' }
    });
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5173/.well-known/oauth-protected-resource/mcp',
      expect.objectContaining({ method: 'GET', credentials: 'include' })
    );
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

  it('invokes unauthorized handler when notifyUnauthorized is called directly', async () => {
    const unauthorizedSpy = vi.fn();
    const { notifyUnauthorized, setUnauthorizedHandler } = await import('./http');
    setUnauthorizedHandler(unauthorizedSpy);

    notifyUnauthorized();
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

  it.each([{}, { csrfToken: 'another-token' }])('retains the tab token after refresh with response data %j', async (data) => {
    const fetchMock = vi.mocked(fetch);
    fetchMock
      .mockResolvedValueOnce(new Response('', { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ success: true, data })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ success: true, data: { id: 42 } })));
    const { postJson, setCsrfToken } = await import('./http');
    setCsrfToken('tab-token');

    const result = await postJson('/api/boards', { name: 'Created' });

    expect(result).toEqual({ ok: true, data: undefined });
    expect(fetchMock).toHaveBeenCalledTimes(3);
    for (const index of [0, 2]) {
      expect(new Headers(fetchMock.mock.calls[index]?.[1]?.headers).get('X-BoardOil-CSRF')).toBe('tab-token');
    }
  });

  it('does not replace the token or replay again when a refreshed mutation fails csrf validation', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock
      .mockResolvedValueOnce(new Response('', { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ success: true, data: {} })))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        success: false, statusCode: 403, message: 'CSRF validation failed.'
      }), { status: 403 }));
    const unauthorizedSpy = vi.fn();
    const { postJson, setCsrfToken, setUnauthorizedHandler } = await import('./http');
    setCsrfToken('stale-tab-token');
    setUnauthorizedHandler(unauthorizedSpy);

    const result = await postJson('/api/boards', { name: 'Rejected' });

    expect(result).toMatchObject({ ok: false, error: { statusCode: 403 } });
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(unauthorizedSpy).toHaveBeenCalledTimes(1);
    expect(new Headers(fetchMock.mock.calls[2]?.[1]?.headers).get('X-BoardOil-CSRF')).toBe('stale-tab-token');
  });

  it('clears stale authentication without retrying when csrf validation fails', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(new Response(JSON.stringify({
      success: false,
      statusCode: 403,
      message: 'CSRF validation failed.'
    }), { status: 403, headers: { 'Content-Type': 'application/json' } }));

    const unauthorizedSpy = vi.fn();
    const { postJson, setCsrfToken, setUnauthorizedHandler } = await import('./http');
    setUnauthorizedHandler(unauthorizedSpy);
    setCsrfToken('csrf-stale');

    const result = await postJson('/api/boards', { name: 'Rejected' });

    expect(result).toMatchObject({
      ok: false,
      error: { kind: 'http', statusCode: 403, message: 'CSRF validation failed.' }
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(unauthorizedSpy).toHaveBeenCalledTimes(1);
  });

  it('does not retry an ordinary forbidden response', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue(new Response(JSON.stringify({
      success: false,
      statusCode: 403,
      message: 'Access denied.'
    }), { status: 403, headers: { 'Content-Type': 'application/json' } }));

    const { postJson, setCsrfToken } = await import('./http');
    setCsrfToken('csrf-token');

    const result = await postJson('/api/boards', { name: 'Forbidden' });

    expect(result).toMatchObject({
      ok: false,
      error: { kind: 'http', statusCode: 403, message: 'Access denied.' }
    });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it.each(['headers', 'body'])('aborts stalled refresh %s and allows a later refresh', async stage => {
    vi.useFakeTimers();
    const fetchMock = vi.mocked(fetch);
    let refreshSignal!: AbortSignal;
    fetchMock.mockImplementationOnce(async (_url, init) => {
      refreshSignal = init!.signal!;
      const stalled = new Promise<never>((_resolve, reject) => {
        refreshSignal.addEventListener('abort', () => reject(refreshSignal.reason), { once: true });
      });
      if (stage === 'headers') {
        return stalled;
      }
      return { ok: true, json: () => stalled } as unknown as Response;
    });
    const { attemptSessionRefresh } = await import('./http');
    const firstRefresh = attemptSessionRefresh();
    const sharedRefresh = attemptSessionRefresh();
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(30_000);
    expect(refreshSignal.aborted).toBe(true);
    expect(await firstRefresh).toBe(false);
    expect(await sharedRefresh).toBe(false);

    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ success: true })));
    expect(await attemptSessionRefresh()).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(vi.getTimerCount()).toBe(0);
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

  it('does not invoke unauthorized handler when the session probe confirms an anonymous browser', async () => {
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
        status: 401
      } as unknown as Response);

    const unauthorizedSpy = vi.fn();
    const { getEnvelope, setUnauthorizedHandler } = await import('./http');
    setUnauthorizedHandler(unauthorizedSpy);

    const result = await getEnvelope<unknown>('/api/auth/me');
    await Promise.resolve();

    expect(result.ok).toBe(false);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[1]?.[0]).toBe('http://localhost:5173/api/auth/refresh');
    expect(unauthorizedSpy).not.toHaveBeenCalled();
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

  it('posts quiet diagnostics with csrf without refreshing or handling unauthorized responses', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: false,
      status: 401
    } as unknown as Response);
    const unauthorizedSpy = vi.fn();
    const { postJsonQuiet, setCsrfToken, setUnauthorizedHandler } = await import('./http');
    setCsrfToken('csrf-token');
    setUnauthorizedHandler(unauthorizedSpy);

    await postJsonQuiet('/api/system/error-logs:report-client-error', { message: 'boom' });

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      'http://localhost:5173/api/system/error-logs:report-client-error'
    );
    const request = fetchMock.mock.calls[0]?.[1];
    expect(request?.credentials).toBe('include');
    expect(request?.body).toBe('{"message":"boom"}');
    const headers = request?.headers as Headers;
    expect(headers.get('Content-Type')).toBe('application/json');
    expect(headers.get('X-BoardOil-CSRF')).toBe('csrf-token');
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

  it('posts form data payloads without forcing json content type', async () => {
    const fetchMock = vi.mocked(fetch);
    fetchMock.mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({
        success: true,
        data: { id: 9, name: 'Imported' },
        statusCode: 200
      })
    } as unknown as Response);

    const formData = new FormData();
    formData.append('name', 'Imported');
    formData.append('file', new Blob(['zip-data'], { type: 'application/zip' }), 'board.boardoil.zip');

    const { postFormData, setCsrfToken } = await import('./http');
    setCsrfToken('csrf-token');
    const result = await postFormData<{ id: number; name: string }>('/api/boards/import', formData);

    expect(result.ok).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const fetchArgs = fetchMock.mock.calls[0];
    expect(fetchArgs?.[0]).toBe('http://localhost:5173/api/boards/import');
    expect(fetchArgs?.[1]?.credentials).toBe('include');
    expect(fetchArgs?.[1]?.body).toBe(formData);
    const headers = fetchArgs?.[1]?.headers as Headers;
    expect(headers.get('Content-Type')).toBeNull();
    expect(headers.get('X-BoardOil-CSRF')).toBe('csrf-token');
  });
});
