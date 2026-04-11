import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { UserDirectoryEntry } from '../types/authTypes';
import { ok } from '../types/result';
import { getEnvelope } from './http';

export type UsersApi = ReturnType<typeof createUsersApi>;

export function createUsersApi() {
  async function getAllUsers(): Promise<Result<UserDirectoryEntry[], AppError>> {
    const envelopeResult = await getEnvelope<UserDirectoryEntry[]>('/api/users');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  return {
    getAllUsers
  };
}

export const usersApi = createUsersApi();
