import { apiBase, buildApiUrl } from './config';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { ApiEnvelope } from '../types/boardTypes';
import type { Result } from '../types/result';

let csrfToken: string | null = null;
let unauthorizedHandler: (() => void | Promise<void>) | null = null;
let handlingUnauthorized = false;
let refreshInFlight: Promise<boolean> | null = null;

const csrfValidationFailureMessage = 'CSRF validation failed.';

export type BinaryResponse = {
  blob: Blob;
  fileName: string;
  contentType: string | null;
};

export function setCsrfToken(token: string | null) {
  csrfToken = token;
}

export function setUnauthorizedHandler(handler: (() => void | Promise<void>) | null) {
  unauthorizedHandler = handler;
}

export function notifyUnauthorized() {
  void handleUnauthorized();
}

export async function attemptSessionRefresh() {
  return tryRefreshSession();
}

export async function getEnvelope<T>(path: string): Promise<Result<ApiEnvelope<T>, AppError>> {
  const responseResult = await request(path, { method: 'GET' });
  if (!responseResult.ok) {
    return responseResult;
  }

  const envelopeResult = await parseEnvelope<T>(responseResult.data);
  if (!envelopeResult.ok) {
    return envelopeResult;
  }

  if (responseResult.data.ok && envelopeResult.data.success !== false) {
    return ok(envelopeResult.data);
  }

  return err({
    kind: 'api',
    message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`,
    validationErrors: envelopeResult.data.validationErrors
  });
}

export async function getJson<T>(path: string): Promise<Result<T, AppError>> {
  const responseResult = await request(path, { method: 'GET' });
  if (!responseResult.ok) {
    return responseResult;
  }

  const body = (await responseResult.data.json().catch(() => null)) as T | null;
  if (body === null) {
    return err({
      kind: 'parse',
      message: 'Unexpected empty API response.'
    });
  }

  return ok(body);
}

export async function postJson(path: string, payload: unknown): Promise<Result<void, AppError>> {
  return sendJson('POST', path, payload);
}

export async function postJsonQuiet(path: string, payload: unknown): Promise<void> {
  try {
    await send(path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
  } catch {
    // Best-effort diagnostics must never affect normal application behaviour.
  }
}

export async function patchJson(path: string, payload: unknown): Promise<Result<void, AppError>> {
  return sendJson('PATCH', path, payload);
}

export async function putJson(path: string, payload: unknown): Promise<Result<void, AppError>> {
  return sendJson('PUT', path, payload);
}

export async function postData<T>(path: string, payload: unknown): Promise<Result<T, AppError>> {
  return sendJsonForData<T>('POST', path, payload);
}

export async function postFormData<T>(path: string, payload: FormData): Promise<Result<T, AppError>> {
  return sendFormForData<T>('POST', path, payload);
}

export async function uploadFormData<T>(path: string, payload: FormData, onProgress: (percent: number) => void,
  signal: AbortSignal): Promise<Result<T, AppError>> {
  async function upload(): Promise<Result<Response, AppError>> {
    return new Promise(resolve => {
      if (signal.aborted) { resolve(err({ kind: 'network', message: 'Upload cancelled.' })); return; }
      const xhr = new XMLHttpRequest();
      xhr.open('POST', buildApiUrl(path));
      xhr.withCredentials = true;
      if (csrfToken) { xhr.setRequestHeader('X-BoardOil-CSRF', csrfToken); }
      const abort = () => xhr.abort();
      const finish = (result: Result<Response, AppError>) => {
        signal.removeEventListener('abort', abort);
        resolve(result);
      };
      xhr.upload.onprogress = event => {
        if (event.lengthComputable) { onProgress(Math.round(event.loaded * 100 / event.total)); }
      };
      xhr.onload = () => {
        try {
          const body = xhr.status === 204 || xhr.status === 205 || xhr.status === 304 ? null : xhr.responseText;
          finish(ok(new Response(body, {
            status: xhr.status,
            headers: { 'Content-Type': xhr.getResponseHeader('Content-Type') ?? 'application/json' }
          })));
        } catch { finish(err({ kind: 'parse', message: 'Unexpected upload response.' })); }
      };
      xhr.onerror = () => finish(err({ kind: 'network', message: 'Upload failed. Check your connection and retry.' }));
      xhr.onabort = () => finish(err({ kind: 'network', message: 'Upload cancelled.' }));
      signal.addEventListener('abort', abort, { once: true });
      xhr.send(payload);
    });
  }
  let response = await upload();
  if (response.ok && response.data.status === 401 && shouldAttemptSessionRefresh(path) && !signal.aborted) {
    if (await tryRefreshSession()) { response = await upload(); }
  }
  if (!response.ok) { return response; }
  if (!response.data.ok) {
    const error = await tryParseErrorPayload(response.data);
    if (response.data.status === 401 || isCsrfValidationFailure(response.data.status, error?.message)) {
      notifyUnauthorized();
    }
    return err({ kind: 'http', statusCode: response.data.status,
      message: error?.message ?? formatStatusMessage(response.data), validationErrors: error?.validationErrors });
  }
  const envelope = await parseEnvelope<T>(response.data);
  if (!envelope.ok) { return envelope; }
  if (envelope.data.success === false || envelope.data.data == null) {
    return err({ kind: 'api', message: envelope.data.message ?? 'Upload response was missing.' });
  }
  return ok(envelope.data.data);
}

export async function patchData<T>(path: string, payload: unknown): Promise<Result<T, AppError>> {
  return sendJsonForData<T>('PATCH', path, payload);
}

export async function putData<T>(path: string, payload: unknown): Promise<Result<T, AppError>> {
  return sendJsonForData<T>('PUT', path, payload);
}

export async function deleteJson(path: string): Promise<Result<void, AppError>> {
  const responseResult = await request(path, { method: 'DELETE' });
  if (!responseResult.ok) {
    return responseResult;
  }

  const envelopeResult = await parseEnvelope<unknown>(responseResult.data);
  if (!envelopeResult.ok) {
    return envelopeResult;
  }

  if (responseResult.data.ok && envelopeResult.data.success !== false) {
    return ok(undefined);
  }

  return err({
    kind: 'api',
    message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`,
    validationErrors: envelopeResult.data.validationErrors
  });
}

export async function getBinary(path: string): Promise<Result<BinaryResponse, AppError>> {
  const responseResult = await request(path, { method: 'GET' });
  if (!responseResult.ok) {
    return responseResult;
  }

  const response = responseResult.data;
  let blob: Blob;
  try {
    blob = await response.blob();
  } catch {
    return err({
      kind: 'parse',
      message: 'Unexpected binary API response.'
    });
  }

  return ok({
    blob,
    fileName: extractFileName(response.headers.get('Content-Disposition')!),
    contentType: response.headers.get('Content-Type')
  });
}

export async function getBlob(path: string): Promise<Result<Blob, AppError>> {
  const responseResult = await request(path, { method: 'GET' });
  if (!responseResult.ok) { return responseResult; }
  try { return ok(await responseResult.data.blob()); }
  catch { return err({ kind: 'parse', message: 'Unexpected binary API response.' }); }
}

export async function putBinary(path: string, payload: Blob): Promise<Result<void, AppError>> {
  const responseResult = await request(path, {
    method: 'PUT',
    headers: { 'Content-Type': payload.type || 'application/octet-stream' },
    body: payload
  });
  if (!responseResult.ok) { return responseResult; }
  const envelopeResult = await parseEnvelope<unknown>(responseResult.data);
  if (!envelopeResult.ok) { return envelopeResult; }
  if (envelopeResult.data.success === false) {
    return err({ kind: 'api', message: envelopeResult.data.message ?? 'Thumbnail could not be stored.' });
  }
  return ok(undefined);
}

async function sendJson(
  method: 'POST' | 'PUT' | 'PATCH',
  path: string,
  payload: unknown
): Promise<Result<void, AppError>> {
  const responseResult = await request(path, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!responseResult.ok) {
    return responseResult;
  }

  const envelopeResult = await parseEnvelope<unknown>(responseResult.data);
  if (!envelopeResult.ok) {
    return envelopeResult;
  }

  if (responseResult.data.ok && envelopeResult.data.success !== false) {
    return ok(undefined);
  }

  return err({
    kind: 'api',
    message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`,
    validationErrors: envelopeResult.data.validationErrors
  });
}

async function sendJsonForData<T>(
  method: 'POST' | 'PUT' | 'PATCH',
  path: string,
  payload: unknown
): Promise<Result<T, AppError>> {
  const responseResult = await request(path, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  if (!responseResult.ok) {
    return responseResult;
  }

  const envelopeResult = await parseEnvelope<T>(responseResult.data);
  if (!envelopeResult.ok) {
    return envelopeResult;
  }

  if (!responseResult.data.ok || envelopeResult.data.success === false) {
    return err({
      kind: 'api',
      message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`
    });
  }

  if (envelopeResult.data.data === null) {
    return err({
      kind: 'parse',
      message: 'Expected response payload was missing.'
    });
  }

  return ok(envelopeResult.data.data);
}

async function sendFormForData<T>(
  method: 'POST',
  path: string,
  payload: FormData
): Promise<Result<T, AppError>> {
  const responseResult = await request(path, {
    method,
    body: payload
  });
  if (!responseResult.ok) {
    return responseResult;
  }

  const envelopeResult = await parseEnvelope<T>(responseResult.data);
  if (!envelopeResult.ok) {
    return envelopeResult;
  }

  if (!responseResult.data.ok || envelopeResult.data.success === false) {
    return err({
      kind: 'api',
      message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`
    });
  }

  if (envelopeResult.data.data === null) {
    return err({
      kind: 'parse',
      message: 'Expected response payload was missing.'
    });
  }

  return ok(envelopeResult.data.data);
}

async function request(path: string, init: RequestInit): Promise<Result<Response, AppError>> {
  try {
    let response = await send(path, init);

    if (response.status === 401 && shouldAttemptSessionRefresh(path)) {
      if (await tryRefreshSession()) {
        response = await send(path, init);
      }
    }

    if (!response.ok) {
      const envelope = await tryParseErrorPayload(response);
      if (response.status === 401 && shouldHandleUnauthorized(path)) {
        void handleUnauthorized();
      }
      if (isStateChangingRequest(init) && isCsrfValidationFailure(response.status, envelope?.message)) {
        void handleUnauthorized();
      }

      return err({
        kind: 'http',
        message: envelope?.message ?? formatStatusMessage(response),
        statusCode: response.status,
        validationErrors: envelope?.validationErrors
      });
    }

    return ok(response);
  } catch {
    return err({
      kind: 'network',
      message: `Cannot reach API at ${apiBase}. Start backend there or set VITE_API_BASE.`
    });
  }
}

async function handleUnauthorized() {
  if (!unauthorizedHandler || handlingUnauthorized) {
    return;
  }

  handlingUnauthorized = true;
  try {
    await unauthorizedHandler();
  } finally {
    handlingUnauthorized = false;
  }
}

async function send(path: string, init: RequestInit): Promise<Response> {
  const headers = new Headers(init.headers ?? undefined);
  const method = (init.method ?? 'GET').toUpperCase();
  const isStateChanging = method === 'POST' || method === 'PUT' || method === 'PATCH' || method === 'DELETE';
  if (isStateChanging && csrfToken && !headers.has('X-BoardOil-CSRF')) {
    headers.set('X-BoardOil-CSRF', csrfToken);
  }

  return fetch(buildApiUrl(path), {
    ...init,
    headers,
    credentials: 'include'
  });
}

function isStateChangingRequest(init: RequestInit) {
  const method = (init.method ?? 'GET').toUpperCase();
  return method === 'POST' || method === 'PUT' || method === 'PATCH' || method === 'DELETE';
}

function shouldAttemptSessionRefresh(path: string) {
  const normalisedPath = path.toLowerCase();
  if (!normalisedPath.startsWith('/api/')) {
    return false;
  }

  if (normalisedPath === '/api/auth/refresh') {
    return false;
  }

  return !isUnauthenticatedAuthPath(normalisedPath);
}

function shouldHandleUnauthorized(path: string) {
  const normalisedPath = path.toLowerCase();
  if (normalisedPath === '/api/auth/me') {
    return false;
  }

  return !isUnauthenticatedAuthPath(normalisedPath);
}

function isUnauthenticatedAuthPath(path: string) {
  return path === '/api/auth/login'
    || path === '/api/auth/register-initial-admin'
    || path === '/api/auth/bootstrap-status'
    || path === '/api/auth/machine/login'
    || path === '/api/auth/machine/refresh'
    || path === '/api/auth/machine/logout';
}

async function tryRefreshSession() {
  if (refreshInFlight) {
    return refreshInFlight;
  }

  refreshInFlight = (async () => {
    try {
      const response = await fetch(buildApiUrl('/api/auth/refresh'), {
        method: 'POST',
        credentials: 'include'
      });
      if (!response.ok) {
        return false;
      }

      const envelope = (await response.json().catch(() => null)) as ApiEnvelope<{ csrfToken?: string }> | null;
      if (envelope?.success === false) {
        return false;
      }

      const nextCsrfToken = envelope?.data?.csrfToken;
      if (typeof nextCsrfToken !== 'string' || nextCsrfToken.length === 0) {
        return false;
      }

      setCsrfToken(nextCsrfToken);
      return true;
    } catch {
      return false;
    }
  })();

  try {
    return await refreshInFlight;
  } finally {
    refreshInFlight = null;
  }
}

function isCsrfValidationFailure(status: number, message: string | undefined) {
  return status === 403 && message === csrfValidationFailureMessage;
}

async function tryParseEnvelope(response: Response) {
  return (await response.clone().json().catch(() => null)) as ApiEnvelope<unknown> | null;
}

type ErrorPayload = {
  message?: string;
  title?: string;
  detail?: string;
  error?: string;
  validationErrors?: Record<string, string[]>;
  errors?: Record<string, string[]>;
};

async function tryParseErrorPayload(response: Response): Promise<{ message?: string; validationErrors?: Record<string, string[]> } | null> {
  const payload = (await response.clone().json().catch(() => null)) as ErrorPayload | null;
  if (!payload || typeof payload !== 'object') {
    return null;
  }

  const message = typeof payload.message === 'string'
    ? payload.message
    : typeof payload.detail === 'string' && payload.detail.trim().length > 0
      ? payload.detail
      : typeof payload.title === 'string'
        ? payload.title
        : typeof payload.error === 'string'
          ? payload.error
          : undefined;

  const validationErrors = payload.validationErrors ?? payload.errors;
  if (!message && !validationErrors) {
    return null;
  }

  return { message, validationErrors };
}

function formatStatusMessage(response: Response) {
  const statusText = response.statusText?.trim();
  if (statusText) {
    return `Request failed with status ${response.status} (${statusText})`;
  }

  return `Request failed with status ${response.status}`;
}

async function parseEnvelope<T>(response: Response): Promise<Result<ApiEnvelope<T>, AppError>> {
  const body = (await response.json().catch(() => null)) as ApiEnvelope<T> | null;
  if (body) {
    return ok(body);
  }

  return err({
    kind: 'parse',
    message: 'Unexpected empty API response.'
  });
}

function extractFileName(contentDisposition: string) {
  const encodedMatch = contentDisposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (encodedMatch && encodedMatch[1]) {
    try {
      return decodeURIComponent(encodedMatch[1].trim().replace(/^"|"$/g, ''));
    } catch {
      return encodedMatch[1].trim().replace(/^"|"$/g, '');
    }
  }

  const simpleMatch = contentDisposition.match(/filename=([^;]+)/i);
  return simpleMatch![1].trim().replace(/^"|"$/g, '');
}
