import type { AppError } from '../types/appError';
import type { Result } from '../types/result';
import type {
  OwnUserProfile,
  UserDirectoryEntry,
  UserProfileEditModel,
  UserProfileImage
} from '../types/authTypes';
import { err, ok } from '../types/result';
import { deleteJson, getEnvelope, postFormData, putData } from './http';

export type UsersApi = ReturnType<typeof createUsersApi>;

export function createUsersApi() {
  async function getAllUsers(): Promise<Result<UserDirectoryEntry[], AppError>> {
    const envelopeResult = await getEnvelope<UserDirectoryEntry[]>('/api/users');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    return ok(envelopeResult.data.data ?? []);
  }

  async function getMyProfile(): Promise<Result<OwnUserProfile, AppError>> {
    const envelopeResult = await getEnvelope<OwnUserProfile>('/api/users/me');
    if (!envelopeResult.ok) {
      return envelopeResult;
    }

    if (!envelopeResult.data.data) {
      return err({
        kind: 'parse',
        message: 'Expected response payload was missing.'
      });
    }

    return ok(envelopeResult.data.data);
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

  async function deleteMyProfileImage(): Promise<Result<void, AppError>> {
    return deleteJson('/api/users/me/profile-image');
  }

  async function updateMyProfile(model: UserProfileEditModel): Promise<Result<OwnUserProfile, AppError>> {
    return putData<OwnUserProfile>('/api/users/me', model);
  }

  return {
    getAllUsers,
    getMyProfile,
    getMyProfileImage,
    uploadMyProfileImage,
    deleteMyProfileImage,
    updateMyProfile
  };
}

export const usersApi = createUsersApi();
