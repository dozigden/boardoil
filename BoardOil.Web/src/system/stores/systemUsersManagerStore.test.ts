import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createSystemApi, type SystemApi } from '../../shared/api/systemApi';
import { useUiFeedbackStore } from '../../shared/stores/uiFeedbackStore';
import type { ManagedUser } from '../../shared/types/authTypes';
import { err, ok } from '../../shared/types/result';

vi.mock('../../shared/api/systemApi', () => ({
  createSystemApi: vi.fn()
}));

import { useSystemUsersManagerStore } from './systemUsersManagerStore';

describe('systemUsersManagerStore feedback', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.resetAllMocks();
  });

  it('shows successful mutations as toasts', async () => {
    const api = createApi();
    api.createUser.mockResolvedValue(ok(newUser('New User')));
    vi.mocked(createSystemApi).mockReturnValue(api);
    const store = useSystemUsersManagerStore();
    const feedback = useUiFeedbackStore();

    const created = await store.createUser({
      userName: 'new-user',
      displayName: 'New User',
      email: 'new-user@example.test',
      password: 'Password1234!',
      role: 'Standard'
    });

    expect(created).toBe(true);
    expect(feedback.toastMessage).toBe('Created successfully.');
    expect(feedback.toastTone).toBe('success');
    expect(store.errorMessage).toBeNull();
  });

  it('shows mutation failures as error toasts without replacing load errors', async () => {
    const api = createApi();
    api.deleteUser.mockResolvedValue(err({ kind: 'api', message: 'Could not delete user.' }));
    vi.mocked(createSystemApi).mockReturnValue(api);
    const store = useSystemUsersManagerStore();
    const feedback = useUiFeedbackStore();

    const deleted = await store.deleteUser(1);

    expect(deleted).toBe(false);
    expect(feedback.toastMessage).toBe('Could not delete user.');
    expect(feedback.toastTone).toBe('error');
    expect(store.errorMessage).toBeNull();
  });
});

function createApi() {
  return {
    createUser: vi.fn(),
    deleteUser: vi.fn(),
    getUsers: vi.fn(),
    resetUserPassword: vi.fn(),
    updateUser: vi.fn()
  } as unknown as SystemApi & {
    createUser: ReturnType<typeof vi.fn>;
    deleteUser: ReturnType<typeof vi.fn>;
  };
}

function newUser(displayName: string): ManagedUser {
  return {
    id: 1,
    userName: 'new-user',
    displayName,
    email: 'new-user@example.test',
    role: 'Standard',
    identityType: 'User',
    isActive: true,
    createdAtUtc: '2026-08-19T12:00:00Z',
    updatedAtUtc: '2026-08-19T12:00:00Z'
  };
}
