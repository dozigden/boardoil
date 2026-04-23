import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { BoardMember, BoardMemberRole, SystemBoardSummary } from '../types/boardTypes';
import type {
  AccessToken,
  ClientAccount,
  CreateClientAccessTokenRequest,
  CreateClientAccountRequest,
  CreatedAccessToken,
  CreatedClientAccount,
  ManagedUser,
  UpdateClientAccountRequest,
  UpdateManagedUserRequest
} from '../types/authTypes';
import type { ConfigurationDto, UpdateConfigurationRequest } from '../types/configurationTypes';
import { err, ok } from '../types/result';
import { deleteJson, getEnvelope, patchData, postData, putData, putJson } from './http';

export type SystemApi = ReturnType<typeof createSystemApi>;

export function createSystemApi() {
  async function getConfiguration(): Promise<Result<ConfigurationDto, AppError>> {
    const result = await getEnvelope<ConfigurationDto>('/api/system/configuration');
    if (!result.ok) {
      return result;
    }

    if (!result.data.data) {
      return err({
        kind: 'api',
        message: result.data.message ?? 'Failed to load configuration.'
      });
    }

    return ok(result.data.data);
  }

  async function updateConfiguration(request: UpdateConfigurationRequest): Promise<Result<ConfigurationDto, AppError>> {
    return putData<ConfigurationDto>('/api/system/configuration', request);
  }

  async function getBoards(): Promise<Result<SystemBoardSummary[], AppError>> {
    const envelopeResult = await getEnvelope<SystemBoardSummary[]>('/api/system/boards');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function getBoardMembers(boardId: number): Promise<Result<BoardMember[], AppError>> {
    const envelopeResult = await getEnvelope<BoardMember[]>(`/api/system/boards/${boardId}/members`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function addBoardMember(boardId: number, userId: number, role: BoardMemberRole): Promise<Result<BoardMember, AppError>> {
    return postData<BoardMember>(`/api/system/boards/${boardId}/members`, { userId, role });
  }

  async function updateBoardMemberRole(
    boardId: number,
    userId: number,
    role: BoardMemberRole
  ): Promise<Result<BoardMember, AppError>> {
    return patchData<BoardMember>(`/api/system/boards/${boardId}/members/${userId}`, { role });
  }

  async function removeBoardMember(boardId: number, userId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/system/boards/${boardId}/members/${userId}`);
  }

  async function getUsers(): Promise<Result<ManagedUser[], AppError>> {
    const envelopeResult = await getEnvelope<ManagedUser[]>('/api/system/users');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createUser(userName: string, email: string, password: string, role: 'Admin' | 'Standard'): Promise<Result<ManagedUser, AppError>> {
    return postData<ManagedUser>('/api/system/users', { userName, email, password, role });
  }

  async function updateUser(userId: number, request: UpdateManagedUserRequest): Promise<Result<ManagedUser, AppError>> {
    return putData<ManagedUser>(`/api/system/users/${userId}`, request);
  }

  async function updateUserRole(userId: number, role: 'Admin' | 'Standard'): Promise<Result<ManagedUser, AppError>> {
    return putData<ManagedUser>(`/api/system/users/${userId}/role`, { role });
  }

  async function updateUserStatus(userId: number, isActive: boolean): Promise<Result<ManagedUser, AppError>> {
    return putData<ManagedUser>(`/api/system/users/${userId}/status`, { isActive });
  }

  async function resetUserPassword(userId: number, newPassword: string): Promise<Result<void, AppError>> {
    return putJson(`/api/system/users/${userId}/password`, { newPassword });
  }

  async function deleteUser(userId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/system/users/${userId}`);
  }

  async function getClientAccounts(): Promise<Result<ClientAccount[], AppError>> {
    const envelopeResult = await getEnvelope<ClientAccount[]>('/api/system/client-accounts');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createClientAccount(request: CreateClientAccountRequest): Promise<Result<CreatedClientAccount, AppError>> {
    return postData<CreatedClientAccount>('/api/system/client-accounts', request);
  }

  async function updateClientAccount(clientAccountId: number, request: UpdateClientAccountRequest): Promise<Result<ClientAccount, AppError>> {
    return putData<ClientAccount>(`/api/system/client-accounts/${clientAccountId}`, request);
  }

  async function getClientAccountTokens(clientAccountId: number): Promise<Result<AccessToken[], AppError>> {
    const envelopeResult = await getEnvelope<AccessToken[]>(`/api/system/client-accounts/${clientAccountId}/tokens`);
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function createClientAccountToken(
    clientAccountId: number,
    request: CreateClientAccessTokenRequest
  ): Promise<Result<CreatedAccessToken, AppError>> {
    return postData<CreatedAccessToken>(`/api/system/client-accounts/${clientAccountId}/tokens`, request);
  }

  async function revokeClientAccountToken(clientAccountId: number, tokenId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/system/client-accounts/${clientAccountId}/tokens/${tokenId}`);
  }

  async function deleteClientAccount(clientAccountId: number): Promise<Result<void, AppError>> {
    return deleteJson(`/api/system/client-accounts/${clientAccountId}`);
  }

  return {
    getConfiguration,
    updateConfiguration,
    getBoards,
    getBoardMembers,
    addBoardMember,
    updateBoardMemberRole,
    removeBoardMember,
    getUsers,
    createUser,
    updateUser,
    updateUserRole,
    updateUserStatus,
    resetUserPassword,
    deleteUser,
    getClientAccounts,
    createClientAccount,
    updateClientAccount,
    getClientAccountTokens,
    createClientAccountToken,
    revokeClientAccountToken,
    deleteClientAccount
  };
}

export const systemApi = createSystemApi();
