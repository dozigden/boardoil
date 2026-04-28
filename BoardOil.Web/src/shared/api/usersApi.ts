import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type { UserDirectoryEntry, UserProfileImage } from '../types/authTypes';
import { err, ok } from '../types/result';
import { getEnvelope, postFormData } from './http';

export type UsersApi = ReturnType<typeof createUsersApi>;

export function createUsersApi() {
  async function getAllUsers(): Promise<Result<UserDirectoryEntry[], AppError>> {
    const envelopeResult = await getEnvelope<UserDirectoryEntry[]>('/api/users');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function getMyProfileImage(): Promise<Result<UserProfileImage | null, AppError>> {
    const envelopeResult = await getEnvelope<UserProfileImage>('/api/users/me/profile-image');
    if (!envelopeResult.ok) {
      if (envelopeResult.error.kind === 'http' && envelopeResult.error.statusCode === 404) {
        return ok(null);
      }

      return err(envelopeResult.error);
    }

    if (!envelopeResult.data.data) {
      return ok(null);
    }

    return ok(envelopeResult.data.data);
  }

  async function uploadMyProfileImage(file: File): Promise<Result<UserProfileImage, AppError>> {
    const formData = new FormData();
    formData.append('file', file);
    return postFormData<UserProfileImage>('/api/users/me/profile-image', formData);
  }

  return {
    getAllUsers,
    getMyProfileImage,
    uploadMyProfileImage
  };
}

export const usersApi = createUsersApi();
