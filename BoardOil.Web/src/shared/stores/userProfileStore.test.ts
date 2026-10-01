import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { useUserProfileStore } from './userProfileStore';
import { useAuthStore } from './authStore';
import { err, ok } from '../types/result';
import type { OwnUserProfile } from '../types/authTypes';
import type { AppError } from '../types/appError';
import type { Result } from '../types/result';

const usersApi = {
  getMyProfile: vi.fn(),
  updateMyProfile: vi.fn()
};

vi.mock('../api/usersApi', () => ({ createUsersApi: () => usersApi }));
vi.mock('../api/authApi', () => ({ createAuthApi: () => ({}) }));
vi.mock('../api/http', () => ({
  setCsrfToken: vi.fn(),
  setUnauthorizedHandler: vi.fn()
}));
vi.mock('../../router', () => ({
  router: {
    currentRoute: { value: { name: 'user-admin-profile', fullPath: '/user-admin/profile' } },
    replace: vi.fn()
  }
}));

const originalProfile: OwnUserProfile = {
  id: 7,
  userName: 'member',
  displayName: 'Original',
  email: 'original@example.test',
  role: 'Standard'
};

describe('userProfileStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    usersApi.getMyProfile.mockResolvedValue(ok(originalProfile));
    useAuthStore().user = {
      id: originalProfile.id,
      userName: originalProfile.userName,
      displayName: originalProfile.displayName,
      role: originalProfile.role
    };
  });

  it('loads the authoritative profile for the editor', async () => {
    const store = useUserProfileStore();

    expect(await store.loadOwnProfile()).toEqual(originalProfile);
    expect(store.ownProfile).toEqual(originalProfile);
  });

  it.each(['success', 'failure'] as const)('coordinates profile and auth updates on save %s', async outcome => {
    const store = useUserProfileStore();
    const auth = useAuthStore();
    await store.loadOwnProfile();
    const originalUser = auth.user;
    const saved = { ...originalProfile, displayName: 'Updated', email: 'updated@example.test' };
    const draft = { displayName: ' Updated ', email: saved.email };
    let complete!: (result: Result<OwnUserProfile, AppError>) => void;
    usersApi.updateMyProfile.mockReturnValueOnce(new Promise(resolve => { complete = resolve; }));

    const pending = store.saveOwnProfile(draft);
    expect(store.busy).toBe(true);
    expect(store.ownProfile).toEqual(originalProfile);
    expect(auth.user).toBe(originalUser);
    expect(usersApi.updateMyProfile).toHaveBeenCalledWith(draft);

    complete(outcome === 'success' ? ok(saved) : err({ kind: 'api', message: 'Save failed.' }));
    expect(await pending).toEqual(outcome === 'success' ? saved : null);
    expect(store.ownProfile).toEqual(outcome === 'success' ? saved : originalProfile);
    expect(auth.user).toEqual(outcome === 'success' ? { ...originalUser, displayName: saved.displayName } : originalUser);
    expect(store.errorMessage).toBe(outcome === 'success' ? null : 'Save failed.');
    expect(store.busy).toBe(false);
    expect(usersApi.getMyProfile).toHaveBeenCalledTimes(1);
    expect(draft.displayName).toBe(' Updated ');
  });

  it('resets the profile and save error without clearing authentication', async () => {
    const store = useUserProfileStore();
    const auth = useAuthStore();
    await store.loadOwnProfile();
    usersApi.updateMyProfile.mockResolvedValueOnce(err({ kind: 'api', message: 'Save failed.' }));
    await store.saveOwnProfile({ displayName: 'Updated', email: 'updated@example.test' });

    store.reset();

    expect(store.ownProfile).toBeNull();
    expect(store.errorMessage).toBeNull();
    expect(store.busy).toBe(false);
    expect(auth.isAuthenticated).toBe(true);
    expect(auth.user?.displayName).toBe(originalProfile.displayName);
  });
});
