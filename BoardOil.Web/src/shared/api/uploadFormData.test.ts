import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

class UploadRequest {
  static instances: UploadRequest[] = [];
  withCredentials = false;
  status = 201;
  responseText = JSON.stringify({ success: true, data: { id: 7 } });
  headers: Record<string, string> = {};
  upload = { onprogress: null as ((event: { lengthComputable: boolean; loaded: number; total: number }) => void) | null };
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;
  open = vi.fn();
  send = vi.fn();
  constructor() { UploadRequest.instances.push(this); }
  setRequestHeader(name: string, value: string) { this.headers[name] = value; }
  getResponseHeader() { return 'application/json'; }
  abort() { this.onabort?.(); }
}

describe('multipart upload transport', () => {
  beforeEach(() => {
    vi.resetModules();
    UploadRequest.instances = [];
    vi.stubGlobal('window', { location: { origin: 'http://localhost:5173' } });
    vi.stubGlobal('XMLHttpRequest', UploadRequest);
    vi.stubGlobal('fetch', vi.fn());
  });
  afterEach(() => vi.unstubAllGlobals());

  it('sends credentials and CSRF without overriding multipart boundaries, with progress', async () => {
    const { uploadFormData, setCsrfToken } = await import('./http');
    setCsrfToken('csrf');
    const progress = vi.fn();
    const result = uploadFormData('/api/boards/1/cards/1/attachments', new FormData(), progress, new AbortController().signal);
    const request = UploadRequest.instances[0]!;
    expect(request.withCredentials).toBe(true);
    expect(request.headers).toEqual({ 'X-BoardOil-CSRF': 'csrf' });
    request.upload.onprogress?.({ lengthComputable: true, loaded: 1, total: 2 });
    request.onload?.();
    expect(progress).toHaveBeenCalledWith(50);
    expect(await result).toEqual({ ok: true, data: { id: 7 } });
  });

  it('refreshes an expired session once and uses the new CSRF token', async () => {
    const { uploadFormData, setCsrfToken } = await import('./http');
    setCsrfToken('old');
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({ success: true, data: { csrfToken: 'new' } })));
    const result = uploadFormData('/api/boards/1/cards/1/attachments', new FormData(), vi.fn(), new AbortController().signal);
    UploadRequest.instances[0]!.status = 401;
    UploadRequest.instances[0]!.onload?.();
    await vi.waitFor(() => expect(UploadRequest.instances).toHaveLength(2));
    expect(UploadRequest.instances[1]!.headers).toEqual({ 'X-BoardOil-CSRF': 'new' });
    UploadRequest.instances[1]!.onload?.();
    expect((await result).ok).toBe(true);
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('returns structured size errors and handles cancellation', async () => {
    const { uploadFormData } = await import('./http');
    const tooLarge = uploadFormData('/api/attachments', new FormData(), vi.fn(), new AbortController().signal);
    UploadRequest.instances[0]!.status = 413;
    UploadRequest.instances[0]!.responseText = JSON.stringify({ success: false, message: 'Too large' });
    UploadRequest.instances[0]!.onload?.();
    expect(await tooLarge).toMatchObject({ ok: false, error: { statusCode: 413, message: 'Too large' } });
    const controller = new AbortController();
    const cancelled = uploadFormData('/api/attachments', new FormData(), vi.fn(), controller.signal);
    controller.abort();
    expect(await cancelled).toMatchObject({ ok: false, error: { message: 'Upload cancelled.' } });
  });
});
