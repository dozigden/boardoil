import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { AuthSession, AuthUser, CsrfTokenDto } from '../types/authTypes';
import { getEnvelope, postData, postJson } from './http';

export type AuthApi = ReturnType<typeof createAuthApi>;

export function createAuthApi() {
  async function login(userName: string, password: string): Promise<Result<AuthSession, AppError>> {
    return postData<AuthSession>('/api/auth/login', { userName, password });
  }

  async function logout(): Promise<Result<void, AppError>> {
    return postJson('/api/auth/logout', {});
  }

  async function getMe(): Promise<Result<AuthUser | null, AppError>> {
    const envelopeResult = await getEnvelope<AuthUser>('/api/auth/me');
    if (!envelopeResult.ok) {
      return err(envelopeResult.error);
    }

    if (!envelopeResult.data.data) {
      return ok(null);
    }

    return ok(envelopeResult.data.data);
  }

  async function getCsrfToken(): Promise<Result<string, AppError>> {
    const envelopeResult = await getEnvelope<CsrfTokenDto>('/api/auth/csrf');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    if (!envelopeResult.data.data?.csrfToken) {
      return err({
        kind: 'api',
        message: envelopeResult.data.message ?? 'Failed to get CSRF token.'
      });
    }

    return ok(envelopeResult.data.data.csrfToken);
  }

  return {
    login,
    logout,
    getMe,
    getCsrfToken
  };
}

export const authApi = createAuthApi();
