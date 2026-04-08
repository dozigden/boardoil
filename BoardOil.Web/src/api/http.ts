import { apiBase, buildApiUrl } from './config';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { ApiEnvelope } from '../types/boardTypes';
import type { Result } from '../types/result';

let csrfToken: string | null = null;
let unauthorizedHandler: (() => void | Promise<void>) | null = null;
let handlingUnauthorized = false;
let refreshInFlight: Promise<boolean> | null = null;

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
    message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`
  });
}

export async function postJson(path: string, payload: unknown): Promise<Result<void, AppError>> {
  return sendJson('POST', path, payload);
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
    message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`
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
    message: envelopeResult.data.message ?? `Request failed with status ${responseResult.data.status}`
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

async function request(path: string, init: RequestInit): Promise<Result<Response, AppError>> {
  try {
    const response = await send(path, init);
    if (!response.ok) {
      if (response.status === 401 && shouldAttemptSessionRefresh(path)) {
        const refreshed = await tryRefreshSession();
        if (refreshed) {
          const retriedResponse = await send(path, init);
          if (retriedResponse.ok) {
            return ok(retriedResponse);
          }

          const retriedEnvelope = await tryParseEnvelope(retriedResponse);
          if (retriedResponse.status === 401 && shouldHandleUnauthorized(path)) {
            void handleUnauthorized();
          }

          return err({
            kind: 'http',
            message: retriedEnvelope?.message ?? `Request failed with status ${retriedResponse.status}`,
            statusCode: retriedResponse.status
          });
        }
      }

      const envelope = await tryParseEnvelope(response);
      if (response.status === 401 && shouldHandleUnauthorized(path)) {
        void handleUnauthorized();
      }

      return err({
        kind: 'http',
        message: envelope?.message ?? `Request failed with status ${response.status}`,
        statusCode: response.status
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
  return !isUnauthenticatedAuthPath(path.toLowerCase());
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

async function tryParseEnvelope(response: Response) {
  return (await response.clone().json().catch(() => null)) as ApiEnvelope<unknown> | null;
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
