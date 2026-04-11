import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type {
  AccessToken,
  AuthSession,
  AuthUser,
  BootstrapStatusDto,
  CreateAccessTokenRequest,
  CreatedAccessToken,
  CsrfTokenDto,
  ManagedUser,
  UserDirectoryEntry
} from '../types/authTypes';
import { deleteJson, getEnvelope, postData, postJson, putData } from './http';

export type AuthApi = ReturnType<typeof createAuthApi>;

export function createAuthApi() {
  async function registerInitialAdmin(userName: string, password: string): Promise<Result<AuthSession, AppError>> {
    return postData<AuthSession>('/api/auth/register-initial-admin', { userName, password });
  }

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

  async function getBootstrapStatus(): Promise<Result<boolean, AppError>> {
    const envelopeResult = await getEnvelope<BootstrapStatusDto>('/api/auth/bootstrap-status');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data?.requiresInitialAdminSetup === true);
  }

  async function getUsers(): Promise<Result<ManagedUser[], AppError>> {
    const envelopeResult = await getEnvelope<ManagedUser[]>('/api/system/users');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function getAllUsers(): Promise<Result<UserDirectoryEntry[], AppError>> {
    const envelopeResult = await getEnvelope<UserDirectoryEntry[]>('/api/users');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createUser(userName: string, password: string, role: 'Admin' | 'Standard'): Promise<Result<ManagedUser, AppError>> {
    return postData<ManagedUser>('/api/system/users', { userName, password, role });
  }

  async function updateUserRole(userId: number, role: 'Admin' | 'Standard'): Promise<Result<ManagedUser, AppError>> {
    return putData<ManagedUser>(`/api/system/users/${userId}/role`, { role });
  }

  async function updateUserStatus(userId: number, isActive: boolean): Promise<Result<ManagedUser, AppError>> {
    return putData<ManagedUser>(`/api/system/users/${userId}/status`, { isActive });
  }

  async function getAccessTokens(): Promise<Result<AccessToken[], AppError>> {
    const envelopeResult = await getEnvelope<AccessToken[]>('/api/auth/access-tokens');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createAccessToken(request: CreateAccessTokenRequest): Promise<Result<CreatedAccessToken, AppError>> {
    return postData<CreatedAccessToken>('/api/auth/access-tokens', request);
  }

  async function revokeAccessToken(id: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/auth/access-tokens/${id}`);
  }

  return {
    registerInitialAdmin,
    login,
    logout,
    getMe,
    getCsrfToken,
    getBootstrapStatus,
    getUsers,
    getAllUsers,
    createUser,
    updateUserRole,
    updateUserStatus,
    getAccessTokens,
    createAccessToken,
    revokeAccessToken
  };
}

export const authApi = createAuthApi();
