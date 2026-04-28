import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useUserProfileImageStore } from './userProfileImageStore';
import { err, ok } from '../types/result';
import type { AppError } from '../types/appError';

const usersApi = {
  getMyProfileImage: vi.fn(),
  uploadMyProfileImage: vi.fn(),
  deleteMyProfileImage: vi.fn()
};

vi.mock('../api/usersApi', () => ({
  createUsersApi: () => usersApi
}));

vi.mock('../api/config', () => ({
  buildApiUrl: (path: string) => `http://localhost:5000${path}`
}));

describe('userProfileImageStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    usersApi.getMyProfileImage.mockResolvedValue(ok(null));
    usersApi.uploadMyProfileImage.mockResolvedValue(ok({
      id: 1,
      contentType: 'image/png',
      relativePath: 'userprofile/1/a.png',
      byteLength: 123,
      width: 128,
      height: 128,
      createdAtUtc: '2026-04-28T00:00:00Z',
      updatedAtUtc: '2026-04-28T00:00:00Z'
    }));
    usersApi.deleteMyProfileImage.mockResolvedValue(ok(undefined));
  });

  it('loadOwnProfileImage loads once unless forced', async () => {
    const store = useUserProfileImageStore();

    await store.loadOwnProfileImage();
    await store.loadOwnProfileImage();
    await store.loadOwnProfileImage(true);

    expect(usersApi.getMyProfileImage).toHaveBeenCalledTimes(2);
  });

  it('uploadOwnProfileImage sets profile image and computed url', async () => {
    const store = useUserProfileImageStore();
    const file = new File(['x'], 'avatar.png', { type: 'image/png' });

    const success = await store.uploadOwnProfileImage(file);

    expect(success).toBe(true);
    expect(store.userProfileImage?.relativePath).toBe('userprofile/1/a.png');
    expect(store.userProfileImageUrl).toBe('http://localhost:5000/images/userprofile/1/a.png');
  });

  it('deleteOwnProfileImage clears profile image on success', async () => {
    const store = useUserProfileImageStore();
    store.userProfileImage = {
      id: 1,
      contentType: 'image/png',
      relativePath: 'userprofile/1/a.png',
      byteLength: 123,
      width: 128,
      height: 128,
      createdAtUtc: '2026-04-28T00:00:00Z',
      updatedAtUtc: '2026-04-28T00:00:00Z'
    };

    const success = await store.deleteOwnProfileImage();

    expect(success).toBe(true);
    expect(store.userProfileImage).toBeNull();
  });

  it('deleteOwnProfileImage exposes API error message on failure', async () => {
    const store = useUserProfileImageStore();
    const apiError: AppError = { kind: 'api', message: 'Delete failed.' };
    usersApi.deleteMyProfileImage.mockResolvedValue(err(apiError));

    const success = await store.deleteOwnProfileImage();

    expect(success).toBe(false);
    expect(store.errorMessage).toBe('Delete failed.');
  });
});
