import { apiBase, buildApiUrl } from './config';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { ApiEnvelope } from '../types/boardTypes';
import type { Result } from '../types/result';

let csrfToken: string | null = null;
let unauthorizedHandler: (() => void | Promise<void>) | null = null;
let handlingUnauthorized = false;

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
    const headers = new Headers(init.headers ?? undefined);
    const method = (init.method ?? 'GET').toUpperCase();
    const isStateChanging = method === 'POST' || method === 'PUT' || method === 'PATCH' || method === 'DELETE';
    if (isStateChanging && csrfToken && !headers.has('X-BoardOil-CSRF')) {
      headers.set('X-BoardOil-CSRF', csrfToken);
    }

    const response = await fetch(buildApiUrl(path), {
      ...init,
      headers,
      credentials: 'include'
    });
    if (!response.ok) {
      const envelope = (await response.clone().json().catch(() => null)) as ApiEnvelope<unknown> | null;
      if (response.status === 401) {
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
