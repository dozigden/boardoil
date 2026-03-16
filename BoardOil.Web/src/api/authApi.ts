import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { AuthSession, AuthUser, CsrfTokenDto, ManagedUser } from '../types/authTypes';
import { getEnvelope, patchData, postData, postJson } from './http';

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

  async function getUsers(): Promise<Result<ManagedUser[], AppError>> {
    const envelopeResult = await getEnvelope<ManagedUser[]>('/api/users');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createUser(userName: string, password: string, role: 'Admin' | 'Standard'): Promise<Result<ManagedUser, AppError>> {
    return postData<ManagedUser>('/api/users', { userName, password, role });
  }

  async function updateUserRole(userId: number, role: 'Admin' | 'Standard'): Promise<Result<ManagedUser, AppError>> {
    return patchData<ManagedUser>(`/api/users/${userId}/role`, { role });
  }

  async function updateUserStatus(userId: number, isActive: boolean): Promise<Result<ManagedUser, AppError>> {
    return patchData<ManagedUser>(`/api/users/${userId}/status`, { isActive });
  }

  return {
    login,
    logout,
    getMe,
    getCsrfToken,
    getUsers,
    createUser,
    updateUserRole,
    updateUserStatus
  };
}

export const authApi = createAuthApi();
